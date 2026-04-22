namespace ManufacturingPlatform.Modules.AI.Domain;

public sealed record InsightPrompt(Guid Id, string PromptKey, string Purpose);

public sealed record InsightResult(Guid Id, string InsightType, DateTimeOffset GeneratedAt);

public sealed record InsightRun(Guid Id, string RunNumber, string DatasetCode, DateTimeOffset StartedAt);

public sealed record ForecastScenario(Guid Id, string ScenarioCode, string MetricName);

public sealed record AnomalyRule(Guid Id, string RuleCode, string MetricCode, string RuleType);

public sealed record AnomalyAlert(Guid Id, string AlertCode, string Severity);
