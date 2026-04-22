using ManufacturingPlatform.BuildingBlocks.Abstractions;

namespace ManufacturingPlatform.Modules.MES.Contracts;

public sealed record MesModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> IntegrationTouchpoints,
    IReadOnlyList<DataOwnershipDefinition> OwnershipRules,
    IReadOnlyList<WorkflowDefinition> WorkflowDefinitions,
    IReadOnlyList<FinancialImpactDefinition> FinancialImpacts);
