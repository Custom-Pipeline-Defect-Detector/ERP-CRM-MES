using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.Integration.Application;
using ManufacturingPlatform.Modules.Integration.Contracts;
using ManufacturingPlatform.Modules.Integration.Domain;
using ManufacturingPlatform.Modules.Integration.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.Integration;

public sealed class IntegrationModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "integration",
        "Integration",
        "integration",
        "Legacy-system adapters, synchronization, and reconciliation.",
        "Phase 1",
        IntegrationCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/integration").WithTags("Integration");

        group.MapGet("/overview", Results);
        group.MapGet("/external-systems", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetExternalSystems(httpContext.GetSessionToken())));
        group.MapGet("/accounting-export-batches", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetAccountingExportBatches(httpContext.GetSessionToken())));
        group.MapPost("/accounting-export-batches", (HttpContext httpContext, IManufacturingRuntime runtime, CreateAccountingExportBatchRequest request) => RuntimeApi.Execute(() => runtime.CreateAccountingExportBatch(httpContext.GetSessionToken(), request)));
        group.MapPost("/accounting-export-batches/{exportBatchId:guid}/dispatch", (HttpContext httpContext, IManufacturingRuntime runtime, Guid exportBatchId) => RuntimeApi.Execute(() => runtime.DispatchAccountingExportBatch(httpContext.GetSessionToken(), exportBatchId)));
        group.MapGet("/jobs", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetIntegrationJobs(httpContext.GetSessionToken())));
        group.MapPost("/jobs/{integrationJobId:guid}/reconcile", (HttpContext httpContext, IManufacturingRuntime runtime, Guid integrationJobId, ReconcileIntegrationJobRequest request) => RuntimeApi.Execute(() => runtime.ReconcileIntegrationJob(httpContext.GetSessionToken(), integrationJobId, request)));
        group.MapPost("/jobs/{integrationJobId:guid}/complete", (HttpContext httpContext, IManufacturingRuntime runtime, Guid integrationJobId, CompleteIntegrationJobRequest request) => RuntimeApi.Execute(() => runtime.CompleteIntegrationJob(httpContext.GetSessionToken(), integrationJobId, request)));
    }

    private static Ok<IntegrationModuleOverview> Results()
    {
        return TypedResults.Ok(
            new IntegrationModuleOverview(
                "Kingdee coexistence, migration, and reconciliation workflows.",
                [nameof(ExternalSystem), nameof(SyncContractDefinition), nameof(SyncJob), nameof(SyncMapping), nameof(SyncFailure), nameof(ReconciliationRule), nameof(ReconciliationSnapshot)],
                IntegrationCapabilities.All,
                IntegrationInfrastructureNotes.All));
    }
}
