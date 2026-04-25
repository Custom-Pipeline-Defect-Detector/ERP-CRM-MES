namespace ManufacturingPlatform.Modules.ERP.Domain;

public enum PurchaseRequestState
{
    Draft,
    Submitted,
    Approved,
    Released,
    Closed,
    Cancelled
}

public enum PurchaseOrderState
{
    Draft,
    Submitted,
    Approved,
    Released,
    PartiallyReceived,
    Completed,
    Closed,
    Cancelled
}

public enum GoodsReceiptState
{
    Draft,
    Received,
    PendingInspection,
    Accepted,
    Rejected,
    Closed
}

public enum SalesShipmentState
{
    Draft,
    Approved,
    Picking,
    Shipped,
    Invoiced,
    Closed,
    Cancelled
}

public enum SalesReturnState
{
    Draft,
    Approved,
    Received,
    Closed,
    Cancelled
}

public enum InventoryTransactionType
{
    GoodsReceipt,
    GoodsIssue,
    Transfer,
    ProductionIssue,
    ProductionReceipt,
    SalesShipment,
    SalesReturn,
    Scrap,
    Rework
}

public enum InventoryQualityStatus
{
    PendingInspection,
    Quarantined,
    Released,
    ConditionalRelease,
    Rejected
}

public enum WarehouseTransferState
{
    Draft,
    Completed,
    Cancelled
}

public enum ProductionOrderState
{
    DraftPlan,
    Approved,
    ReleasedToMes,
    InExecution,
    TechnicallyCompleted,
    Costed,
    Closed,
    Cancelled
}

public enum AccountingBridgeState
{
    Draft,
    ReadyForExport,
    Exported,
    Posted,
    Reconciled,
    Failed
}

public enum SettlementType
{
    Payable,
    Receivable
}

public enum SettlementState
{
    Open,
    PartiallySettled,
    Settled,
    Cancelled
}

public enum InvoiceType
{
    Receivable,
    Payable
}

public enum InvoiceState
{
    Draft,
    Issued,
    PartiallySettled,
    Settled,
    Cancelled
}

public enum AccountingPeriodState
{
    Open,
    Closed
}

public enum AccountingVoucherState
{
    Draft,
    Approved,
    Posted,
    Cancelled
}

public sealed record Supplier(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string TaxRegistrationNumber);

public sealed record Material(
    Guid Id,
    string Code,
    string Name,
    string Specification,
    string BaseUnitCode,
    bool IsLotControlled);

public sealed record Product(
    Guid Id,
    string Code,
    string Name,
    string ProductFamily,
    string BaseUnitCode,
    bool IsLotControlled);

