namespace ManufacturingPlatform.BuildingBlocks.Runtime;

public sealed record SessionUserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string Department,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record SessionDto(
    string Token,
    DateTimeOffset ExpiresAt,
    SessionUserDto User);

public sealed record LoginRequest(string UserName);

public sealed record UserListItemDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string Department,
    IReadOnlyList<string> Roles);

public sealed record AuditLogItemDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EntityName,
    string ActionName,
    string Detail,
    string PerformedBy);

public sealed record DashboardMetricsDto(
    int CustomerCount,
    int ActiveSalesOrders,
    int ActiveProductionOrders,
    int ActiveWorkOrders,
    decimal OpenSalesAmount,
    decimal ReportedGoodQuantity,
    int PendingQualityInspections,
    int InventoryItemsTracked,
    int ReadyAccountingBridgeEntries);

public sealed record ActivityFeedItemDto(
    DateTimeOffset OccurredAt,
    string Module,
    string Summary);

public sealed record DashboardSnapshotDto(
    DashboardMetricsDto Metrics,
    IReadOnlyList<SalesOrderListItemDto> RecentSalesOrders,
    IReadOnlyList<ProductionOrderListItemDto> ActiveProductionOrders,
    IReadOnlyList<WorkOrderListItemDto> ActiveWorkOrders,
    IReadOnlyList<ActivityFeedItemDto> RecentActivity);

public sealed record ProductListItemDto(
    Guid Id,
    string Code,
    string Name,
    string ProductFamily,
    string BaseUnitCode);

public sealed record MaterialListItemDto(
    Guid Id,
    string Code,
    string Name,
    string BaseUnitCode,
    decimal StandardUnitCost,
    decimal ReorderLevel);

public sealed record SupplierListItemDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string TaxRegistrationNumber,
    int LeadTimeDays,
    string PaymentTerms);

public sealed record PurchaseOrderListItemDto(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    string MaterialCode,
    decimal OrderedQuantity,
    decimal ReceivedQuantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string WarehouseCode,
    string State,
    DateOnly OrderedOn,
    DateOnly ExpectedReceiptDate);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    string MaterialCode,
    decimal OrderedQuantity,
    decimal UnitPrice,
    string WarehouseCode,
    DateOnly ExpectedReceiptDate);

public sealed record GoodsReceiptListItemDto(
    Guid Id,
    string ReceiptNumber,
    string PurchaseOrderNumber,
    string SupplierName,
    string MaterialCode,
    decimal ReceivedQuantity,
    string WarehouseCode,
    string QualityStatus,
    string State,
    string BatchNumber,
    DateOnly ReceivedOn);

public sealed record ReceivePurchaseOrderRequest(
    decimal ReceivedQuantity,
    string BatchNumber);

public sealed record SalesShipmentListItemDto(
    Guid Id,
    string ShipmentNumber,
    string SalesOrderNumber,
    string CustomerName,
    string ProductCode,
    decimal ShippedQuantity,
    string WarehouseCode,
    string BatchNumber,
    string State,
    DateOnly ShippedOn);

public sealed record ShipSalesOrderRequest(
    Guid SalesOrderId,
    decimal ShippedQuantity,
    string WarehouseCode);

public sealed record CustomerListItemDto(
    Guid Id,
    string Code,
    string Name,
    string Status,
    string Segment,
    string Region,
    string Industry,
    string CreditRating);

public sealed record CreateCustomerRequest(
    string Code,
    string Name,
    string Segment,
    string Region,
    string Industry,
    string CreditRating);

public sealed record SalesOrderListItemDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    string ProductCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string State,
    DateOnly OrderDate,
    DateOnly RequestedDeliveryDate,
    string? ProductionOrderNumber);

public sealed record CreateSalesOrderRequest(
    Guid CustomerId,
    string ProductCode,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly RequestedDeliveryDate);

public sealed record ProductionOrderListItemDto(
    Guid Id,
    string ProductionOrderNumber,
    string SalesOrderNumber,
    string ProductCode,
    decimal PlannedQuantity,
    string State,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    string? WorkOrderNumber);

public sealed record WorkOrderListItemDto(
    Guid Id,
    string WorkOrderNumber,
    string ProductionOrderNumber,
    string ProductCode,
    decimal PlannedQuantity,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string State,
    string CurrentOperationName,
    string IssueMode,
    bool IsMachineDriven);

public sealed record ReportWorkOrderRequest(
    decimal GoodQuantity,
    decimal ScrapQuantity,
    decimal ReworkQuantity,
    decimal LaborHours,
    decimal MachineHours,
    string? ScrapReasonCode);

public sealed record InventoryBalanceDto(
    string ItemCode,
    string ItemName,
    string WarehouseCode,
    decimal OnHandQuantity,
    decimal PendingInspectionQuantity,
    decimal ReleasedQuantity,
    decimal AverageUnitCost);

public sealed record CostLedgerItemDto(
    Guid WorkOrderId,
    string WorkOrderNumber,
    string ProductionOrderNumber,
    decimal MaterialCost,
    decimal LaborCost,
    decimal MachineCost,
    decimal OverheadCost,
    decimal ScrapCost,
    decimal TotalCost,
    decimal UnitCost);

public sealed record AccountingBridgeItemDto(
    Guid Id,
    string BridgeNumber,
    string SourceDocumentNumber,
    string SourceEvent,
    decimal Amount,
    string PostingAccount,
    string State,
    DateTimeOffset GeneratedAt);

public sealed record UpdateAccountingBridgeStateRequest(string TargetState);

