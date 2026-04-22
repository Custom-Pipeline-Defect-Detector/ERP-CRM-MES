namespace ManufacturingPlatform.Modules.MES.Domain;

public enum WorkOrderState
{
    Created,
    Dispatched,
    Started,
    Paused,
    Completed,
    Reported,
    Closed,
    Cancelled
}

public enum OperationExecutionState
{
    Created,
    Ready,
    Dispatched,
    Started,
    Paused,
    Completed,
    Closed
}

public enum WipLotStatus
{
    Created,
    InProcess,
    OnHold,
    Released,
    Completed,
    Scrapped
}

public enum IssueMode
{
    ManualIssue,
    Backflush
}

public enum DowntimeCategory
{
    Planned,
    Unplanned,
    Changeover,
    QualityHold,
    MaterialShortage,
    Maintenance
}

public sealed record ProcessRoute(
    Guid Id,
    string RouteCode,
    string Name,
    string Version,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ProcessStep(
    Guid Id,
    Guid RouteId,
    string StepCode,
    string StepName,
    int Sequence,
    bool IsMachineDriven,
    bool AllowsReworkLoop);

public sealed record WorkCenter(
    Guid Id,
    string Code,
    string Name,
    bool IsBottleneck);

public sealed record Machine(
    Guid Id,
    string Code,
    string Name,
    string MachineType,
    bool SupportsAutomaticCollection);

public sealed record ProductionOrderRelease(
    Guid Id,
    Guid ProductionOrderId,
    string ReleaseNumber,
    DateTimeOffset ReleasedAt,
    Guid ReleasedByUserId);

public sealed record WorkOrder(
    Guid Id,
    string WorkOrderNumber,
    Guid ProductionOrderId,
    WorkOrderState State,
    string ProductCode,
    decimal PlannedQuantity);

public sealed record WorkOrderOperation(
    Guid Id,
    Guid WorkOrderId,
    string OperationCode,
    string OperationName,
    int Sequence,
    OperationExecutionState State,
    Guid WorkCenterId,
    Guid? MachineId,
    bool IsMachineDriven,
    IssueMode IssueMode);

public sealed record OperationDispatch(
    Guid Id,
    Guid WorkOrderOperationId,
    string DispatchNumber,
    Guid ShiftId,
    Guid? PrimaryOperatorUserId,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset? PlannedEndAt);

public sealed record OperatorAssignment(
    Guid Id,
    Guid WorkOrderOperationId,
    Guid OperatorUserId,
    Guid ShiftId,
    DateTimeOffset AssignedAt);

public sealed record WipLot(
    Guid Id,
    string LotNumber,
    Guid WorkOrderId,
    Guid CurrentOperationId,
    WipLotStatus Status,
    decimal Quantity,
    string? ParentLotNumber);

public sealed record WipMove(
    Guid Id,
    string MoveNumber,
    Guid WipLotId,
    Guid FromOperationId,
    Guid ToOperationId,
    decimal Quantity,
    DateTimeOffset MovedAt);

public sealed record MaterialIssue(
    Guid Id,
    string IssueNumber,
    Guid WorkOrderOperationId,
    IssueMode IssueMode,
    string Status,
    DateTimeOffset IssuedAt);

public sealed record MaterialConsumptionActual(
    Guid Id,
    Guid WorkOrderOperationId,
    string MaterialCode,
    string? BatchNumber,
    decimal Quantity,
    DateTimeOffset ConsumedAt);

public sealed record LaborTimeEntry(
    Guid Id,
    Guid WorkOrderOperationId,
    Guid OperatorUserId,
    Guid ShiftId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    decimal ReportedHours);

public sealed record MachineTimeEntry(
    Guid Id,
    Guid WorkOrderOperationId,
    Guid MachineId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    decimal ReportedHours);

public sealed record ProductionReport(
    Guid Id,
    string ReportNumber,
    Guid WorkOrderOperationId,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    decimal ReworkQuantity,
    DateTimeOffset ReportedAt);

public sealed record ScrapRecord(
    Guid Id,
    string ScrapNumber,
    Guid WorkOrderOperationId,
    string ReasonCode,
    decimal Quantity,
    string Disposition);

public sealed record ReworkOrder(
    Guid Id,
    string ReworkNumber,
    Guid SourceWorkOrderOperationId,
    Guid TargetProcessStepId,
    string Status);

public sealed record BatchGenealogyLink(
    Guid Id,
    string ParentLotNumber,
    string ChildLotNumber,
    string RelationType);

public sealed record EquipmentStatusLog(
    Guid Id,
    Guid MachineId,
    string StatusCode,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

public sealed record DowntimeRecord(
    Guid Id,
    Guid WorkOrderOperationId,
    Guid? MachineId,
    string ReasonCode,
    DowntimeCategory Category,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);

public sealed record OeeSnapshot(
    Guid Id,
    Guid WorkCenterId,
    Guid ShiftId,
    DateOnly BusinessDate,
    decimal AvailabilityPercent,
    decimal PerformancePercent,
    decimal QualityPercent);
