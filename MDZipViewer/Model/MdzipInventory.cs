using System.Text.Json.Serialization;

namespace MDZipViewer.Model;

public sealed class MdzipInventory
{
    public required string SourceFile { get; init; }
    public required DateTime ScannedAtUtc { get; init; }
    public List<string> ArchiveEntries { get; } = [];
    public List<ModelDocument> Documents { get; } = [];
    public List<ModelElement> Elements { get; } = [];
    public List<ModelDiagram> Diagrams { get; } = [];
    public List<ModelRelationship> Relationships { get; } = [];

    [JsonIgnore]
    public int PackageCount => Elements.Count(e => e.Type.EndsWith("Package", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public int RelationshipCount => Relationships.Count;
}

public sealed record ModelDocument(string EntryName, string RootName, string? ExporterVersion);

public sealed record ModelElement(
    string Id,
    string Name,
    string Type,
    string Document,
    string? OwnerId,
    string? HumanType);

public sealed record ModelDiagram(
    string Id,
    string Name,
    string Type,
    string Document,
    string? OwnerId);

public sealed record ModelRelationship(
    string Id,
    string Name,
    string Type,
    string Document,
    string? OwnerId,
    string? SourceId,
    string? TargetId);

internal static class MdzipTypeClassifier
{
    private static readonly string[] RelationshipTokens =
    [
        "Association", "Dependency", "Generalization", "Connector", "Transition",
        "InformationFlow", "ControlFlow", "ObjectFlow", "Realization", "Usage",
        "Satisfy", "Verify", "Refine", "Allocate", "Trace"
    ];

    public static bool IsRelationship(string type) =>
        RelationshipTokens.Any(token => type.Contains(token, StringComparison.OrdinalIgnoreCase));
}
