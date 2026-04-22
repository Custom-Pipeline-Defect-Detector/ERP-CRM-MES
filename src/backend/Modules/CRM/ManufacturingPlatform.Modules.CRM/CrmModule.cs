using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.CRM.Application;
using ManufacturingPlatform.Modules.CRM.Contracts;
using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.CRM.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.CRM;

public sealed class CrmModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "crm",
        "CRM",
        "crm",
        "Customer lifecycle and commercial pipeline management.",
        "Phase 1",
        CrmCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/crm").WithTags("CRM");

        group.MapGet("/overview", Results);
    }

    private static Ok<CrmModuleOverview> Results()
    {
        return TypedResults.Ok(
            new CrmModuleOverview(
                "Lead-to-order and customer relationship workflows.",
                [nameof(Customer), nameof(CustomerContact), nameof(Lead), nameof(LeadQualificationRecord), nameof(Opportunity), nameof(OpportunityStageHistory), nameof(Quote), nameof(SalesActivity), nameof(FollowUpTask), nameof(Reminder), nameof(Contract), nameof(ContractRevision), nameof(SalesOrder), nameof(SalesTarget), nameof(SalesPerformanceSnapshot), nameof(AfterSalesTicket), nameof(WarrantyClaim)],
                CrmCapabilities.All,
                CrmInfrastructureNotes.All,
                CrmGovernance.OwnershipRules,
                CrmGovernance.WorkflowDefinitions));
    }
}
