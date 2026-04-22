using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.ERP.Domain;
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
