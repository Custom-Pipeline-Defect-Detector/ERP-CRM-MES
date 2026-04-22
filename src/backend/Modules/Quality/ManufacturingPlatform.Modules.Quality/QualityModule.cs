using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.Quality.Application;
using ManufacturingPlatform.Modules.Quality.Contracts;
using ManufacturingPlatform.Modules.Quality.Domain;
using ManufacturingPlatform.Modules.Quality.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.Quality;

public sealed class QualityModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "quality",
        "Quality",
        "quality",
        "Inspection, defect, and corrective action management.",
        "Phase 2",
        QualityCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/quality").WithTags("Quality");

        group.MapGet("/overview", Results);
        group.MapGet("/inspections", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetQualityInspections(httpContext.GetSessionToken())));
        group.MapPost("/inspections/{inspectionId:guid}/complete", (HttpContext httpContext, IManufacturingRuntime runtime, Guid inspectionId, CompleteQualityInspectionRequest request) => RuntimeApi.Execute(() => runtime.CompleteQualityInspection(httpContext.GetSessionToken(), inspectionId, request)));
    }

    private static Ok<QualityModuleOverview> Results()
    {
        return TypedResults.Ok(
            new QualityModuleOverview(
                "Inspection-to-corrective-action quality control workflows.",
                [nameof(QualityStandard), nameof(InspectionPlan), nameof(InspectionLot), nameof(InspectionRecord), nameof(QualityStatus), nameof(HoldReleaseRecord), nameof(DefectRecord), nameof(NonconformanceDisposition), nameof(Ncr), nameof(Capa)],
                QualityCapabilities.All,
                QualityInfrastructureNotes.All,
                QualityGovernance.OwnershipRules,
                QualityGovernance.WorkflowDefinitions));
    }
}
