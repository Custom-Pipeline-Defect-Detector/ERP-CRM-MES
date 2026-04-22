namespace ManufacturingPlatform.BuildingBlocks.Abstractions;

public sealed record DataOwnershipDefinition(
    string EntityName,
    string SystemOwner,
    IReadOnlyList<string> ConsumingModules,
    string Rule);

public sealed record StateTransitionDefinition(
    string FromState,
    string ToState,
    string Trigger,
    string Owner);

public sealed record WorkflowDefinition(
    string ObjectName,
    string Owner,
    IReadOnlyList<string> States,
    IReadOnlyList<StateTransitionDefinition> Transitions);

public sealed record FinancialImpactDefinition(
    string TriggeringEvent,
    string FinancialImpact,
    string AccountingTarget,
    string Rule);
