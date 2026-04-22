namespace ManufacturingPlatform.BuildingBlocks.Abstractions;

public sealed record ModuleDescriptor(
    string Key,
    string DisplayName,
    string RoutePrefix,
    string Description,
    string DeliveryPhase,
    IReadOnlyList<string> Capabilities);
