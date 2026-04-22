namespace ManufacturingPlatform.Modules.Platform.Contracts;

public sealed record PlatformModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> InfrastructureNotes);