public sealed record BillOfMaterials(
    Guid Id,
    string BomNumber,
    string Version,
    Guid ProductId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record Warehouse(
    Guid Id,
    string Code,
    string Name,
    bool IsQualityControlled,
    bool SupportsWip);

public sealed record PurchaseRequest(
    Guid Id,
    string RequestNumber,
    PurchaseRequestState State,
    Guid RequestedByDepartmentId,
    DateOnly RequestedOn);

public sealed record PurchaseOrder(
    Guid Id,
    string OrderNumber,
    PurchaseOrderState State,
    Guid SupplierId,
    DateOnly OrderedOn,
    DateOnly? ExpectedReceiptDate);

public sealed record GoodsReceipt(
    Guid Id,
    string ReceiptNumber,
    GoodsReceiptState State,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceivedOn,
    InventoryQualityStatus QualityStatus);

public sealed record SalesShipment(
    Guid Id,
    string ShipmentNumber,
    SalesShipmentState State,
    Guid SalesOrderId,
    Guid WarehouseId,
    DateOnly ShippedOn);

public sealed record SalesReturn(
    Guid Id,
    string ReturnNumber,
    SalesReturnState State,
    Guid SalesShipmentId,
    DateOnly ReturnedOn);

public sealed record StockReservation(
    Guid Id,
    string ReservationNumber,
    Guid WarehouseId,
    string ItemCode,
    string? BatchNumber,
    decimal ReservedQuantity);

public sealed record BatchBalance(
    Guid Id,
    Guid WarehouseId,
    string ItemCode,
    string BatchNumber,
    decimal OnHandQuantity,
    decimal AvailableQuantity,
    InventoryQualityStatus QualityStatus);

public sealed record InventoryTransaction(
    Guid Id,
    string TransactionNumber,
    InventoryTransactionType TransactionType,
    Guid WarehouseId,
    string ItemCode,
    decimal Quantity,
    string? BatchNumber,
    DateTimeOffset OccurredAt);

public sealed record InventoryLedgerEntry(
    Guid Id,
    string LedgerNumber,
    Guid InventoryTransactionId,
    decimal Amount,
    string ValuationLayer);

public sealed record WarehouseTransfer(
    Guid Id,
    string TransferNumber,
    WarehouseTransferState State,
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    string ItemCode,
    decimal Quantity,
    string? BatchNumber,
    DateTimeOffset TransferredAt);

public sealed record ProductionOrder(
    Guid Id,
    string ProductionOrderNumber,
    ProductionOrderState State,
    string ProductCode,
    decimal PlannedQuantity,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate);

public sealed record ExpenseRecord(
    Guid Id,
    string ExpenseNumber,
    Guid CostCenterId,
    decimal Amount,
    DateOnly IncurredOn);

public sealed record CostCenter(
    Guid Id,
    string Code,
    string Name);

public sealed record OverheadRule(
    Guid Id,
    string Code,
    string Name,
    string AllocationBasis,
    decimal Rate);

public sealed record CostAllocationRule(
    Guid Id,
    string Code,
    string Name,
    string CostDriver,
    string TargetObjectType);

public sealed record WorkOrderCostLedger(
    Guid Id,
    Guid WorkOrderId,
    decimal MaterialCost,
    decimal LaborCost,
    decimal MachineCost,
    decimal OverheadCost,
    decimal ScrapCost);

public sealed record BatchCostLedger(
    Guid Id,
    string BatchNumber,
    decimal TotalCost,
    decimal GoodQuantity,
    decimal UnitCost);

public sealed record AccountingBridgeEntry(
    Guid Id,
    string BridgeNumber,
    string SourceDocumentNumber,
    string SourceEvent,
    AccountingBridgeState State,
    decimal Amount,
    string PostingAccount);

public sealed record SettlementEntry(
    Guid Id,
    string EntryNumber,
    SettlementType SettlementType,
    SettlementState State,
    string SourceDocumentType,
    string SourceDocumentNumber,
    string CounterpartyName,
    decimal TotalAmount,
    decimal SettledAmount,
    DateOnly DueDate,
    DateTimeOffset CreatedAt);

public sealed record InvoiceDocument(
    Guid Id,
    string InvoiceNumber,
    InvoiceType InvoiceType,
    InvoiceState State,
    string SourceDocumentType,
    string SourceDocumentNumber,
    string CounterpartyName,
    decimal Amount,
    DateOnly IssuedOn,
    DateOnly DueDate);

public sealed record SettlementApplication(
    Guid Id,
    string ApplicationNumber,
    Guid SettlementEntryId,
    Guid? InvoiceId,
    decimal Amount,
    string? Reference,
    string AppliedBy,
    DateTimeOffset AppliedAt);

public sealed record AccountingPeriod(
    Guid Id,
    string PeriodCode,
    AccountingPeriodState State,
    DateOnly StartDate,
    DateOnly EndDate,
    string? ClosedBy,
    DateTimeOffset? ClosedAt);

public sealed record AccountingVoucher(
    Guid Id,
    string VoucherNumber,
    string PeriodCode,
    AccountingVoucherState State,
    decimal TotalDebitAmount,
    decimal TotalCreditAmount,
    DateTimeOffset CreatedAt);

public sealed record AccountingVoucherLine(
    Guid Id,
    Guid AccountingVoucherId,
    Guid AccountingBridgeEntryId,
    int LineNumber,
    string AccountCode,
    decimal DebitAmount,
    decimal CreditAmount);

public sealed record VoucherExportBatch(
    Guid Id,
    string ExportBatchNumber,
    AccountingBridgeState State,
    DateTimeOffset GeneratedAt);
