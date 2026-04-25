using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.ERP.Domain;
using ManufacturingPlatform.Modules.Integration.Domain;
using ManufacturingPlatform.Modules.MES.Domain;
using ManufacturingPlatform.Modules.Quality.Domain;

namespace ManufacturingPlatform.Host.Persistence;

public sealed class DepartmentEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<UserEntity> Users { get; set; } = [];
}

public sealed class RoleEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<UserRoleEntity> UserRoles { get; set; } = [];
    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = [];
}

public sealed class PermissionEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = [];
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string UserName { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? PasswordResetExpiresAt { get; set; }
    public int FailedAccessCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public Guid DepartmentId { get; set; }
    public DepartmentEntity? Department { get; set; }
    public ICollection<UserRoleEntity> UserRoles { get; set; } = [];
    public ICollection<SessionEntity> Sessions { get; set; } = [];
}

public sealed class UserRoleEntity
{
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public Guid RoleId { get; set; }
    public RoleEntity? Role { get; set; }
}

public sealed class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public RoleEntity? Role { get; set; }
    public Guid PermissionId { get; set; }
    public PermissionEntity? Permission { get; set; }
}

public sealed class SessionEntity
{
    public required string Token { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public required string Module { get; set; }
    public required string ActionName { get; set; }
    public required string Detail { get; set; }
    public required string PerformedBy { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class WarehouseEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsQualityControlled { get; set; }
    public bool SupportsWip { get; set; }
    public ICollection<InventoryTransactionEntity> InventoryTransactions { get; set; } = [];
}

public sealed class MaterialEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string BaseUnitCode { get; set; }
    public decimal StandardUnitCost { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsLotControlled { get; set; }
    public ICollection<BomComponentEntity> BomComponents { get; set; } = [];
    public ICollection<PurchaseOrderEntity> PurchaseOrders { get; set; } = [];
}

public sealed class ProductEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string ProductFamily { get; set; }
    public required string BaseUnitCode { get; set; }
    public bool IsLotControlled { get; set; }
    public decimal StandardLaborHoursPerUnit { get; set; }
    public decimal StandardMachineHoursPerUnit { get; set; }
    public decimal OverheadRatePerUnit { get; set; }
    public ICollection<BomComponentEntity> BomComponents { get; set; } = [];
}

public sealed class BomComponentEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public ProductEntity? Product { get; set; }
    public Guid MaterialId { get; set; }
    public MaterialEntity? Material { get; set; }
    public decimal QuantityPerUnit { get; set; }
}

public sealed class CustomerEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public CustomerStatus Status { get; set; }
    public CustomerSegment Segment { get; set; }
    public required string Region { get; set; }
    public required string Industry { get; set; }
    public required string CreditRating { get; set; }
    public ICollection<SalesOrderEntity> SalesOrders { get; set; } = [];
}

public sealed class SupplierEntity
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string TaxRegistrationNumber { get; set; }
    public int LeadTimeDays { get; set; }
    public required string PaymentTerms { get; set; }
    public ICollection<PurchaseOrderEntity> PurchaseOrders { get; set; } = [];
}

