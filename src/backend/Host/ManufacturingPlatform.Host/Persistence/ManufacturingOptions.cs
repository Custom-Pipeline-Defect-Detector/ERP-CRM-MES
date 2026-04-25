namespace ManufacturingPlatform.Host.Persistence;

public sealed class ManufacturingOptions
{
    public DatabaseOptions Database { get; init; } = new();
    public AiOptions Ai { get; init; } = new();
    public CostingOptions Costing { get; init; } = new();
    public IntegrationOptions Integration { get; init; } = new();
}

public sealed class DatabaseOptions
{
    public string Provider { get; init; } = "Sqlite";
    public string ConnectionString { get; init; } = "Data Source=../../../../infra/data/manufacturing-platform.db";
}

public sealed class AiOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "claude-opus-4-6";
    public int TimeoutSeconds { get; init; } = 20;
}

public sealed class CostingOptions
{
    public decimal LaborRatePerHour { get; init; } = 36m;
    public decimal MachineRatePerHour { get; init; } = 52m;
    public decimal DefaultOverheadRatePerUnit { get; init; } = 14m;
}

public sealed class IntegrationOptions
{
    public int TimeoutSeconds { get; init; } = 20;
}
