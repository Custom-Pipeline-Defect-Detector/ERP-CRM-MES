using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.AI.Application;
using ManufacturingPlatform.Modules.AI.Contracts;
using ManufacturingPlatform.Modules.AI.Domain;
using ManufacturingPlatform.Modules.AI.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.AI;

public sealed class AiModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "ai",
        "AI",
        "ai",
        "Read-only intelligence services over curated business datasets.",
        "Phase 4",
        AiCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ai").WithTags("AI");

        group.MapGet("/overview", Results);
        group.MapGet("/management-summary", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GenerateManagementSummary(httpContext.GetSessionToken())));
    }

    private static Ok<AiModuleOverview> Results()
    {
        return TypedResults.Ok(
            new AiModuleOverview(
                "Insights, forecasting, anomaly detection, and report assistance.",
                [nameof(InsightPrompt), nameof(InsightRun), nameof(InsightResult), nameof(ForecastScenario), nameof(AnomalyRule), nameof(AnomalyAlert)],
                AiCapabilities.All,
                AiInfrastructureNotes.All));
    }
}
