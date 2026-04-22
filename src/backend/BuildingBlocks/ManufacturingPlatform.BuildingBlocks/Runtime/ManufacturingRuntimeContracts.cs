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

public interface IManufacturingRuntime
{
    SessionDto Login(LoginRequest request);
    SessionDto GetSession(string? token);
    IReadOnlyList<UserListItemDto> GetUsers(string? token);
    IReadOnlyList<AuditLogItemDto> GetAuditLogs(string? token, int take);
    DashboardSnapshotDto GetDashboard(string? token);
    IReadOnlyList<ProductListItemDto> GetProducts(string? token);
    IReadOnlyList<CustomerListItemDto> GetCustomers(string? token);
    CustomerListItemDto CreateCustomer(string? token, CreateCustomerRequest request);
    IReadOnlyList<SalesOrderListItemDto> GetSalesOrders(string? token);
    SalesOrderListItemDto CreateSalesOrder(string? token, CreateSalesOrderRequest request);
    SalesOrderListItemDto SubmitSalesOrder(string? token, Guid salesOrderId);
    SalesOrderListItemDto ApproveSalesOrder(string? token, Guid salesOrderId);
    SalesOrderListItemDto ReleaseSalesOrder(string? token, Guid salesOrderId);
    IReadOnlyList<ProductionOrderListItemDto> GetProductionOrders(string? token);
    ProductionOrderListItemDto ReleaseProductionOrderToMes(string? token, Guid productionOrderId);
    IReadOnlyList<WorkOrderListItemDto> GetWorkOrders(string? token);
    WorkOrderListItemDto DispatchWorkOrder(string? token, Guid workOrderId);
    WorkOrderListItemDto StartWorkOrder(string? token, Guid workOrderId);
    WorkOrderListItemDto ReportWorkOrder(string? token, Guid workOrderId, ReportWorkOrderRequest request);
    IReadOnlyList<InventoryBalanceDto> GetInventoryBalances(string? token);
    IReadOnlyList<CostLedgerItemDto> GetCostLedgers(string? token);
    IReadOnlyList<AccountingBridgeItemDto> GetAccountingBridges(string? token);
    IReadOnlyList<QualityInspectionItemDto> GetQualityInspections(string? token);
    QualityInspectionItemDto CompleteQualityInspection(string? token, Guid inspectionId, CompleteQualityInspectionRequest request);
    AiManagementSummaryDto GenerateManagementSummary(string? token);
}
