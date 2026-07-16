namespace MDZipViewer.Model;

public sealed record DiagramNote(
    string Id,
    string DiagramId,
    string Text,
    string? LinkedElementId,
    LayoutBounds Bounds,
    string Document);

public sealed record ModelConstraint(
    string OwnerId,
    string Name,
    string Body,
    string Language,
    string Document);

public sealed record ModelLifeline(
    string Id,
    string? RepresentsPropertyId,
    string? RepresentedTypeId,
    string Document);

public sealed record ModelMessage(
    string Id,
    string Name,
    string? SourceLifelineId,
    string? TargetLifelineId,
    string? SendEventId,
    string? ReceiveEventId,
    string Document);

public sealed record ModelGuard(
    string DiagramId,
    string Text,
    string? SourceElementId,
    string? TargetElementId,
    string Document);

public sealed record ParseDiagnostic(
    string Severity,
    string Area,
    string Message,
    string? Document = null);
