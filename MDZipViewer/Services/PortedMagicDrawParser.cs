// Ported from the MagicdrawMigrator reader in Geert Bellekens' Enterprise Architect Toolpack.
// Original copyright and attribution remain with the upstream author.
// Enterprise Architect output dependencies were replaced with MDZipViewer model records.

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

public sealed class PortedMagicDrawParser
{
    private static readonly string[] ModelEntryNames =
    [
        "com.nomagic.magicdraw.uml_model.model",
        "com.nomagic.magicdraw.uml_model.shared_model"
    ];

    public void Parse(ZipArchive archive, MdzipInventory inventory)
    {
        var modelEntries = archive.Entries
            .Where(IsModelEntry)
            .OrderByDescending(e => ModelEntryNames.Any(n => e.FullName.EndsWith(n, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (modelEntries.Count == 0)
            throw new InvalidDataException("No MagicDraw model XML was found inside the archive.");

        foreach (var entry in modelEntries)
            ParseModelEntry(archive, entry, inventory);
    }

    private static bool IsModelEntry(ZipArchiveEntry entry) =>
        entry.Length > 0 &&
        (ModelEntryNames.Any(n => entry.FullName.EndsWith(n, StringComparison.OrdinalIgnoreCase))
         || entry.FullName.EndsWith(".mdxml", StringComparison.OrdinalIgnoreCase));

    private static void ParseModelEntry(ZipArchive archive, ZipArchiveEntry entry, MdzipInventory inventory)
    {
        XDocument document;
        try
        {
            using var stream = entry.Open();
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
        {
            inventory.Diagnostics.Add(new("Error", "Model", ex.Message, entry.FullName));
            return;
        }

        var root = document.Root;
        if (root is null)
            return;

        inventory.Documents.Add(new ModelDocument(
            entry.FullName,
            root.Name.LocalName,
            Descendants(root, "Documentation").Select(e => Attr(e, "exporterVersion")).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))));

        ParseElements(root, entry.FullName, inventory);
        ParseConstraints(root, entry.FullName, inventory);
        ParseLifelines(root, entry.FullName, inventory);
        ParseMessages(root, entry.FullName, inventory);
        ParseOwnedDiagrams(archive, root, entry.FullName, inventory);
    }

    private static void ParseElements(XElement root, string documentName, MdzipInventory inventory)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            var id = Attr(element, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var rawType = Attr(element, "type") ?? element.Name.LocalName;
            var type = NormalizeType(rawType);
            var name = Attr(element, "name") ?? "(unnamed)";
            var ownerId = Attr(element.Parent, "id");
            var humanType = Attr(element, "humanType");

            inventory.Elements.Add(new(id, name, type, documentName, ownerId, humanType));

            if (!MdzipTypeClassifier.IsRelationship(type))
                continue;

            var source = FirstReference(element, "source", "client", "specific", "informationSource", "end1", "from");
            var target = FirstReference(element, "target", "supplier", "general", "informationTarget", "end2", "to");

            if (source is null || target is null)
            {
                var memberEnds = References(Attr(element, "memberEnd")).Take(2).ToArray();
                if (memberEnds.Length == 2)
                {
                    source ??= memberEnds[0];
                    target ??= memberEnds[1];
                }
            }

            inventory.Relationships.Add(new(id, name, type, documentName, ownerId, source, target));
        }
    }

    private static void ParseConstraints(XElement root, string documentName, MdzipInventory inventory)
    {
        foreach (var rule in Descendants(root, "ownedRule"))
        {
            var owner = rule.Parent;
            var ownerId = Attr(owner, "id");
            if (string.IsNullOrWhiteSpace(ownerId))
                continue;

            var name = Attr(rule, "name");
            var specification = rule.Elements().FirstOrDefault(e => Local(e) == "specification");
            var body = specification?.Elements().FirstOrDefault(e => Local(e) == "body")?.Value;
            var language = specification?.Elements().FirstOrDefault(e => Local(e) == "language")?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(body))
                continue;

            inventory.Constraints.Add(new(ownerId, name, body.Replace("\n", Environment.NewLine), language ?? string.Empty, documentName));
        }
    }

