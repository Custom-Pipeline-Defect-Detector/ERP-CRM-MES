using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
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
        group.MapGet("/customers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetCustomers(httpContext.GetSessionToken())));
        group.MapPost("/customers", (HttpContext httpContext, IManufacturingRuntime runtime, CreateCustomerRequest request) => RuntimeApi.Execute(() => runtime.CreateCustomer(httpContext.GetSessionToken(), request)));
        group.MapGet("/sales-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSalesOrders(httpContext.GetSessionToken())));
        group.MapPost("/sales-orders", (HttpContext httpContext, IManufacturingRuntime runtime, CreateSalesOrderRequest request) => RuntimeApi.Execute(() => runtime.CreateSalesOrder(httpContext.GetSessionToken(), request)));
        group.MapPost("/sales-orders/{salesOrderId:guid}/submit", (HttpContext httpContext, IManufacturingRuntime runtime, Guid salesOrderId) => RuntimeApi.Execute(() => runtime.SubmitSalesOrder(httpContext.GetSessionToken(), salesOrderId)));
        group.MapPost("/sales-orders/{salesOrderId:guid}/approve", (HttpContext httpContext, IManufacturingRuntime runtime, Guid salesOrderId) => RuntimeApi.Execute(() => runtime.ApproveSalesOrder(httpContext.GetSessionToken(), salesOrderId)));
        group.MapPost("/sales-orders/{salesOrderId:guid}/release", (HttpContext httpContext, IManufacturingRuntime runtime, Guid salesOrderId) => RuntimeApi.Execute(() => runtime.ReleaseSalesOrder(httpContext.GetSessionToken(), salesOrderId)));
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
