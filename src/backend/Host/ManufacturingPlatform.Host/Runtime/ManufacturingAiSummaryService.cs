using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Host.Persistence;
using Microsoft.Extensions.Options;

namespace ManufacturingPlatform.Host.Runtime;

public sealed class ManufacturingAiSummaryService(IHttpClientFactory httpClientFactory, IOptions<ManufacturingOptions> options)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ManufacturingOptions _options = options.Value;

    public AiManagementSummaryDto GenerateSummary(
        DashboardMetricsDto metrics,
        IReadOnlyList<ProductionOrderListItemDto> productionOrders,
        IReadOnlyList<QualityInspectionItemDto> inspections,
        IReadOnlyList<AccountingBridgeItemDto> accountingBridges)
    {
        var warnings = new List<string>();

        if (metrics.PendingQualityInspections > 0)
        {
            warnings.Add($"{metrics.PendingQualityInspections} quality inspections are still awaiting release.");
        }

        if (accountingBridges.Count(bridge => string.Equals(bridge.State, "ReadyForExport", StringComparison.OrdinalIgnoreCase)) > 0)
        {
            warnings.Add("Accounting bridge entries are ready for export but not yet posted.");
        }

        if (productionOrders.Any(order => string.Equals(order.State, "Approved", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("At least one approved production order has not been released to MES.");
        }

        var fallbackSummary =
            $"Open sales amount is {metrics.OpenSalesAmount:0} CNY across {metrics.ActiveSalesOrders} active sales orders. " +
            $"{metrics.ActiveProductionOrders} production orders and {metrics.ActiveWorkOrders} work orders are active. " +
            $"{metrics.ReportedGoodQuantity:0.##} good units have been reported. " +
            $"{metrics.PendingQualityInspections} inspections are pending. " +
            $"{accountingBridges.Count} accounting bridge entries are currently tracked.";

        if (string.IsNullOrWhiteSpace(_options.Ai.ApiKey) || string.IsNullOrWhiteSpace(_options.Ai.BaseUrl))
        {
            return new AiManagementSummaryDto("LocalRules", "rule-based", DateTimeOffset.UtcNow, fallbackSummary, warnings);
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(ManufacturingAiSummaryService));
            client.BaseAddress = new Uri(_options.Ai.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new("Bearer", _options.Ai.ApiKey);

            var prompt =
                "You are an on-premise manufacturing management analyst. " +
                "Summarize the current factory condition using only the supplied data. " +
                "Be specific, concise, and explain any operational risks or bottlenecks.\n\n" +
                $"Metrics: salesOrders={metrics.ActiveSalesOrders}, productionOrders={metrics.ActiveProductionOrders}, " +
                $"workOrders={metrics.ActiveWorkOrders}, openSalesAmount={metrics.OpenSalesAmount}, " +
                $"reportedGoodQuantity={metrics.ReportedGoodQuantity}, pendingQualityInspections={metrics.PendingQualityInspections}, " +
                $"inventoryItemsTracked={metrics.InventoryItemsTracked}, readyBridgeEntries={metrics.ReadyAccountingBridgeEntries}.\n" +
                $"Production orders: {string.Join("; ", productionOrders.Select(order => $"{order.ProductionOrderNumber}:{order.ProductCode}:{order.State}:{order.WorkOrderNumber ?? "NoWorkOrder"}"))}.\n" +
                $"Quality inspections: {string.Join("; ", inspections.Select(inspection => $"{inspection.InspectionNumber}:{inspection.WorkOrderNumber}:{inspection.State}:{inspection.Disposition ?? "Pending"}"))}.\n" +
                $"Accounting bridges: {string.Join("; ", accountingBridges.Select(bridge => $"{bridge.BridgeNumber}:{bridge.SourceEvent}:{bridge.Amount}:{bridge.State}"))}.";

            var request = new ChatCompletionsRequest(
                _options.Ai.Model,
                [
                    new ChatMessage("system", "You generate explainable manufacturing management summaries."),
                    new ChatMessage("user", prompt)
                ]);

            var response = client.PostAsJsonAsync("v1/chat/completions", request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var payload = response.Content.ReadFromJsonAsync<ChatCompletionsResponse>().GetAwaiter().GetResult();
            var summary = payload?.Choices.FirstOrDefault()?.Message.Content?.Trim();

            if (string.IsNullOrWhiteSpace(summary))
            {
                return new AiManagementSummaryDto("LocalRules", "rule-based", DateTimeOffset.UtcNow, fallbackSummary, warnings);
            }

            return new AiManagementSummaryDto("ConfiguredGateway", _options.Ai.Model, DateTimeOffset.UtcNow, summary, warnings);
        }
        catch
        {
            warnings.Add("External AI gateway was unavailable; local summary rules were used instead.");
            return new AiManagementSummaryDto("LocalRules", "rule-based", DateTimeOffset.UtcNow, fallbackSummary, warnings);
        }
    }

    private sealed record ChatCompletionsRequest(string Model, IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionsResponse([property: JsonPropertyName("choices")] IReadOnlyList<Choice> Choices);

    private sealed record Choice([property: JsonPropertyName("message")] ChoiceMessage Message);

    private sealed record ChoiceMessage([property: JsonPropertyName("content")] string Content);
}
