namespace ManufacturingPlatform.Modules.AI.Contracts;

public sealed record AiModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> InfrastructureNotes);
