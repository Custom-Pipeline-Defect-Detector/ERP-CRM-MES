namespace ManufacturingPlatform.Modules.Reporting.Domain;

public sealed record DatasetDefinition(Guid Id, string DatasetCode, string Name, string OwnerModule);

public sealed record SemanticMetricDefinition(Guid Id, string MetricCode, string Name, string DatasetCode);

public sealed record DrillDownDefinition(Guid Id, string DrillDownCode, string MetricCode, string TargetObjectType);

public sealed record DashboardDefinition(Guid Id, string Code, string Name);

public sealed record ReportDefinition(Guid Id, string Code, string Name);

public sealed record ReportSnapshot(Guid Id, string ReportCode, DateTimeOffset GeneratedAt);

public sealed record ExportJob(Guid Id, string JobNumber, string Format);
