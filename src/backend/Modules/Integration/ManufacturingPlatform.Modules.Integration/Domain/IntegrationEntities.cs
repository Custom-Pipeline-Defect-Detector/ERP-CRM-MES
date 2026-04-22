namespace ManufacturingPlatform.Modules.Integration.Domain;

public sealed record ExternalSystem(Guid Id, string SystemCode, string Name);

public sealed record SyncContractDefinition(
    Guid Id,
    string EntityName,
    string SourceSystem,
    string TargetSystem,
    string ConflictRule);

public sealed record SyncJob(Guid Id, string JobNumber, string Direction);

public sealed record SyncMapping(Guid Id, string EntityName, string ExternalField);

public sealed record SyncFailure(Guid Id, string JobNumber, string ErrorCode);

public sealed record ReconciliationRule(Guid Id, string RuleCode, string EntityName, string Frequency);

public sealed record ReconciliationSnapshot(Guid Id, string SnapshotCode, DateTimeOffset CapturedAt);