public sealed class PurchaseOrderEntity
{
    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity? Supplier { get; set; }
    public Guid MaterialId { get; set; }
    public MaterialEntity? Material { get; set; }
    public Guid WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public PurchaseOrderState State { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly OrderedOn { get; set; }
    public DateOnly ExpectedReceiptDate { get; set; }
    public ICollection<GoodsReceiptEntity> GoodsReceipts { get; set; } = [];
}

public sealed class GoodsReceiptEntity
{
    public Guid Id { get; set; }
    public required string ReceiptNumber { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrderEntity? PurchaseOrder { get; set; }
    public Guid WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public GoodsReceiptState State { get; set; }
    public InventoryQualityStatus QualityStatus { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public required string BatchNumber { get; set; }
    public DateOnly ReceivedOn { get; set; }
}

public sealed class SalesOrderEntity
{
    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerEntity? Customer { get; set; }
    public required string ProductCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public SalesOrderState State { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly RequestedDeliveryDate { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public ProductionOrderEntity? ProductionOrder { get; set; }
    public ICollection<SalesShipmentEntity> SalesShipments { get; set; } = [];
}

public sealed class SalesShipmentEntity
{
    public Guid Id { get; set; }
    public required string ShipmentNumber { get; set; }
    public Guid SalesOrderId { get; set; }
    public SalesOrderEntity? SalesOrder { get; set; }
    public Guid WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public SalesShipmentState State { get; set; }
    public decimal ShippedQuantity { get; set; }
    public required string BatchNumber { get; set; }
    public decimal UnitPrice { get; set; }
    public DateOnly ShippedOn { get; set; }
}

public sealed class ProductionOrderEntity
{
    public Guid Id { get; set; }
    public required string ProductionOrderNumber { get; set; }
    public Guid SalesOrderId { get; set; }
    public SalesOrderEntity? SalesOrder { get; set; }
    public ProductionOrderState State { get; set; }
    public required string ProductCode { get; set; }
    public decimal PlannedQuantity { get; set; }
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public Guid? WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
}

public sealed class WorkOrderEntity
{
    public Guid Id { get; set; }
    public required string WorkOrderNumber { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrderEntity? ProductionOrder { get; set; }
    public WorkOrderState State { get; set; }
    public required string ProductCode { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal GoodQuantity { get; set; }
    public decimal ScrapQuantity { get; set; }
    public decimal ReworkQuantity { get; set; }
    public decimal LaborHours { get; set; }
    public decimal MachineHours { get; set; }
    public required string CurrentOperationName { get; set; }
    public int CurrentOperationSequence { get; set; }
    public IssueMode IssueMode { get; set; }
    public bool IsMachineDriven { get; set; }
    public string? FinishedGoodsBatchNumber { get; set; }
    public ICollection<WorkOrderOperationEntity> Operations { get; set; } = [];
    public ICollection<QualityInspectionEntity> QualityInspections { get; set; } = [];
}

public sealed class WorkOrderOperationEntity
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public required string OperationCode { get; set; }
    public required string OperationName { get; set; }
    public int Sequence { get; set; }
    public OperationExecutionState State { get; set; }
    public bool IsMachineDriven { get; set; }
    public IssueMode IssueMode { get; set; }
    public required string WorkCenterCode { get; set; }
    public string? MachineCode { get; set; }
}

public sealed class InventoryTransactionEntity
{
    public Guid Id { get; set; }
    public required string TransactionNumber { get; set; }
    public InventoryTransactionType TransactionType { get; set; }
    public Guid WarehouseId { get; set; }
    public WarehouseEntity? Warehouse { get; set; }
    public required string ItemCode { get; set; }
    public decimal Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public InventoryQualityStatus QualityStatus { get; set; }
    public required string SourceDocumentNumber { get; set; }
    public required string SourceEvent { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class WarehouseTransferEntity
{
    public Guid Id { get; set; }
    public required string TransferNumber { get; set; }
    public Guid FromWarehouseId { get; set; }
    public WarehouseEntity? FromWarehouse { get; set; }
    public Guid ToWarehouseId { get; set; }
    public WarehouseEntity? ToWarehouse { get; set; }
    public required string ItemCode { get; set; }
    public required string ItemName { get; set; }
    public decimal Quantity { get; set; }
    public required string BatchNumber { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public InventoryQualityStatus QualityStatus { get; set; }
    public WarehouseTransferState State { get; set; }
    public DateTimeOffset TransferredAt { get; set; }
}

public sealed class CostPostingEntity
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrderEntity? ProductionOrder { get; set; }
    public required string PostingType { get; set; }
    public decimal Amount { get; set; }
    public required string Reference { get; set; }
    public DateTimeOffset PostedAt { get; set; }
}

public sealed class AccountingBridgeEntryEntity
{
    public Guid Id { get; set; }
    public required string BridgeNumber { get; set; }
    public required string SourceDocumentNumber { get; set; }
    public required string SourceEvent { get; set; }
    public AccountingBridgeState State { get; set; }
    public decimal Amount { get; set; }
    public required string PostingAccount { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public Guid? ExportBatchId { get; set; }
    public AccountingExportBatchEntity? ExportBatch { get; set; }
}

public sealed class SettlementEntryEntity
{
    public Guid Id { get; set; }
    public required string EntryNumber { get; set; }
    public SettlementType SettlementType { get; set; }
    public SettlementState State { get; set; }
    public required string SourceDocumentType { get; set; }
    public required string SourceDocumentNumber { get; set; }
    public required string CounterpartyName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSettledAt { get; set; }
    public InvoiceEntity? Invoice { get; set; }
    public ICollection<SettlementApplicationEntity> Applications { get; set; } = [];
}

public sealed class InvoiceEntity
{
    public Guid Id { get; set; }
    public required string InvoiceNumber { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public InvoiceState State { get; set; }
    public Guid SettlementEntryId { get; set; }
    public SettlementEntryEntity? SettlementEntry { get; set; }
    public required string SourceDocumentType { get; set; }
    public required string SourceDocumentNumber { get; set; }
    public required string CounterpartyName { get; set; }
    public decimal Amount { get; set; }
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<SettlementApplicationEntity> Applications { get; set; } = [];
}

public sealed class SettlementApplicationEntity
{
    public Guid Id { get; set; }
    public required string ApplicationNumber { get; set; }
    public Guid SettlementEntryId { get; set; }
    public SettlementEntryEntity? SettlementEntry { get; set; }
    public Guid? InvoiceId { get; set; }
    public InvoiceEntity? Invoice { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public required string AppliedBy { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
}

public sealed class AccountingPeriodEntity
{
    public Guid Id { get; set; }
    public required string PeriodCode { get; set; }
    public AccountingPeriodState State { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseNotes { get; set; }
}

public sealed class AccountingVoucherEntity
{
    public Guid Id { get; set; }
    public required string VoucherNumber { get; set; }
    public required string PeriodCode { get; set; }
    public AccountingVoucherState State { get; set; }
    public decimal TotalDebitAmount { get; set; }
    public decimal TotalCreditAmount { get; set; }
    public int LineCount { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? PostedBy { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public string? Notes { get; set; }
    public ICollection<AccountingVoucherLineEntity> Lines { get; set; } = [];
}

public sealed class AccountingVoucherLineEntity
{
    public Guid Id { get; set; }
    public Guid AccountingVoucherId { get; set; }
    public AccountingVoucherEntity? AccountingVoucher { get; set; }
    public Guid AccountingBridgeEntryId { get; set; }
    public AccountingBridgeEntryEntity? AccountingBridgeEntry { get; set; }
    public int LineNumber { get; set; }
    public required string SourceBridgeNumber { get; set; }
    public required string SourceDocumentNumber { get; set; }
    public required string SourceEvent { get; set; }
    public required string AccountCode { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public required string Narrative { get; set; }
}

public sealed class QualityInspectionEntity
{
    public Guid Id { get; set; }
    public required string InspectionNumber { get; set; }
    public InspectionType InspectionType { get; set; }
    public Guid WorkOrderId { get; set; }
    public WorkOrderEntity? WorkOrder { get; set; }
    public Guid ProductionOrderId { get; set; }
    public ProductionOrderEntity? ProductionOrder { get; set; }
    public required string BatchNumber { get; set; }
    public decimal InspectedQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public InspectionRecordState State { get; set; }
    public QualityDisposition? Disposition { get; set; }
    public string? InspectorUserName { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AiInsightRunEntity
{
    public Guid Id { get; set; }
    public required string InsightType { get; set; }
    public required string Source { get; set; }
    public required string Model { get; set; }
    public required string Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

public sealed class ReportExportJobEntity
{
    public Guid Id { get; set; }
    public required string JobNumber { get; set; }
    public required string ReportCode { get; set; }
    public required string ReportName { get; set; }
    public required string Format { get; set; }
    public required string State { get; set; }
    public int RowCount { get; set; }
    public required string RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public required string FileName { get; set; }
    public required string Content { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ExternalSystemEntity
{
    public Guid Id { get; set; }
    public required string SystemCode { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public ExternalSystemAdapterMode AdapterMode { get; set; }
    public required string AccountingExportPath { get; set; }
    public required string JobStatusPathTemplate { get; set; }
    public bool IsEnabled { get; set; }
    public ExternalSystemHealthStatus HealthStatus { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class AccountingExportBatchEntity
{
    public Guid Id { get; set; }
    public required string BatchNumber { get; set; }
    public required string TargetSystemCode { get; set; }
    public ExportBatchState State { get; set; }
    public int BridgeCount { get; set; }
    public decimal TotalAmount { get; set; }
    public int AttemptCount { get; set; }
    public string? ExternalBatchNumber { get; set; }
    public string? LastMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<AccountingBridgeEntryEntity> AccountingBridges { get; set; } = [];
    public ICollection<IntegrationJobEntity> IntegrationJobs { get; set; } = [];
}

public sealed class IntegrationJobEntity
{
    public Guid Id { get; set; }
    public required string JobNumber { get; set; }
    public required string TargetSystemCode { get; set; }
    public required string JobType { get; set; }
    public IntegrationJobState State { get; set; }
    public Guid? ExportBatchId { get; set; }
    public AccountingExportBatchEntity? ExportBatch { get; set; }
    public int AttemptNumber { get; set; }
    public string? DispatchEndpoint { get; set; }
    public string? ExternalJobNumber { get; set; }
    public string? ExternalBatchNumber { get; set; }
    public required string PayloadSummary { get; set; }
    public string? RequestPayloadJson { get; set; }
    public string? ResponsePayloadJson { get; set; }
    public string? ResponseCode { get; set; }
    public string? LastMessage { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? LastAttemptedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
