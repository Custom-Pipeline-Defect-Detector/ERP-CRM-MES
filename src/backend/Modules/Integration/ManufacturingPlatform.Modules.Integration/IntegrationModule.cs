using ManufacturingPlatform.BuildingBlocks.Abstractions;
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
