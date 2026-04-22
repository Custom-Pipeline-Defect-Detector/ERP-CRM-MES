using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.ERP.Application;
using ManufacturingPlatform.Modules.ERP.Contracts;
using ManufacturingPlatform.Modules.ERP.Domain;
using ManufacturingPlatform.Modules.ERP.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingPlatform.Modules.ERP;

public sealed class ErpModule : IModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "erp",
        "ERP",
        "erp",
        "Procurement, inventory, material master data, and finance-support workflows.",
        "Phase 1",
        ErpCapabilities.All);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/erp").WithTags("ERP");

        group.MapGet("/overview", Results);
        group.MapGet("/products", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetProducts(httpContext.GetSessionToken())));
        group.MapGet("/materials", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetMaterials(httpContext.GetSessionToken())));
        group.MapGet("/suppliers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSuppliers(httpContext.GetSessionToken())));
        group.MapGet("/purchase-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetPurchaseOrders(httpContext.GetSessionToken())));
        group.MapPost("/purchase-orders", (HttpContext httpContext, IManufacturingRuntime runtime, CreatePurchaseOrderRequest request) => RuntimeApi.Execute(() => runtime.CreatePurchaseOrder(httpContext.GetSessionToken(), request)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/submit", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId) => RuntimeApi.Execute(() => runtime.SubmitPurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/approve", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId) => RuntimeApi.Execute(() => runtime.ApprovePurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/receive", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId, ReceivePurchaseOrderRequest request) => RuntimeApi.Execute(() => runtime.ReceivePurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId, request)));
        group.MapGet("/goods-receipts", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetGoodsReceipts(httpContext.GetSessionToken())));
        group.MapGet("/production-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetProductionOrders(httpContext.GetSessionToken())));
        group.MapPost("/production-orders/{productionOrderId:guid}/release-to-mes", (HttpContext httpContext, IManufacturingRuntime runtime, Guid productionOrderId) => RuntimeApi.Execute(() => runtime.ReleaseProductionOrderToMes(httpContext.GetSessionToken(), productionOrderId)));
        group.MapGet("/sales-shipments", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSalesShipments(httpContext.GetSessionToken())));
        group.MapPost("/sales-shipments", (HttpContext httpContext, IManufacturingRuntime runtime, ShipSalesOrderRequest request) => RuntimeApi.Execute(() => runtime.ShipSalesOrder(httpContext.GetSessionToken(), request)));
        group.MapGet("/inventory-balances", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetInventoryBalances(httpContext.GetSessionToken())));
        group.MapGet("/cost-ledgers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetCostLedgers(httpContext.GetSessionToken())));
        group.MapGet("/accounting-bridges", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetAccountingBridges(httpContext.GetSessionToken())));
        group.MapPost("/accounting-bridges/{accountingBridgeId:guid}/state", (HttpContext httpContext, IManufacturingRuntime runtime, Guid accountingBridgeId, UpdateAccountingBridgeStateRequest request) => RuntimeApi.Execute(() => runtime.UpdateAccountingBridgeState(httpContext.GetSessionToken(), accountingBridgeId, request)));
    }

    private static Ok<ErpModuleOverview> Results()
    {
        return TypedResults.Ok(
            new ErpModuleOverview(
                "Plan-to-procure and stock-to-cost operations.",
                [nameof(Supplier), nameof(Material), nameof(Product), nameof(BillOfMaterials), nameof(Warehouse), nameof(PurchaseRequest), nameof(PurchaseOrder), nameof(GoodsReceipt), nameof(SalesShipment), nameof(SalesReturn), nameof(StockReservation), nameof(BatchBalance), nameof(InventoryTransaction), nameof(InventoryLedgerEntry), nameof(ProductionOrder), nameof(ExpenseRecord), nameof(CostCenter), nameof(OverheadRule), nameof(CostAllocationRule), nameof(WorkOrderCostLedger), nameof(BatchCostLedger), nameof(AccountingBridgeEntry), nameof(VoucherExportBatch)],
                ErpCapabilities.All,
                ErpInfrastructureNotes.All,
                ErpGovernance.OwnershipRules,
                ErpGovernance.WorkflowDefinitions,
                ErpGovernance.FinancialImpacts));
    }
}
