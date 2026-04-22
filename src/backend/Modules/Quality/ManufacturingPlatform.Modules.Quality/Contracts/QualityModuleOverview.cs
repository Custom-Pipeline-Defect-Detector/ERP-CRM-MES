using ManufacturingPlatform.BuildingBlocks.Abstractions;

namespace ManufacturingPlatform.Modules.Quality.Contracts;

public sealed record QualityModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> IntegrationTouchpoints,
    IReadOnlyList<DataOwnershipDefinition> OwnershipRules,
    IReadOnlyList<WorkflowDefinition> WorkflowDefinitions);
