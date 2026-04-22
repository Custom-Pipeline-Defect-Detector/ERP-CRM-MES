using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.ERP.Domain;

namespace ManufacturingPlatform.Modules.ERP.Application;

public static class ErpGovernance
{
    public static readonly DataOwnershipDefinition[] OwnershipRules =
    [
        new(nameof(Supplier), "ERP", ["Reporting", "Integration"], "ERP owns supplier qualification and commercial procurement records."),
        new(nameof(Warehouse), "ERP", ["MES", "Quality", "Reporting"], "Warehouse structure and stock ledger ownership remain in ERP."),
        new(nameof(InventoryTransaction), "ERP", ["MES", "Quality", "Reporting", "Integration"], "ERP owns the stock ledger and inventory valuation events."),
        new(nameof(ProductionOrder), "ERP / production planning", ["MES", "Reporting", "Integration"], "ERP owns production demand planning and release."),
        new(nameof(AccountingBridgeEntry), "ERP", ["Integration", "Reporting"], "ERP owns accounting bridge entries and posting confirmation back from Kingdee.")
    ];

    public static readonly WorkflowDefinition[] WorkflowDefinitions =
    [
        new(
            nameof(PurchaseOrder),
            "Purchasing",
            Enum.GetNames<PurchaseOrderState>(),
            [
                new(nameof(PurchaseOrderState.Draft), nameof(PurchaseOrderState.Submitted), "Submit for approval", "Purchasing"),
                new(nameof(PurchaseOrderState.Submitted), nameof(PurchaseOrderState.Approved), "Approve purchase order", "Purchasing manager"),
                new(nameof(PurchaseOrderState.Approved), nameof(PurchaseOrderState.Released), "Send to supplier", "Purchasing"),
                new(nameof(PurchaseOrderState.Released), nameof(PurchaseOrderState.PartiallyReceived), "Receive partial goods", "Warehouse"),
                new(nameof(PurchaseOrderState.PartiallyReceived), nameof(PurchaseOrderState.Completed), "Receive all goods", "Warehouse"),
                new(nameof(PurchaseOrderState.Completed), nameof(PurchaseOrderState.Closed), "Close purchasing document", "Purchasing"),
                new(nameof(PurchaseOrderState.Draft), nameof(PurchaseOrderState.Cancelled), "Cancel draft purchase order", "Purchasing")
            ]),
        new(
            nameof(ProductionOrder),
            "Production planning",
            Enum.GetNames<ProductionOrderState>(),
            [
                new(nameof(ProductionOrderState.DraftPlan), nameof(ProductionOrderState.Approved), "Approve production order", "Planning"),
                new(nameof(ProductionOrderState.Approved), nameof(ProductionOrderState.ReleasedToMes), "Release to MES", "Planning"),
                new(nameof(ProductionOrderState.ReleasedToMes), nameof(ProductionOrderState.InExecution), "Start execution in MES", "MES"),
                new(nameof(ProductionOrderState.InExecution), nameof(ProductionOrderState.TechnicallyCompleted), "Complete execution", "MES"),
                new(nameof(ProductionOrderState.TechnicallyCompleted), nameof(ProductionOrderState.Costed), "Settle cost", "Cost accounting"),
                new(nameof(ProductionOrderState.Costed), nameof(ProductionOrderState.Closed), "Close production order", "Planning")
            ])
    ];

    public static readonly FinancialImpactDefinition[] FinancialImpacts =
    [
        new(nameof(GoodsReceipt), "Increase inventory and create payable bridge basis", "Inventory / GRNI / AP bridge", "Accepted receipts create stock and accounting bridge events."),
        new(nameof(SalesShipment), "Decrease inventory and create revenue / receivable bridge basis", "Inventory / COGS / AR bridge", "Shipped quantities create shipment and cost-recognition bridge events."),
        new(nameof(InventoryTransaction), "Post inventory, WIP, scrap, or transfer valuation changes", "Inventory / WIP / Scrap", "Every inventory-affecting transaction must create a value-layer event."),
        new(nameof(AccountingBridgeEntry), "Export controlled voucher-ready posting data", "Kingdee voucher interface", "ERP generates the bridge output even when Kingdee remains the ledger.")
    ];
}
