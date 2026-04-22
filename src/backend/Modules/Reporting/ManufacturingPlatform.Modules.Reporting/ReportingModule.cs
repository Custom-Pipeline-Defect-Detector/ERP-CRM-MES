using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.Reporting.Application;
using ManufacturingPlatform.Modules.Reporting.Contracts;
using ManufacturingPlatform.Modules.Reporting.Domain;
using ManufacturingPlatform.Modules.Reporting.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.Reporting;

public sealed class ReportingModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "reporting",
        "Reporting",
        "reporting",
        "Dashboards, exports, and management reporting surfaces.",
        "Phase 1",
        ReportingCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reporting").WithTags("Reporting");

        group.MapGet("/overview", Results);
    }

    private static Ok<ReportingModuleOverview> Results()
    {
        return TypedResults.Ok(
            new ReportingModuleOverview(
                "Decision support through dashboards, printable reports, and exports.",
                [nameof(DatasetDefinition), nameof(SemanticMetricDefinition), nameof(DrillDownDefinition), nameof(DashboardDefinition), nameof(ReportDefinition), nameof(ReportSnapshot), nameof(ExportJob)],
                ReportingCapabilities.All,
                ReportingInfrastructureNotes.All));
    }
}
