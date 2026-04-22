using ManufacturingPlatform.BuildingBlocks.Abstractions;

namespace ManufacturingPlatform.Modules.ERP.Contracts;

public sealed record ErpModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> IntegrationTouchpoints,
    IReadOnlyList<DataOwnershipDefinition> OwnershipRules,
    IReadOnlyList<WorkflowDefinition> WorkflowDefinitions,
    IReadOnlyList<FinancialImpactDefinition> FinancialImpacts);
