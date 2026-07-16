using System.IO.Compression;
using System.Xml.Linq;
using MDZipViewer.Model;

namespace MDZipViewer.Services;

public sealed class MdzipReader
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

            var exporterVersion = root
                .Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Documentation", StringComparison.OrdinalIgnoreCase))?
                .Attributes()
                .FirstOrDefault(a => a.Name.LocalName.Equals("exporterVersion", StringComparison.OrdinalIgnoreCase))?
                .Value;

            inventory.Documents.Add(new ModelDocument(entry.FullName, root.Name.LocalName, exporterVersion));

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
                    inventory.Diagrams.Add(new ModelDiagram(id, name, normalizedType, entry.FullName, ownerId));

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

                    inventory.Relationships.Add(new ModelRelationship(
                        id, name, normalizedType, entry.FullName, ownerId, sourceId, targetId));
                }
            }
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
        {
            // Some archives contain auxiliary XML that is not model content.
            // Ignore those entries and continue with the primary model documents.
        }
    }

    private static string? FindReference(XElement element, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            var value = AttributeValue(element, name);
            var reference = SplitReferences(value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(reference))
                return reference;
        }

        foreach (var child in element.Elements())
        {
            if (!names.Any(name => child.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var reference = SplitReferences(
                AttributeValue(child, "idref") ?? AttributeValue(child, "href") ?? child.Value)
                .FirstOrDefault();
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
            var normalized = token;
            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex >= 0 && hashIndex + 1 < normalized.Length)
                normalized = normalized[(hashIndex + 1)..];
            yield return normalized;
        }
    }

    private static string? AttributeValue(XElement? element, string localName) =>
        element?.Attributes()
            .FirstOrDefault(a => a.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?
            .Value;

    private static string NormalizeType(string value)
    {
        var index = value.IndexOf(':');
        return index >= 0 && index + 1 < value.Length ? value[(index + 1)..] : value;
    }

    private static bool LooksLikeDiagram(XElement element, string type, string? humanType)
    {
        return type.Contains("Diagram", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Contains("Diagram", StringComparison.OrdinalIgnoreCase)
            || (humanType?.Contains("Diagram", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
