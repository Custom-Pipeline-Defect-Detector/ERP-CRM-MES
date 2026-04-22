using ManufacturingPlatform.BuildingBlocks.Abstractions;

namespace ManufacturingPlatform.Modules.CRM.Contracts;

public sealed record CrmModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> IntegrationTouchpoints,
    IReadOnlyList<DataOwnershipDefinition> OwnershipRules,
    IReadOnlyList<WorkflowDefinition> WorkflowDefinitions);
