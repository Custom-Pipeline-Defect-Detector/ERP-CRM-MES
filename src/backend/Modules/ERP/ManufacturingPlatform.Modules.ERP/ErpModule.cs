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
        group.MapGet("/warehouses", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetWarehouses(httpContext.GetSessionToken())));
        group.MapGet("/purchase-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetPurchaseOrders(httpContext.GetSessionToken())));
        group.MapPost("/purchase-orders", (HttpContext httpContext, IManufacturingRuntime runtime, CreatePurchaseOrderRequest request) => RuntimeApi.Execute(() => runtime.CreatePurchaseOrder(httpContext.GetSessionToken(), request)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/submit", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId) => RuntimeApi.Execute(() => runtime.SubmitPurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/approve", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId) => RuntimeApi.Execute(() => runtime.ApprovePurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId)));
        group.MapPost("/purchase-orders/{purchaseOrderId:guid}/receive", (HttpContext httpContext, IManufacturingRuntime runtime, Guid purchaseOrderId, ReceivePurchaseOrderRequest request) => RuntimeApi.Execute(() => runtime.ReceivePurchaseOrder(httpContext.GetSessionToken(), purchaseOrderId, request)));
        group.MapGet("/goods-receipts", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetGoodsReceipts(httpContext.GetSessionToken())));
        group.MapGet("/warehouse-transfers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetWarehouseTransfers(httpContext.GetSessionToken())));
        group.MapPost("/warehouse-transfers", (HttpContext httpContext, IManufacturingRuntime runtime, CreateWarehouseTransferRequest request) => RuntimeApi.Execute(() => runtime.CreateWarehouseTransfer(httpContext.GetSessionToken(), request)));
        group.MapGet("/production-orders", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetProductionOrders(httpContext.GetSessionToken())));
        group.MapPost("/production-orders/{productionOrderId:guid}/release-to-mes", (HttpContext httpContext, IManufacturingRuntime runtime, Guid productionOrderId) => RuntimeApi.Execute(() => runtime.ReleaseProductionOrderToMes(httpContext.GetSessionToken(), productionOrderId)));
        group.MapGet("/sales-shipments", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSalesShipments(httpContext.GetSessionToken())));
        group.MapPost("/sales-shipments", (HttpContext httpContext, IManufacturingRuntime runtime, ShipSalesOrderRequest request) => RuntimeApi.Execute(() => runtime.ShipSalesOrder(httpContext.GetSessionToken(), request)));
        group.MapGet("/inventory-balances", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetInventoryBalances(httpContext.GetSessionToken())));
        group.MapGet("/cost-ledgers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetCostLedgers(httpContext.GetSessionToken())));
        group.MapGet("/accounting-bridges", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetAccountingBridges(httpContext.GetSessionToken())));
        group.MapPost("/accounting-bridges/{accountingBridgeId:guid}/state", (HttpContext httpContext, IManufacturingRuntime runtime, Guid accountingBridgeId, UpdateAccountingBridgeStateRequest request) => RuntimeApi.Execute(() => runtime.UpdateAccountingBridgeState(httpContext.GetSessionToken(), accountingBridgeId, request)));
        group.MapGet("/invoices", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetInvoices(httpContext.GetSessionToken())));
        group.MapPost("/invoices", (HttpContext httpContext, IManufacturingRuntime runtime, CreateInvoiceRequest request) => RuntimeApi.Execute(() => runtime.CreateInvoice(httpContext.GetSessionToken(), request)));
        group.MapPost("/invoices/{invoiceId:guid}/issue", (HttpContext httpContext, IManufacturingRuntime runtime, Guid invoiceId) => RuntimeApi.Execute(() => runtime.IssueInvoice(httpContext.GetSessionToken(), invoiceId)));
        group.MapGet("/settlements", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSettlements(httpContext.GetSessionToken())));
        group.MapPost("/settlements/{settlementEntryId:guid}/apply", (HttpContext httpContext, IManufacturingRuntime runtime, Guid settlementEntryId, ApplySettlementRequest request) => RuntimeApi.Execute(() => runtime.ApplySettlement(httpContext.GetSessionToken(), settlementEntryId, request)));
        group.MapGet("/settlement-applications", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSettlementApplications(httpContext.GetSessionToken())));
        group.MapGet("/settlement-aging", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetSettlementAging(httpContext.GetSessionToken())));
        group.MapGet("/accounting-periods", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetAccountingPeriods(httpContext.GetSessionToken())));
        group.MapPost("/accounting-periods/{accountingPeriodId:guid}/close", (HttpContext httpContext, IManufacturingRuntime runtime, Guid accountingPeriodId, CloseAccountingPeriodRequest request) => RuntimeApi.Execute(() => runtime.CloseAccountingPeriod(httpContext.GetSessionToken(), accountingPeriodId, request)));
        group.MapGet("/accounting-vouchers", (HttpContext httpContext, IManufacturingRuntime runtime) => RuntimeApi.Execute(() => runtime.GetAccountingVouchers(httpContext.GetSessionToken())));
        group.MapPost("/accounting-vouchers", (HttpContext httpContext, IManufacturingRuntime runtime, CreateAccountingVoucherRequest request) => RuntimeApi.Execute(() => runtime.CreateAccountingVoucher(httpContext.GetSessionToken(), request)));
        group.MapPost("/accounting-vouchers/{accountingVoucherId:guid}/approve", (HttpContext httpContext, IManufacturingRuntime runtime, Guid accountingVoucherId) => RuntimeApi.Execute(() => runtime.ApproveAccountingVoucher(httpContext.GetSessionToken(), accountingVoucherId)));
        group.MapPost("/accounting-vouchers/{accountingVoucherId:guid}/post", (HttpContext httpContext, IManufacturingRuntime runtime, Guid accountingVoucherId) => RuntimeApi.Execute(() => runtime.PostAccountingVoucher(httpContext.GetSessionToken(), accountingVoucherId)));
    }

    private static Ok<ErpModuleOverview> Results()
    {
        return TypedResults.Ok(
            new ErpModuleOverview(
                "Plan-to-procure and stock-to-cost operations.",
                [nameof(Supplier), nameof(Material), nameof(Product), nameof(BillOfMaterials), nameof(Warehouse), nameof(PurchaseRequest), nameof(PurchaseOrder), nameof(GoodsReceipt), nameof(SalesShipment), nameof(SalesReturn), nameof(StockReservation), nameof(BatchBalance), nameof(InventoryTransaction), nameof(InventoryLedgerEntry), nameof(ProductionOrder), nameof(ExpenseRecord), nameof(CostCenter), nameof(OverheadRule), nameof(CostAllocationRule), nameof(WorkOrderCostLedger), nameof(BatchCostLedger), nameof(AccountingBridgeEntry), nameof(AccountingVoucher), nameof(AccountingVoucherLine), nameof(VoucherExportBatch)],
                ErpCapabilities.All,
                ErpInfrastructureNotes.All,
                ErpGovernance.OwnershipRules,
                ErpGovernance.WorkflowDefinitions,
                ErpGovernance.FinancialImpacts));
    }
}
