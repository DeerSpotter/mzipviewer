using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

/// <summary>
/// Visual diagram parser validated against CATIA Magic 2026.1 bundled UPDM/UAF samples.
/// Modern diagram geometry is stored as x,y,width,height.
/// </summary>
public sealed class ModernMagicDrawVisualParser
{
    private static readonly string[] ModelEntryNames =
    [
        "com.nomagic.magicdraw.uml_model.model",
        "com.nomagic.magicdraw.uml_model.shared_model"
    ];

    public void ReplaceVisuals(ZipArchive archive, MdzipInventory inventory)
    {
        inventory.Diagrams.Clear();
        inventory.Presentations.Clear();
        inventory.Notes.Clear();
        inventory.Diagnostics.RemoveAll(d => d.Category.Equals("Diagram", StringComparison.OrdinalIgnoreCase));

        foreach (var entry in archive.Entries.Where(IsModelEntry))
            ParseModelEntry(archive, entry, inventory);
    }

    private static bool IsModelEntry(ZipArchiveEntry entry) =>
        entry.Length > 0 &&
        (ModelEntryNames.Any(n => entry.FullName.EndsWith(n, StringComparison.OrdinalIgnoreCase)) ||
         entry.FullName.EndsWith(".mdxml", StringComparison.OrdinalIgnoreCase));

    private static void ParseModelEntry(ZipArchive archive, ZipArchiveEntry entry, MdzipInventory inventory)
    {
        XDocument model;
        try
        {
            using var stream = entry.Open();
            model = LoadMagicDrawXml(stream);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            inventory.Diagnostics.Add(new("Error", "Diagram", ex.Message, entry.FullName));
            return;
        }

        if (model.Root is null)
            return;

        foreach (var ownedDiagram in model.Descendants().Where(e => Local(e) == "ownedDiagram"))
        {
            var name = Attr(ownedDiagram, "name") ?? "(unnamed diagram)";
            var owner = Attr(ownedDiagram, "ownerOfDiagram") ?? Attr(ownedDiagram.Parent, "id");
            var streamContentId = ownedDiagram.Descendants()
                .Where(e => Local(e) == "binaryObject")
                .Select(e => Attr(e, "streamContentID"))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var diagramId = !string.IsNullOrWhiteSpace(owner)
                ? owner + "\u0003" + name
                : Attr(ownedDiagram, "id") ?? "diagram-" + inventory.Diagrams.Count;

            inventory.Diagrams.Add(new(diagramId, name, "Diagram", entry.FullName, owner, streamContentId));

            if (string.IsNullOrWhiteSpace(streamContentId))
            {
                inventory.Diagnostics.Add(new("Warning", "Diagram", $"'{name}' has no streamContentID.", entry.FullName));
                continue;
            }

            var payload = FindEntry(archive, streamContentId);
            if (payload is null)
            {
                inventory.Diagnostics.Add(new("Warning", "Diagram", $"'{name}' payload '{streamContentId}' was not found.", entry.FullName));
                continue;
            }

            ParsePayload(payload, diagramId, name, inventory);
        }
    }

    private static void ParsePayload(ZipArchiveEntry entry, string diagramId, string diagramName, MdzipInventory inventory)
    {
        XDocument diagram;
        try
        {
            using var stream = entry.Open();
            diagram = LoadMagicDrawXml(stream);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            inventory.Diagnostics.Add(new("Error", "Diagram", $"'{diagramName}': {ex.Message}", entry.FullName));
            return;
        }

        var mdElements = diagram.Descendants().Where(e => Local(e) == "mdElement").ToList();
        var drawable = 0;
        var presentationToModel = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mdElement in mdElements)
        {
            var presentationId = Attr(mdElement, "id") ?? $"presentation-{inventory.Presentations.Count + 1}";
            var elementClass = Attr(mdElement, "elementClass") ?? "PresentationElement";
            var geometryText = mdElement.Descendants().FirstOrDefault(e => Local(e) == "geometry")?.Value;
            var bounds = ParseBounds(geometryText);
            var route = ParseRoute(geometryText, elementClass);
            var modelElementId = GetReferencedElementId(mdElement);

            if (bounds is null && route.Count < 2)
                continue;

            var displayText = mdElement.Descendants()
                .Where(e => Local(e) == "text")
                .Select(e => StripHtml(e.Value))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var visualId = modelElementId ?? $"visual::{diagramId}::{presentationId}";
            presentationToModel[presentationId] = visualId;

            if (!inventory.Elements.Any(e => e.Id.Equals(visualId, StringComparison.Ordinal)))
            {
                inventory.Elements.Add(new(
                    visualId,
                    string.IsNullOrWhiteSpace(displayText) ? elementClass : displayText,
                    elementClass,
                    entry.FullName,
                    diagramId,
                    elementClass));
            }

            inventory.Presentations.Add(new(
                presentationId,
                diagramId,
                visualId,
                elementClass,
                bounds,
                route,
                entry.FullName));

            if (elementClass.Equals("Note", StringComparison.OrdinalIgnoreCase) ||
                elementClass.Equals("TextBox", StringComparison.OrdinalIgnoreCase))
            {
                inventory.Notes.Add(new(
                    presentationId,
                    diagramId,
                    displayText ?? string.Empty,
                    modelElementId,
                    bounds ?? new LayoutBounds(0, 0, 1, 1),
                    entry.FullName));
            }

            drawable++;
        }

