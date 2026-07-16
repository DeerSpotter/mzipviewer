using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
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
            ReadModelDocument(archive, entry, inventory);

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

    private static void ReadModelDocument(ZipArchive archive, ZipArchiveEntry entry, MdzipInventory inventory)
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
            ParseSemanticElements(root, entry.FullName, inventory);
            ParseOwnedDiagrams(archive, root, entry.FullName, inventory);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            // Auxiliary XML entries are allowed inside MDZIP archives.
        }
    }

    private static void ParseSemanticElements(XElement root, string documentName, MdzipInventory inventory)
    {
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

            if (!inventory.Elements.Any(e => e.Id.Equals(id, StringComparison.Ordinal)))
                inventory.Elements.Add(new ModelElement(id, name, normalizedType, documentName, ownerId, humanType));

            if (!MdzipTypeClassifier.IsRelationship(normalizedType))
                continue;

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

            if (!inventory.Relationships.Any(r => r.Id.Equals(id, StringComparison.Ordinal)))
                inventory.Relationships.Add(new ModelRelationship(id, name, normalizedType, documentName, ownerId, sourceId, targetId));
        }
    }

    private static void ParseOwnedDiagrams(ZipArchive archive, XElement root, string documentName, MdzipInventory inventory)
    {
        foreach (var diagramNode in root.Descendants().Where(e => e.Name.LocalName.Equals("ownedDiagram", StringComparison.OrdinalIgnoreCase)))
        {
            var name = AttributeValue(diagramNode, "name") ?? "(unnamed diagram)";
            var ownerId = AttributeValue(diagramNode, "ownerOfDiagram") ?? AttributeValue(diagramNode.Parent, "id");
            var diagramId = !string.IsNullOrWhiteSpace(ownerId)
                ? ownerId + '\u0003' + name
                : AttributeValue(diagramNode, "id") ?? $"diagram-{inventory.Diagrams.Count + 1}";

            var binaryObject = diagramNode.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("binaryObject", StringComparison.OrdinalIgnoreCase));
            var streamContentId = AttributeValue(binaryObject, "streamContentID");
            var type = NormalizeType(AttributeValue(diagramNode, "type") ?? "Diagram");

            var diagram = new ModelDiagram(diagramId, name, type, documentName, ownerId, streamContentId);
            if (!inventory.Diagrams.Any(d => d.Id.Equals(diagramId, StringComparison.Ordinal)))
                inventory.Diagrams.Add(diagram);

            if (!string.IsNullOrWhiteSpace(streamContentId))
                ParseBinaryDiagramEntry(archive, diagram, streamContentId, inventory);
        }
    }

    private static void ParseBinaryDiagramEntry(ZipArchive archive, ModelDiagram diagram, string streamContentId, MdzipInventory inventory)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals(streamContentId, StringComparison.OrdinalIgnoreCase)
            || e.FullName.EndsWith('/' + streamContentId, StringComparison.OrdinalIgnoreCase));

        if (entry is null || entry.Length == 0)
            return;

        try
        {
            using var stream = entry.Open();
            using var reader = CreateMagicDrawXmlReader(stream);
            var document = XDocument.Load(reader, LoadOptions.None);

            foreach (var mdElement in document.Descendants().Where(e => e.Name.LocalName.Equals("mdElement", StringComparison.OrdinalIgnoreCase)))
                ParseMdElement(mdElement, diagram, entry.FullName, inventory);
        }
        catch (XmlException)
        {
            // The referenced payload is not an XML diagram stream understood by this reader.
        }
    }

    private static XmlReader CreateMagicDrawXmlReader(Stream stream)
    {
        var nameTable = new NameTable();
        var namespaceManager = new XmlNamespaceManager(nameTable);
        namespaceManager.AddNamespace("xmi", "http://www.omg.org/spec/XMI/20131001");
        var context = new XmlParserContext(nameTable, namespaceManager, string.Empty, XmlSpace.Default);
        var settings = new XmlReaderSettings { NameTable = nameTable, DtdProcessing = DtdProcessing.Prohibit };
        return XmlReader.Create(stream, settings, context);
    }

    private static void ParseMdElement(XElement mdElement, ModelDiagram diagram, string binaryDocument, MdzipInventory inventory)
    {
        var presentationId = AttributeValue(mdElement, "id") ?? $"presentation-{inventory.Presentations.Count + 1}";
        var elementClass = AttributeValue(mdElement, "elementClass") ?? "PresentationElement";
        var elementIdNode = mdElement.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("elementID", StringComparison.OrdinalIgnoreCase));
        var modelElementId = ReadReferencedId(elementIdNode);

        var geometryNode = mdElement.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("geometry", StringComparison.OrdinalIgnoreCase));
        var geometry = geometryNode?.Value;
        var bounds = ParseMagicDrawGeometry(geometry);
        var route = IsLinkElement(elementClass) ? ParseMagicDrawRoute(geometry) : [];

        if (elementClass.Equals("Note", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(modelElementId))
        {
            modelElementId = presentationId;
            var noteText = mdElement.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("text", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!inventory.Elements.Any(e => e.Id.Equals(modelElementId, StringComparison.Ordinal)))
                inventory.Elements.Add(new ModelElement(modelElementId, string.IsNullOrWhiteSpace(noteText) ? "Note" : noteText, "Note", binaryDocument, diagram.OwnerId, "Note"));
        }

        if (elementClass.Equals("Split", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(modelElementId))
        {
            modelElementId = presentationId;
            if (!inventory.Elements.Any(e => e.Id.Equals(modelElementId, StringComparison.Ordinal)))
                inventory.Elements.Add(new ModelElement(modelElementId, "Split", "Split", binaryDocument, diagram.OwnerId, "Split"));
        }

        if (string.IsNullOrWhiteSpace(modelElementId) || (bounds is null && route.Count < 2))
            return;

        inventory.Presentations.Add(new DiagramPresentation(
            presentationId,
            diagram.Id,
            modelElementId,
            elementClass,
            bounds,
            route,
            binaryDocument));
    }

    private static LayoutBounds? ParseMagicDrawGeometry(string? geometry)
    {
        var values = ParseNumbers((geometry ?? string.Empty).Replace(';', ',')).Take(4).ToArray();
        if (values.Length < 4)
            return null;

        var x = values[0];
        var y = values[1];
        var right = values[2];
        var bottom = values[3];
        var width = right - x;
        var height = bottom - y;

        return width > 0 && height > 0 ? new LayoutBounds(x, y, width, height) : null;
    }

    private static IReadOnlyList<LayoutPoint> ParseMagicDrawRoute(string? geometry)
    {
        if (string.IsNullOrWhiteSpace(geometry))
            return [];

        var points = new List<LayoutPoint>();
        foreach (var segment in geometry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = ParseNumbers(segment).Take(2).ToArray();
            if (values.Length == 2)
                points.Add(new LayoutPoint(values[0], values[1]));
        }

        if (points.Count >= 2)
            return points;

        var flat = ParseNumbers(geometry).ToArray();
        if (flat.Length >= 4 && flat.Length % 2 == 0)
        {
            points.Clear();
            for (var i = 0; i < flat.Length; i += 2)
                points.Add(new LayoutPoint(flat[i], flat[i + 1]));
        }

        return points;
    }

    private static bool IsLinkElement(string elementClass) =>
        elementClass.Contains("Flow", StringComparison.OrdinalIgnoreCase)
        || elementClass.Contains("Link", StringComparison.OrdinalIgnoreCase)
        || elementClass.Contains("Connector", StringComparison.OrdinalIgnoreCase)
        || elementClass.Contains("Association", StringComparison.OrdinalIgnoreCase)
        || elementClass.Contains("Dependency", StringComparison.OrdinalIgnoreCase)
        || elementClass.Contains("Transition", StringComparison.OrdinalIgnoreCase);

    private static string? ReadReferencedId(XElement? node)
    {
        if (node is null)
            return null;

        return NormalizeReference(AttributeValue(node, "href"))
            ?? NormalizeReference(AttributeValue(node, "idref"))
            ?? NormalizeReference(AttributeValue(node, "id"));
    }

    private static string? NormalizeReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var hashIndex = value.LastIndexOf('#');
        return hashIndex >= 0 && hashIndex + 1 < value.Length ? value[(hashIndex + 1)..] : value;
    }

    private static string? FindReference(XElement element, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var reference = SplitReferences(AttributeValue(element, name)).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(reference))
                return reference;
        }

        foreach (var child in element.Elements())
        {
            if (!names.Any(name => child.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            var reference = SplitReferences(AttributeValue(child, "idref") ?? AttributeValue(child, "href") ?? child.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(reference))
                return reference;
        }

        return null;
    }

    private static IEnumerable<string> SplitReferences(string? value)
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

    private static IEnumerable<double> ParseNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;
        foreach (Match match in NumberRegex().Matches(value))
            if (double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                yield return number;
    }

    private static string? AttributeValue(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string NormalizeType(string value)
    {
        var index = value.IndexOf(':');
        return index >= 0 && index + 1 < value.Length ? value[(index + 1)..] : value;
    }

    [GeneratedRegex(@"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberRegex();
}
