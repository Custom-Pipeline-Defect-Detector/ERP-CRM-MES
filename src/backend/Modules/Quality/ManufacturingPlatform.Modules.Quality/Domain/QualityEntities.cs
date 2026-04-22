namespace ManufacturingPlatform.Modules.Quality.Domain;

public enum InspectionType
{
    Incoming,
    InProcess,
    Final
}

public enum InspectionRecordState
{
    Planned,
    SampleCollected,
    InInspection,
    Accepted,
    Rejected,
    ConditionalRelease,
    Closed
}

public enum QualityDisposition
{
    Hold,
    Release,
    ConditionalRelease,
    Rework,
    Reject,
    Scrap
}

public enum NcrState
{
    Draft,
    Open,
    InDisposition,
    Closed,
    Cancelled
}

public enum CapaState
{
    Draft,
    Open,
    InProgress,
    Verified,
    Closed,
    Cancelled
}

public sealed record QualityStandard(
    Guid Id,
    string Code,
    string Name,
    string Version);

public sealed record InspectionPlan(
    Guid Id,
    string PlanNumber,
    InspectionType InspectionType,
    string TriggerObjectType,
    string SamplingRule);

public sealed record InspectionLot(
    Guid Id,
    string LotNumber,
    InspectionType InspectionType,
    string SourceDocumentNumber,
    string ItemCode,
    string? BatchNumber,
    decimal Quantity);

public sealed record InspectionRecord(
    Guid Id,
    string RecordNumber,
    Guid InspectionLotId,
    InspectionRecordState State,
    QualityDisposition? Disposition,
    DateTimeOffset? CompletedAt);

public sealed record QualityStatus(
    Guid Id,
    string StatusCode,
    string Name,
    bool IsAvailableForUse);

public sealed record HoldReleaseRecord(
    Guid Id,
    Guid InspectionLotId,
    string? BatchNumber,
    QualityDisposition Disposition,
    DateTimeOffset ProcessedAt,
    string Reason);

public sealed record DefectRecord(
    Guid Id,
    string DefectCode,
    string Category,
    decimal Quantity,
    string Severity);

public sealed record NonconformanceDisposition(
    Guid Id,
    string DispositionNumber,
    Guid DefectRecordId,
    QualityDisposition Disposition,
    string ActionSummary);

public sealed record Ncr(
    Guid Id,
    string NcrNumber,
    NcrState State,
    string RootCause);

public sealed record Capa(
    Guid Id,
    string CapaNumber,
    CapaState State,
    string ActionOwner);
