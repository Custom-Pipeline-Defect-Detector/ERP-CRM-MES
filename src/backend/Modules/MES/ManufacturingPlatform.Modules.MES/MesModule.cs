using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.MES.Application;
using ManufacturingPlatform.Modules.MES.Contracts;
using ManufacturingPlatform.Modules.MES.Domain;
using ManufacturingPlatform.Modules.MES.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.MES;

public sealed class MesModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "mes",
        "MES",
        "mes",
        "Shop-floor execution, traceability, and production control.",
        "Phase 1",
        MesCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/mes").WithTags("MES");

        group.MapGet("/overview", Results);
        group.MapGet("/work-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetWorkOrders(httpContext.GetSessionToken())));
        group.MapPost("/work-orders/{workOrderId:guid}/dispatch", (HttpContext httpContext, IManufacturingRuntime runtime, Guid workOrderId) => RuntimeApi.Execute(() => runtime.DispatchWorkOrder(httpContext.GetSessionToken(), workOrderId)));
        group.MapPost("/work-orders/{workOrderId:guid}/start", (HttpContext httpContext, IManufacturingRuntime runtime, Guid workOrderId) => RuntimeApi.Execute(() => runtime.StartWorkOrder(httpContext.GetSessionToken(), workOrderId)));
        group.MapPost("/work-orders/{workOrderId:guid}/report", (HttpContext httpContext, IManufacturingRuntime runtime, Guid workOrderId, ReportWorkOrderRequest request) => RuntimeApi.Execute(() => runtime.ReportWorkOrder(httpContext.GetSessionToken(), workOrderId, request)));
    }

    private static Ok<MesModuleOverview> Results()
    {
        return TypedResults.Ok(
            new MesModuleOverview(
                "Order-to-execution and workshop traceability workflows.",
                [nameof(ProcessRoute), nameof(ProcessStep), nameof(WorkCenter), nameof(Machine), nameof(ProductionOrderRelease), nameof(WorkOrder), nameof(WorkOrderOperation), nameof(OperationDispatch), nameof(OperatorAssignment), nameof(WipLot), nameof(WipMove), nameof(MaterialIssue), nameof(MaterialConsumptionActual), nameof(LaborTimeEntry), nameof(MachineTimeEntry), nameof(ProductionReport), nameof(ScrapRecord), nameof(ReworkOrder), nameof(BatchGenealogyLink), nameof(EquipmentStatusLog), nameof(DowntimeRecord), nameof(OeeSnapshot)],
                MesCapabilities.All,
                MesInfrastructureNotes.All,
                MesGovernance.OwnershipRules,
                MesGovernance.WorkflowDefinitions,
                MesGovernance.FinancialImpacts));
    }
}