    private static void ParseLifelines(XElement root, string documentName, MdzipInventory inventory)
    {
        var ownedAttributes = Descendants(root, "ownedAttribute")
            .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
            .ToDictionary(e => Attr(e, "id")!, StringComparer.Ordinal);

        foreach (var lifeline in Descendants(root, "lifeline"))
        {
            var id = Attr(lifeline, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var represents = Attr(lifeline, "represents");
            string? representedType = null;
            if (represents is not null && ownedAttributes.TryGetValue(represents, out var property))
                representedType = Attr(property, "type") ?? FirstReference(property, "type");

            inventory.Lifelines.Add(new(id, represents, representedType, documentName));
        }
    }

    private static void ParseMessages(XElement root, string documentName, MdzipInventory inventory)
    {
        var fragments = Descendants(root, "fragment")
            .Where(e => !string.IsNullOrWhiteSpace(Attr(e, "id")))
            .ToDictionary(e => Attr(e, "id")!, StringComparer.Ordinal);

        foreach (var message in Descendants(root, "message"))
        {
            var id = Attr(message, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var sendEvent = Attr(message, "sendEvent");
            var receiveEvent = Attr(message, "receiveEvent");
            var source = CoveredLifeline(fragments, sendEvent);
            var target = CoveredLifeline(fragments, receiveEvent);
            inventory.Messages.Add(new(id, Attr(message, "name") ?? "(unnamed)", source, target, sendEvent, receiveEvent, documentName));
        }
    }

    private static string? CoveredLifeline(IReadOnlyDictionary<string, XElement> fragments, string? occurrenceId)
    {
        if (occurrenceId is null || !fragments.TryGetValue(occurrenceId, out var occurrence))
            return null;
        return FirstReference(occurrence, "covered");
    }

    private static void ParseOwnedDiagrams(ZipArchive archive, XElement root, string documentName, MdzipInventory inventory)
    {
        foreach (var ownedDiagram in Descendants(root, "ownedDiagram"))
        {
            var name = Attr(ownedDiagram, "name") ?? "(unnamed diagram)";
            var owner = Attr(ownedDiagram, "ownerOfDiagram") ?? Attr(ownedDiagram.Parent, "id");
            var streamContentId = Descendants(ownedDiagram, "binaryObject")
                .Select(e => Attr(e, "streamContentID"))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var diagramId = !string.IsNullOrWhiteSpace(owner)
                ? owner + "\u0003" + name
                : Attr(ownedDiagram, "id") ?? "diagram-" + inventory.Diagrams.Count;

            inventory.Diagrams.Add(new(diagramId, name, "Diagram", documentName, owner, streamContentId));

            if (string.IsNullOrWhiteSpace(streamContentId))
            {
                inventory.Diagnostics.Add(new("Warning", "Diagram", "ownedDiagram has no binaryObject streamContentID.", documentName));
                continue;
            }

            var binaryEntry = FindEntry(archive, streamContentId);
            if (binaryEntry is null)
            {
                inventory.Diagnostics.Add(new("Warning", "Diagram", $"Referenced diagram payload '{streamContentId}' was not found.", documentName));
                continue;
            }

            ParseDiagramPayload(binaryEntry, diagramId, inventory);
        }
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string streamContentId) =>
        archive.Entries.FirstOrDefault(e => e.FullName.Equals(streamContentId, StringComparison.OrdinalIgnoreCase))
        ?? archive.Entries.FirstOrDefault(e => e.Name.Equals(streamContentId, StringComparison.OrdinalIgnoreCase))
        ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + streamContentId, StringComparison.OrdinalIgnoreCase));

