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
    public List<DiagramPresentation> Presentations { get; } = [];
    public List<DiagramNote> Notes { get; } = [];
    public List<ModelConstraint> Constraints { get; } = [];
    public List<ModelLifeline> Lifelines { get; } = [];
    public List<ModelMessage> Messages { get; } = [];
    public List<ModelGuard> Guards { get; } = [];
    public List<ParseDiagnostic> Diagnostics { get; } = [];

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
    string? OwnerId,
    string? StreamContentId);

public sealed record ModelRelationship(
    string Id,
    string Name,
    string Type,
    string Document,
    string? OwnerId,
    string? SourceId,
    string? TargetId);

public sealed record LayoutPoint(double X, double Y);

public sealed record LayoutBounds(double X, double Y, double Width, double Height);

public sealed record DiagramPresentation(
    string PresentationId,
    string DiagramId,
    string ModelElementId,
    string PresentationType,
    LayoutBounds? Bounds,
    IReadOnlyList<LayoutPoint> RoutePoints,
    string Document);

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
