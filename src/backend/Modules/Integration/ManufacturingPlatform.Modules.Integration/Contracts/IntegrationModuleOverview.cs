namespace ManufacturingPlatform.Modules.Integration.Contracts;

public sealed record IntegrationModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> InfrastructureNotes);