        foreach (var mdElement in mdElements)
        {
            var elementClass = Attr(mdElement, "elementClass") ?? string.Empty;
            if (!IsConnectorClass(elementClass))
                continue;

            var presentationId = Attr(mdElement, "id") ?? string.Empty;
            var firstEnd = FirstReference(mdElement, "linkFirstEndID");
            var secondEnd = FirstReference(mdElement, "linkSecondEndID");
            if (firstEnd is null || secondEnd is null)
                continue;

            if (!presentationToModel.TryGetValue(secondEnd, out var sourceId) ||
                !presentationToModel.TryGetValue(firstEnd, out var targetId))
                continue;

            var relationshipId = string.IsNullOrWhiteSpace(presentationId)
                ? $"visual-link::{diagramId}::{inventory.Relationships.Count + 1}"
                : $"visual-link::{diagramId}::{presentationId}";

            if (!inventory.Relationships.Any(r => r.Id.Equals(relationshipId, StringComparison.Ordinal)))
                inventory.Relationships.Add(new(relationshipId, elementClass, elementClass, entry.FullName, diagramId, sourceId, targetId));
        }

        inventory.Diagnostics.Add(new(
            drawable > 0 ? "Info" : "Warning",
            "Diagram",
            $"'{diagramName}': {mdElements.Count:N0} mdElement records, {drawable:N0} drawable presentations.",
            entry.FullName));
    }

    private static XDocument LoadMagicDrawXml(Stream stream)
    {
        var nameTable = new NameTable();
        var namespaceManager = new XmlNamespaceManager(nameTable);
        namespaceManager.AddNamespace("xmi", "http://www.omg.org/spec/XMI/20131001");
        var context = new XmlParserContext(nameTable, namespaceManager, string.Empty, XmlSpace.Default);
        var settings = new XmlReaderSettings
        {
            NameTable = nameTable,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreWhitespace = false
        };
        using var reader = XmlReader.Create(stream, settings, context);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static LayoutBounds? ParseBounds(string? geometry)
    {
        var values = ParseNumbers(geometry).Take(4).ToArray();
        if (values.Length < 4)
            return null;

        var x = values[0];
        var y = values[1];
        var width = values[2];
        var height = values[3];
        if (width < 0 || height < 0)
            return null;

        return new LayoutBounds(x, y, Math.Max(1, width), Math.Max(1, height));
    }

    private static IReadOnlyList<LayoutPoint> ParseRoute(string? geometry, string elementClass)
    {
        if (!IsConnectorClass(elementClass) || string.IsNullOrWhiteSpace(geometry) || !geometry.Contains(';'))
            return [];

        var points = new List<LayoutPoint>();
        foreach (var segment in geometry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = ParseNumbers(segment).Take(2).ToArray();
            if (values.Length == 2)
                points.Add(new(values[0], values[1]));
        }
        return points.Count >= 2 ? points : [];
    }

    private static string? GetReferencedElementId(XElement mdElement)
    {
        var node = mdElement.Descendants().FirstOrDefault(e => Local(e) == "elementID");
        return node is null
            ? null
            : NormalizeReference(Attr(node, "href") ?? Attr(node, "idref") ?? Attr(node, "id") ?? node.Value);
    }

    private static string? FirstReference(XElement element, string name)
    {
        var child = element.Descendants().FirstOrDefault(e => Local(e).Equals(name, StringComparison.OrdinalIgnoreCase));
        return child is null
            ? null
            : NormalizeReference(Attr(child, "href") ?? Attr(child, "idref") ?? Attr(child, "id") ?? child.Value);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string id) =>
        archive.Entries.FirstOrDefault(e => e.FullName.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
        archive.Entries.FirstOrDefault(e => e.Name.Equals(id, StringComparison.OrdinalIgnoreCase)) ??
        archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + id, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<double> ParseNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (var token in value.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                yield return number;
    }

    private static bool IsConnectorClass(string value) =>
        value.Contains("Flow", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Connector", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Association", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Dependency", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Transition", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Link", StringComparison.OrdinalIgnoreCase);

    private static string StripHtml(string value)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string Local(XElement element) => element.Name.LocalName;

    private static string? Attr(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? NormalizeReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var hash = value.LastIndexOf('#');
        return hash >= 0 && hash + 1 < value.Length ? value[(hash + 1)..] : value;
    }
}