public sealed record QualityInspectionItemDto(
    Guid Id,
    string InspectionNumber,
    string InspectionType,
    string WorkOrderNumber,
    string ProductionOrderNumber,
    string BatchNumber,
    decimal InspectedQuantity,
    decimal DefectQuantity,
    string State,
    string? Disposition,
    string? Inspector,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Notes);

public sealed record CompleteQualityInspectionRequest(
    string Disposition,
    decimal DefectQuantity,
    string? Notes);

public sealed record AiManagementSummaryDto(
    string Source,
    string Model,
    DateTimeOffset GeneratedAt,
    string Summary,
    IReadOnlyList<string> Warnings);

public sealed record ExternalSystemStatusDto(
    Guid Id,
    string SystemCode,
    string Name,
    string BaseUrl,
    bool IsEnabled,
    string HealthStatus,
    DateTimeOffset? LastCheckedAt);

public sealed record AccountingExportBatchDto(
    Guid Id,
    string BatchNumber,
    string TargetSystemCode,
    string State,
    int BridgeCount,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? CompletedAt);

public sealed record CreateAccountingExportBatchRequest(string TargetSystemCode);

public sealed record IntegrationJobItemDto(
    Guid Id,
    string JobNumber,
    string TargetSystemCode,
    string JobType,
    string State,
    string? RelatedBatchNumber,
    string PayloadSummary,
    DateTimeOffset QueuedAt,
    DateTimeOffset? CompletedAt,
    string? LastError);

public sealed record CompleteIntegrationJobRequest(
    string Outcome,
    string? ErrorMessage);

public interface IManufacturingRuntime
{
    SessionDto Login(LoginRequest request);
    SessionDto GetSession(string? token);
    IReadOnlyList<UserListItemDto> GetUsers(string? token);
    IReadOnlyList<AuditLogItemDto> GetAuditLogs(string? token, int take);
    DashboardSnapshotDto GetDashboard(string? token);
    IReadOnlyList<ProductListItemDto> GetProducts(string? token);
    IReadOnlyList<MaterialListItemDto> GetMaterials(string? token);
    IReadOnlyList<SupplierListItemDto> GetSuppliers(string? token);
    IReadOnlyList<PurchaseOrderListItemDto> GetPurchaseOrders(string? token);
    PurchaseOrderListItemDto CreatePurchaseOrder(string? token, CreatePurchaseOrderRequest request);
    PurchaseOrderListItemDto SubmitPurchaseOrder(string? token, Guid purchaseOrderId);
    PurchaseOrderListItemDto ApprovePurchaseOrder(string? token, Guid purchaseOrderId);
    GoodsReceiptListItemDto ReceivePurchaseOrder(string? token, Guid purchaseOrderId, ReceivePurchaseOrderRequest request);
    IReadOnlyList<GoodsReceiptListItemDto> GetGoodsReceipts(string? token);
    IReadOnlyList<CustomerListItemDto> GetCustomers(string? token);
    CustomerListItemDto CreateCustomer(string? token, CreateCustomerRequest request);
    IReadOnlyList<SalesOrderListItemDto> GetSalesOrders(string? token);
    SalesOrderListItemDto CreateSalesOrder(string? token, CreateSalesOrderRequest request);
    SalesOrderListItemDto SubmitSalesOrder(string? token, Guid salesOrderId);
    SalesOrderListItemDto ApproveSalesOrder(string? token, Guid salesOrderId);
    SalesOrderListItemDto ReleaseSalesOrder(string? token, Guid salesOrderId);
    IReadOnlyList<SalesShipmentListItemDto> GetSalesShipments(string? token);
    SalesShipmentListItemDto ShipSalesOrder(string? token, ShipSalesOrderRequest request);
    IReadOnlyList<ProductionOrderListItemDto> GetProductionOrders(string? token);
    ProductionOrderListItemDto ReleaseProductionOrderToMes(string? token, Guid productionOrderId);
    IReadOnlyList<WorkOrderListItemDto> GetWorkOrders(string? token);
    WorkOrderListItemDto DispatchWorkOrder(string? token, Guid workOrderId);
    WorkOrderListItemDto StartWorkOrder(string? token, Guid workOrderId);
    WorkOrderListItemDto ReportWorkOrder(string? token, Guid workOrderId, ReportWorkOrderRequest request);
    IReadOnlyList<InventoryBalanceDto> GetInventoryBalances(string? token);
    IReadOnlyList<CostLedgerItemDto> GetCostLedgers(string? token);
    IReadOnlyList<AccountingBridgeItemDto> GetAccountingBridges(string? token);
    AccountingBridgeItemDto UpdateAccountingBridgeState(string? token, Guid accountingBridgeId, UpdateAccountingBridgeStateRequest request);
    IReadOnlyList<QualityInspectionItemDto> GetQualityInspections(string? token);
    QualityInspectionItemDto CompleteQualityInspection(string? token, Guid inspectionId, CompleteQualityInspectionRequest request);
    AiManagementSummaryDto GenerateManagementSummary(string? token);
    IReadOnlyList<ExternalSystemStatusDto> GetExternalSystems(string? token);
    IReadOnlyList<AccountingExportBatchDto> GetAccountingExportBatches(string? token);
    AccountingExportBatchDto CreateAccountingExportBatch(string? token, CreateAccountingExportBatchRequest request);
    IntegrationJobItemDto DispatchAccountingExportBatch(string? token, Guid exportBatchId);
    IReadOnlyList<IntegrationJobItemDto> GetIntegrationJobs(string? token);
    IntegrationJobItemDto CompleteIntegrationJob(string? token, Guid integrationJobId, CompleteIntegrationJobRequest request);
}
