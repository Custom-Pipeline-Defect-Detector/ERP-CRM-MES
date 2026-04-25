using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManufacturingPlatform.Host.Persistence;
using ManufacturingPlatform.Modules.Integration.Domain;
using Microsoft.Extensions.Options;

namespace ManufacturingPlatform.Host.Runtime;

public sealed class AccountingExportGatewayService(IHttpClientFactory httpClientFactory, IOptions<ManufacturingOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ManufacturingOptions _options = options.Value;

    public AccountingExportDispatchResult Dispatch(ExternalSystemEntity externalSystem, AccountingExportBatchEntity batch, IReadOnlyList<AccountingBridgeEntryEntity> bridges)
    {
        var payload = BuildPayload(externalSystem, batch, bridges);
        var requestPayloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        return externalSystem.AdapterMode switch
        {
            ExternalSystemAdapterMode.Mock => DispatchMock(externalSystem, batch, requestPayloadJson),
            ExternalSystemAdapterMode.Http => DispatchHttp(externalSystem, requestPayloadJson),
            _ => new AccountingExportDispatchResult(false, false, false, externalSystem.BaseUrl, null, null, null, null, requestPayloadJson, null, $"Unsupported adapter mode '{externalSystem.AdapterMode}'.")
        };
    }

    public AccountingExportReconciliationResult Reconcile(ExternalSystemEntity externalSystem, IntegrationJobEntity job)
    {
        return externalSystem.AdapterMode switch
        {
            ExternalSystemAdapterMode.Mock => ReconcileMock(externalSystem, job),
            ExternalSystemAdapterMode.Http => ReconcileHttp(externalSystem, job),
            _ => new AccountingExportReconciliationResult(false, false, false, externalSystem.BaseUrl, null, null, null, $"Unsupported adapter mode '{externalSystem.AdapterMode}'.")
        };
    }

    private AccountingExportDispatchResult DispatchMock(ExternalSystemEntity externalSystem, AccountingExportBatchEntity batch, string requestPayloadJson)
    {
        var externalJobNumber = $"KDJ-{batch.BatchNumber.Replace("EB-", string.Empty, StringComparison.OrdinalIgnoreCase)}";
        var response = new
        {
            accepted = true,
            status = "Sent",
            externalJobNumber,
            externalBatchNumber = batch.BatchNumber,
            message = "Mock Kingdee adapter accepted the export batch."
        };

        return new AccountingExportDispatchResult(
            true,
            false,
            false,
            externalSystem.BaseUrl,
            externalJobNumber,
            batch.BatchNumber,
            "202",
            "Mock Kingdee adapter accepted the export batch.",
            requestPayloadJson,
            JsonSerializer.Serialize(response, JsonOptions),
            null);
    }

    private AccountingExportReconciliationResult ReconcileMock(ExternalSystemEntity externalSystem, IntegrationJobEntity job)
    {
        var shouldFail = externalSystem.BaseUrl.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            (job.ExternalJobNumber?.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ?? false);
        var status = shouldFail ? "Failed" : "Succeeded";
        var message = shouldFail
            ? "Mock Kingdee adapter rejected the export batch during reconciliation."
            : "Mock Kingdee adapter confirmed the export batch.";
        var response = new
        {
            status,
            externalJobNumber = job.ExternalJobNumber,
            externalBatchNumber = job.ExternalBatchNumber,
            message
        };

        return new AccountingExportReconciliationResult(
            true,
            true,
            !shouldFail,
            externalSystem.BaseUrl,
            "200",
            message,
            JsonSerializer.Serialize(response, JsonOptions),
            shouldFail ? message : null);
    }

    private AccountingExportDispatchResult DispatchHttp(ExternalSystemEntity externalSystem, string requestPayloadJson)
    {
        try
        {
            var client = CreateClient(externalSystem);
            var endpoint = BuildEndpoint(externalSystem.BaseUrl, externalSystem.AccountingExportPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(requestPayloadJson, Encoding.UTF8, "application/json")
            };

            var response = client.Send(request);
            var responsePayload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return new AccountingExportDispatchResult(
                    false,
                    false,
                    false,
                    endpoint,
                    null,
                    null,
                    ((int)response.StatusCode).ToString(),
                    $"Dispatch failed with HTTP {(int)response.StatusCode}.",
                    requestPayloadJson,
                    EmptyToNull(responsePayload),
                    EmptyToNull(responsePayload) ?? $"Dispatch failed with HTTP {(int)response.StatusCode}.");
            }

            var parsed = ParseEnvelope(responsePayload);
            var accepted = parsed.Accepted ?? true;
            var status = parsed.Status ?? (response.StatusCode == System.Net.HttpStatusCode.Accepted ? "Sent" : "Succeeded");
            var isFinal = IsFinalStatus(status);
            var isSuccess = IsSuccessStatus(status);

            return new AccountingExportDispatchResult(
                accepted,
                isFinal,
                isSuccess,
                endpoint,
                parsed.ExternalJobNumber,
                parsed.ExternalBatchNumber,
                ((int)response.StatusCode).ToString(),
                parsed.Message ?? $"External system returned status {status}.",
                requestPayloadJson,
                EmptyToNull(responsePayload),
                accepted ? null : parsed.Message ?? "External system rejected the dispatch request.");
        }
        catch (HttpRequestException exception)
        {
            return new AccountingExportDispatchResult(false, false, false, externalSystem.BaseUrl, null, null, null, null, requestPayloadJson, null, exception.Message);
        }
        catch (TaskCanceledException)
        {
            return new AccountingExportDispatchResult(false, false, false, externalSystem.BaseUrl, null, null, null, null, requestPayloadJson, null, "Dispatch timed out.");
        }
    }

    private AccountingExportReconciliationResult ReconcileHttp(ExternalSystemEntity externalSystem, IntegrationJobEntity job)
    {
        if (string.IsNullOrWhiteSpace(job.ExternalJobNumber))
        {
            return new AccountingExportReconciliationResult(false, false, false, externalSystem.BaseUrl, null, null, null, "The integration job does not contain an external job number.");
        }

        try
        {
            var client = CreateClient(externalSystem);
            var statusPath = externalSystem.JobStatusPathTemplate.Replace("{externalJobNumber}", job.ExternalJobNumber, StringComparison.OrdinalIgnoreCase);
            var endpoint = BuildEndpoint(externalSystem.BaseUrl, statusPath);
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            var response = client.Send(request);
            var responsePayload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return new AccountingExportReconciliationResult(
                    false,
                    false,
                    false,
                    endpoint,
                    ((int)response.StatusCode).ToString(),
                    $"Reconciliation failed with HTTP {(int)response.StatusCode}.",
                    EmptyToNull(responsePayload),
                    EmptyToNull(responsePayload) ?? $"Reconciliation failed with HTTP {(int)response.StatusCode}.");
            }

            var parsed = ParseEnvelope(responsePayload);
            var status = parsed.Status ?? "Sent";

            return new AccountingExportReconciliationResult(
                true,
                IsFinalStatus(status),
                IsSuccessStatus(status),
                endpoint,
                ((int)response.StatusCode).ToString(),
                parsed.Message ?? $"External system returned status {status}.",
                EmptyToNull(responsePayload),
                IsFailureStatus(status) ? parsed.Message ?? "External system marked the job as failed." : null);
        }
        catch (HttpRequestException exception)
        {
            return new AccountingExportReconciliationResult(false, false, false, externalSystem.BaseUrl, null, null, null, exception.Message);
        }
        catch (TaskCanceledException)
        {
            return new AccountingExportReconciliationResult(false, false, false, externalSystem.BaseUrl, null, null, null, "Reconciliation timed out.");
        }
    }

    private HttpClient CreateClient(ExternalSystemEntity externalSystem)
    {
        var client = _httpClientFactory.CreateClient(nameof(AccountingExportGatewayService));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.Integration.TimeoutSeconds, 5, 120));
        client.BaseAddress = BuildBaseAddress(externalSystem.BaseUrl);
        return client;
    }

    private static string BuildEndpoint(string baseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolutePath))
        {
            return absolutePath.ToString();
        }

        var root = baseUrl.TrimEnd('/');
        var suffix = path.TrimStart('/');
        return $"{root}/{suffix}";
    }

    private static Uri BuildBaseAddress(string baseUrl)
    {
        return new Uri(baseUrl.TrimEnd('/') + "/");
    }

    private static AccountingExportEnvelope BuildPayload(ExternalSystemEntity externalSystem, AccountingExportBatchEntity batch, IReadOnlyList<AccountingBridgeEntryEntity> bridges)
    {
        return new AccountingExportEnvelope(
            batch.BatchNumber,
            "ManufacturingPlatform",
            externalSystem.SystemCode,
            batch.CreatedAt,
            batch.TotalAmount,
            bridges
                .OrderBy(entity => entity.BridgeNumber, StringComparer.OrdinalIgnoreCase)
                .Select(entity => new AccountingExportLine(
                    entity.BridgeNumber,
                    entity.SourceDocumentNumber,
                    entity.SourceEvent,
                    entity.PostingAccount,
                    entity.Amount,
                    entity.GeneratedAt))
                .ToArray());
    }

    private static ParsedEnvelope ParseEnvelope(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new ParsedEnvelope(null, null, null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            return new ParsedEnvelope(
                TryGetBoolean(root, "accepted"),
                TryGetString(root, "status"),
                TryGetString(root, "externalJobNumber"),
                TryGetString(root, "externalBatchNumber"),
                TryGetString(root, "message"));
        }
        catch (JsonException)
        {
            return new ParsedEnvelope(null, null, null, null, null);
        }
    }

    private static bool IsFinalStatus(string status)
    {
        return IsSuccessStatus(status) || IsFailureStatus(status);
    }

    private static bool IsSuccessStatus(string status)
    {
        return string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Acknowledged", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Posted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailureStatus(string status)
    {
        return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool? TryGetBoolean(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record ParsedEnvelope(bool? Accepted, string? Status, string? ExternalJobNumber, string? ExternalBatchNumber, string? Message);

    private sealed record AccountingExportEnvelope(
        string BatchNumber,
        string SourceSystem,
        string TargetSystem,
        DateTimeOffset GeneratedAt,
        decimal TotalAmount,
        IReadOnlyList<AccountingExportLine> Lines);

    private sealed record AccountingExportLine(
        string BridgeNumber,
        string SourceDocumentNumber,
        string SourceEvent,
        string PostingAccount,
        decimal Amount,
        DateTimeOffset GeneratedAt);
}

public sealed record AccountingExportDispatchResult(
    bool Accepted,
    bool IsFinal,
    bool Succeeded,
    string Endpoint,
    string? ExternalJobNumber,
    string? ExternalBatchNumber,
    string? ResponseCode,
    string? Message,
    string RequestPayloadJson,
    string? ResponsePayloadJson,
    string? ErrorMessage);

public sealed record AccountingExportReconciliationResult(
    bool Reachable,
    bool IsFinal,
    bool Succeeded,
    string Endpoint,
    string? ResponseCode,
    string? Message,
    string? ResponsePayloadJson,
    string? ErrorMessage);
