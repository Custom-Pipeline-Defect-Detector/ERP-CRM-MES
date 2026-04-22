namespace ManufacturingPlatform.Modules.Reporting.Contracts;

public sealed record ReportingModuleOverview(
    string FocusArea,
    IReadOnlyList<string> PlaceholderEntities,
    IReadOnlyList<string> PlannedCapabilities,
    IReadOnlyList<string> InfrastructureNotes);
