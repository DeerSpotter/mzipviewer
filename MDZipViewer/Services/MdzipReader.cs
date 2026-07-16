using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

public sealed partial class MdzipReader
{
    private static readonly string[] PreferredModelNames =
    [
        "com.nomagic.magicdraw.uml_model.model",
        "com.nomagic.magicdraw.uml_model.shared_model"
    ];

    private static readonly string[] SourceAttributeNames =
    [
        "source", "client", "specific", "informationSource", "end1", "from", "supplierDependency"
    ];

    private static readonly string[] TargetAttributeNames =
    [
        "target", "supplier", "general", "informationTarget", "end2", "to", "clientDependency"
    ];

    private static readonly string[] ModelReferenceNames =
    [
        "elementID", "elementId", "modelElement", "representedElement", "subject", "element", "modelElementID"
    ];

    private static readonly string[] DiagramReferenceNames =
    [
        "diagram", "diagramID", "diagramId", "parentDiagram", "diagramElement"
    ];

    public MdzipInventory Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A file path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The MDZIP file was not found.", filePath);
        if (!filePath.EndsWith(".mdzip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file must have the .mdzip extension.");

        var inventory = new MdzipInventory
        {
            SourceFile = Path.GetFullPath(filePath),
            ScannedAtUtc = DateTime.UtcNow
        };

        using var archive = ZipFile.OpenRead(filePath);
        inventory.ArchiveEntries.AddRange(archive.Entries.Select(e => e.FullName));

        var candidates = archive.Entries
            .Where(IsCandidateModelEntry)
            .OrderByDescending(e => PreferredModelNames.Any(name => e.FullName.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidDataException("No MagicDraw model XML was found inside the archive.");

        foreach (var entry in candidates)
            ReadDocument(entry, inventory);

        return inventory;
    }

    private static bool IsCandidateModelEntry(ZipArchiveEntry entry)
    {
        if (entry.Length == 0)
            return false;

        return PreferredModelNames.Any(name => entry.FullName.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            || entry.FullName.EndsWith(".mdxml", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static void ReadDocument(ZipArchiveEntry entry, MdzipInventory inventory)
    {
        try
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream, LoadOptions.None);
            var root = document.Root;
            if (root is null)
                return;

            var exporterVersion = root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Documentation", StringComparison.OrdinalIgnoreCase))?
                .Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("exporterVersion", StringComparison.OrdinalIgnoreCase))?.Value;

            inventory.Documents.Add(new ModelDocument(entry.FullName, root.Name.LocalName, exporterVersion));
            var documentDiagrams = new List<(XElement Xml, ModelDiagram Diagram)>();

            foreach (var element in root.DescendantsAndSelf())
            {
                var id = AttributeValue(element, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var type = AttributeValue(element, "type") ?? element.Name.LocalName;
                var name = AttributeValue(element, "name") ?? "(unnamed)";
                var ownerId = AttributeValue(element.Parent, "id");
                var humanType = AttributeValue(element, "humanType");
                var normalizedType = NormalizeType(type);

                inventory.Elements.Add(new ModelElement(id, name, normalizedType, entry.FullName, ownerId, humanType));

                if (LooksLikeDiagram(element, normalizedType, humanType))
                {
                    var diagram = new ModelDiagram(id, name, normalizedType, entry.FullName, ownerId);
                    inventory.Diagrams.Add(diagram);
                    documentDiagrams.Add((element, diagram));
                }

                if (MdzipTypeClassifier.IsRelationship(normalizedType))
                {
                    var sourceId = FindReference(element, SourceAttributeNames);
                    var targetId = FindReference(element, TargetAttributeNames);
                    if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
                    {
                        var memberEnds = SplitReferences(AttributeValue(element, "memberEnd")).Take(2).ToArray();
                        if (memberEnds.Length == 2)
                        {
                            sourceId ??= memberEnds[0];
                            targetId ??= memberEnds[1];
                        }
                    }
                    inventory.Relationships.Add(new ModelRelationship(id, name, normalizedType, entry.FullName, ownerId, sourceId, targetId));
                }
            }

            ParsePresentations(root, entry.FullName, documentDiagrams, inventory);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
        {
        }
    }

    private static void ParsePresentations(XElement root, string documentName, IReadOnlyList<(XElement Xml, ModelDiagram Diagram)> diagrams, MdzipInventory inventory)
    {
        var diagramIds = diagrams.Select(d => d.Diagram.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var element in root.DescendantsAndSelf())
        {
            var bounds = ReadBounds(element);
            var route = ReadRoute(element);
            if (bounds is null && route.Count < 2)
                continue;

            var modelId = FindReference(element, ModelReferenceNames)
                ?? FindReference(element, ["idref", "href"]);
            if (string.IsNullOrWhiteSpace(modelId))
                continue;

            var diagramId = FindReference(element, DiagramReferenceNames)
                ?? element.AncestorsAndSelf()
                    .Select(a => AttributeValue(a, "id"))
                    .FirstOrDefault(id => id is not null && diagramIds.Contains(id));

            if (string.IsNullOrWhiteSpace(diagramId))
                continue;

            var presentationId = AttributeValue(element, "id") ?? $"presentation-{inventory.Presentations.Count + 1}";
            var presentationType = NormalizeType(AttributeValue(element, "type") ?? element.Name.LocalName);
            inventory.Presentations.Add(new DiagramPresentation(
                presentationId, diagramId, modelId, presentationType, bounds, route, documentName));
        }
    }

    private static LayoutBounds? ReadBounds(XElement element)
    {
        if (TryDouble(element, "x", out var x) && TryDouble(element, "y", out var y) &&
            (TryDouble(element, "width", out var width) || TryDouble(element, "w", out width)) &&
            (TryDouble(element, "height", out var height) || TryDouble(element, "h", out height)) &&
            width > 0 && height > 0)
            return new LayoutBounds(x, y, width, height);

        foreach (var name in new[] { "bounds", "geometry", "rectangle", "rect" })
        {
            var numbers = ParseNumbers(AttributeValue(element, name)).Take(4).ToArray();
            if (numbers.Length == 4 && numbers[2] > 0 && numbers[3] > 0)
                return new LayoutBounds(numbers[0], numbers[1], numbers[2], numbers[3]);
        }
        return null;
    }

    private static IReadOnlyList<LayoutPoint> ReadRoute(XElement element)
    {
        var type = (AttributeValue(element, "type") ?? element.Name.LocalName).ToLowerInvariant();
        var likelyEdge = type.Contains("path") || type.Contains("edge") || type.Contains("link") || type.Contains("connector");
        foreach (var name in new[] { "points", "path", "waypoints", "route", "geometry" })
        {
            var numbers = ParseNumbers(AttributeValue(element, name)).ToArray();
            if (numbers.Length >= 4 && numbers.Length % 2 == 0 && (likelyEdge || name != "geometry"))
            {
                var points = new List<LayoutPoint>();
                for (var i = 0; i < numbers.Length; i += 2)
                    points.Add(new LayoutPoint(numbers[i], numbers[i + 1]));
                return points;
            }
        }
        return [];
    }

    private static IEnumerable<double> ParseNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (Match match in NumberRegex().Matches(value))
            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                yield return number;
    }

    private static bool TryDouble(XElement element, string name, out double value) =>
        double.TryParse(AttributeValue(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string? FindReference(XElement element, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var reference = SplitReferences(AttributeValue(element, name)).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(reference)) return reference;
        }
        foreach (var child in element.Elements())
        {
            if (!names.Any(name => child.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            var reference = SplitReferences(AttributeValue(child, "idref") ?? AttributeValue(child, "href") ?? child.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(reference)) return reference;
        }
        return null;
    }

    private static IEnumerable<string> SplitReferences(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (var token in value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = token;
            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex >= 0 && hashIndex + 1 < normalized.Length) normalized = normalized[(hashIndex + 1)..];
            yield return normalized;
        }
    }

    private static string? AttributeValue(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string NormalizeType(string value)
    {
        var index = value.IndexOf(':');
        return index >= 0 && index + 1 < value.Length ? value[(index + 1)..] : value;
    }

    private static bool LooksLikeDiagram(XElement element, string type, string? humanType) =>
        type.Contains("Diagram", StringComparison.OrdinalIgnoreCase)
        || element.Name.LocalName.Contains("Diagram", StringComparison.OrdinalIgnoreCase)
        || (humanType?.Contains("Diagram", StringComparison.OrdinalIgnoreCase) ?? false);

    [GeneratedRegex(@"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberRegex();
}