    private static void ParseDiagramPayload(ZipArchiveEntry entry, string diagramId, MdzipInventory inventory)
    {
        XDocument diagram;
        try
        {
            using var stream = entry.Open();
            diagram = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
        {
            inventory.Diagnostics.Add(new("Error", "Diagram", ex.Message, entry.FullName));
            return;
        }

        foreach (var mdElement in diagram.Descendants().Where(e => Local(e) == "mdElement"))
        {
            var presentationId = Attr(mdElement, "id") ?? "presentation-" + inventory.Presentations.Count;
            var elementClass = Attr(mdElement, "elementClass") ?? "PresentationElement";
            var elementId = GetElementId(mdElement);
            var geometry = Descendants(mdElement, "geometry").Select(e => e.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            var bounds = ParseMagicDrawGeometry(geometry);

            if (elementClass.Equals("Note", StringComparison.OrdinalIgnoreCase))
            {
                var text = Descendants(mdElement, "text").Select(e => e.Value).FirstOrDefault() ?? string.Empty;
                var noteId = Attr(mdElement, "id") ?? presentationId;
                if (bounds is not null)
                    inventory.Notes.Add(new(noteId, diagramId, text, elementId, bounds, entry.FullName));
                elementId ??= noteId;
            }

            if (string.IsNullOrWhiteSpace(elementId) && !elementClass.Equals("Split", StringComparison.OrdinalIgnoreCase))
                continue;

            var route = IsConnectorClass(elementClass) ? ParseRoute(geometry) : [];
            inventory.Presentations.Add(new(
                presentationId,
                diagramId,
                elementId ?? presentationId,
                elementClass,
                bounds,
                route,
                entry.FullName));

            if (elementClass.Equals("ControlFlow", StringComparison.OrdinalIgnoreCase))
                ParseGuard(mdElement, diagram, diagramId, entry.FullName, inventory);
        }
    }

    private static void ParseGuard(XElement flow, XDocument diagram, string diagramId, string documentName, MdzipInventory inventory)
    {
        var text = Descendants(flow, "text").Select(e => e.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (string.IsNullOrWhiteSpace(text))
            return;

        var firstEnd = FirstReference(flow, "linkFirstEndID");
        var secondEnd = FirstReference(flow, "linkSecondEndID");
        var source = ResolvePresentationElementId(diagram, secondEnd);
        var target = ResolvePresentationElementId(diagram, firstEnd);
        inventory.Guards.Add(new(diagramId, text, source, target, documentName));
    }

    private static string? ResolvePresentationElementId(XDocument diagram, string? presentationId)
    {
        if (presentationId is null)
            return null;
        var mdElement = diagram.Descendants().FirstOrDefault(e =>
            Local(e) == "mdElement" && Attr(e, "id") == presentationId);
        return mdElement is null ? null : GetElementId(mdElement);
    }

    private static string? GetElementId(XElement mdElement)
    {
        var elementIdNode = Descendants(mdElement, "elementID").FirstOrDefault();
        return elementIdNode is null
            ? null
            : NormalizeReference(Attr(elementIdNode, "href") ?? Attr(elementIdNode, "idref") ?? Attr(elementIdNode, "id") ?? elementIdNode.Value);
    }

    private static LayoutBounds? ParseMagicDrawGeometry(string? geometry)
    {
        var values = ParseIntegers(geometry).Take(4).ToArray();
        if (values.Length < 4)
            return null;

        var x = values[0];
        var y = values[1];
        var right = values[2];
        var bottom = values[3];
        return new LayoutBounds(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    private static IReadOnlyList<LayoutPoint> ParseRoute(string? geometry)
    {
        var values = ParseIntegers(geometry).ToArray();
        if (values.Length < 4 || values.Length % 2 != 0)
            return [];
        var points = new List<LayoutPoint>();
        for (var i = 0; i < values.Length; i += 2)
            points.Add(new(values[i], values[i + 1]));
        return points;
    }

    private static IEnumerable<int> ParseIntegers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;
        foreach (var token in value.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                yield return number;
    }

    private static bool IsConnectorClass(string value) =>
        value.Contains("Flow", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Connector", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Association", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Dependency", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Transition", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Link", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<XElement> Descendants(XElement root, string localName) =>
        root.Descendants().Where(e => Local(e).Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static string Local(XElement element) => element.Name.LocalName;

    private static string? Attr(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string NormalizeType(string value)
    {
        var separator = value.IndexOf(':');
        return separator >= 0 && separator + 1 < value.Length ? value[(separator + 1)..] : value;
    }

    private static string? FirstReference(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var direct = References(Attr(element, name)).FirstOrDefault();
            if (direct is not null)
                return direct;

            var child = element.Elements().FirstOrDefault(e => Local(e).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (child is null)
                continue;
            var childReference = NormalizeReference(Attr(child, "href") ?? Attr(child, "idref") ?? Attr(child, "id") ?? child.Value);
            if (!string.IsNullOrWhiteSpace(childReference))
                return childReference;
        }
        return null;
    }

    private static IEnumerable<string> References(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;
        foreach (var token in value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeReference(token);
            if (!string.IsNullOrWhiteSpace(normalized))
                yield return normalized;
        }
    }

    private static string? NormalizeReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var hash = value.LastIndexOf('#');
        return hash >= 0 && hash + 1 < value.Length ? value[(hash + 1)..] : value;
    }
}
