using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Host.Persistence;
using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.ERP.Domain;
using ManufacturingPlatform.Modules.Integration.Domain;
using ManufacturingPlatform.Modules.MES.Domain;
using ManufacturingPlatform.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManufacturingPlatform.Host.Runtime;

public sealed class EfManufacturingRuntime(
    ManufacturingDbContext dbContext,
    IOptions<ManufacturingOptions> options,
    ManufacturingAiSummaryService aiSummaryService) : IManufacturingRuntime
{
    private readonly ManufacturingDbContext _dbContext = dbContext;
    private readonly ManufacturingOptions _options = options.Value;
    private readonly ManufacturingAiSummaryService _aiSummaryService = aiSummaryService;

    public SessionDto Login(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = _dbContext.Users
            .AsSplitQuery()
            .Include(entity => entity.Department)
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
                    .ThenInclude(entity => entity!.RolePermissions)
                        .ThenInclude(entity => entity.Permission)
            .SingleOrDefault(entity => entity.UserName.ToLower() == request.UserName.Trim().ToLower());

        if (user is null)
        {
            throw new RuntimeValidationException($"Unknown user '{request.UserName}'.");
        }

        var sessionEntity = new SessionEntity
        {
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
        };

        _dbContext.Sessions.Add(sessionEntity);
        AddAudit("Platform", "SessionLogin", $"User {user.DisplayName} signed in.", user.UserName, user.Id);
        _dbContext.SaveChanges();

        return MapSession(user, sessionEntity);
    }

    public SessionDto GetSession(string? token)
    {
        var session = RequireSession(token);
        return MapSession(session.User, session.Session);
    }

    public IReadOnlyList<UserListItemDto> GetUsers(string? token)
    {
        var userContext = RequirePermission(token, "platform.admin");

        var users = _dbContext.Users
            .AsNoTracking()
            .Include(entity => entity.Department)
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
            .OrderBy(entity => entity.Department!.Name)
            .ThenBy(entity => entity.DisplayName)
            .ToArray();

        AddAudit("Platform", "UserListViewed", "Viewed user directory.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return users.Select(entity => new UserListItemDto(
                entity.Id,
                entity.UserName,
                entity.DisplayName,
                entity.Department!.Name,
                entity.UserRoles.Select(link => link.Role!.Name).OrderBy(name => name).ToArray()))
            .ToArray();
    }

    public IReadOnlyList<AuditLogItemDto> GetAuditLogs(string? token, int take)
    {
        var userContext = RequirePermission(token, "platform.admin");
        var limit = Math.Clamp(take, 1, 100);

        var logs = _dbContext.AuditLogs
            .AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.OccurredAt)
            .Take(limit)
            .ToArray();

        AddAudit("Platform", "AuditTrailViewed", $"Viewed {limit} audit events.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return logs.Select(entity => new AuditLogItemDto(
                entity.Id,
                entity.OccurredAt,
                entity.Module,
                entity.ActionName,
                entity.Detail,
                entity.PerformedBy))
            .ToArray();
    }

    public DashboardSnapshotDto GetDashboard(string? token)
    {
        RequirePermission(token, "reporting.dashboard.read");

        return BuildDashboardSnapshot();
    }

    private DashboardSnapshotDto BuildDashboardSnapshot()
    {

        var activeSalesOrders = _dbContext.SalesOrders.Count(entity => entity.State != SalesOrderState.Cancelled && entity.State != SalesOrderState.Closed);
        var activeProductionOrders = _dbContext.ProductionOrders.Count(entity => entity.State != ProductionOrderState.Cancelled && entity.State != ProductionOrderState.Closed);
        var activeWorkOrders = _dbContext.WorkOrders.Count(entity =>
            entity.State == WorkOrderState.Created ||
            entity.State == WorkOrderState.Dispatched ||
            entity.State == WorkOrderState.Started ||
            entity.State == WorkOrderState.Paused);
        var openSalesAmount = _dbContext.SalesOrders
            .AsNoTracking()
            .Where(entity => entity.State != SalesOrderState.Cancelled && entity.State != SalesOrderState.Closed)
            .Select(entity => entity.TotalAmount)
            .AsEnumerable()
            .Sum();
        var reportedGoodQuantity = _dbContext.WorkOrders
            .AsNoTracking()
            .Select(entity => entity.GoodQuantity)
            .AsEnumerable()
            .Sum();

        var metrics = new DashboardMetricsDto(
            _dbContext.Customers.Count(),
            activeSalesOrders,
            activeProductionOrders,
            activeWorkOrders,
            openSalesAmount,
            reportedGoodQuantity,
            _dbContext.QualityInspections.Count(entity => entity.State == InspectionRecordState.InInspection || entity.State == InspectionRecordState.SampleCollected || entity.State == InspectionRecordState.Planned),
            _dbContext.InventoryTransactions.Select(entity => entity.ItemCode).Distinct().Count(),
            _dbContext.AccountingBridgeEntries.Count(entity => entity.State == AccountingBridgeState.ReadyForExport));

        var recentSalesOrders = _dbContext.SalesOrders
            .AsNoTracking()
            .Include(entity => entity.Customer)
            .OrderByDescending(entity => entity.OrderDate)
            .ThenByDescending(entity => entity.OrderNumber)
            .Take(5)
            .ToArray()
            .Select(MapSalesOrder)
            .ToArray();

        var activeProductionOrderDtos = _dbContext.ProductionOrders
            .AsNoTracking()
            .Where(entity => entity.State != ProductionOrderState.Cancelled && entity.State != ProductionOrderState.Closed)
            .OrderBy(entity => entity.PlannedStartDate)
            .Take(5)
            .ToArray()
            .Select(MapProductionOrder)
            .ToArray();

        var activeWorkOrderDtos = _dbContext.WorkOrders
            .AsNoTracking()
            .Where(entity =>
                entity.State == WorkOrderState.Created ||
                entity.State == WorkOrderState.Dispatched ||
                entity.State == WorkOrderState.Started ||
                entity.State == WorkOrderState.Paused)
            .OrderBy(entity => entity.WorkOrderNumber)
            .Take(5)
            .ToArray()
            .Select(MapWorkOrder)
            .ToArray();

        var activity = _dbContext.AuditLogs
            .AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.OccurredAt)
            .Take(8)
            .Select(entity => new ActivityFeedItemDto(entity.OccurredAt, entity.Module, entity.Detail))
            .ToArray();

        return new DashboardSnapshotDto(metrics, recentSalesOrders, activeProductionOrderDtos, activeWorkOrderDtos, activity);
    }

    public IReadOnlyList<ProductListItemDto> GetProducts(string? token)
    {
        RequirePermission(token, "erp.product.read");

        return _dbContext.Products
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(entity => new ProductListItemDto(entity.Id, entity.Code, entity.Name, entity.ProductFamily, entity.BaseUnitCode))
            .ToArray();
    }

    public IReadOnlyList<MaterialListItemDto> GetMaterials(string? token)
    {
        RequirePermission(token, "erp.product.read");

        return _dbContext.Materials
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(entity => new MaterialListItemDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.BaseUnitCode,
                entity.StandardUnitCost,
                entity.ReorderLevel))
            .ToArray();
    }

    public IReadOnlyList<SupplierListItemDto> GetSuppliers(string? token)
    {
        RequirePermission(token, "erp.supplier.read");

        return _dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(entity => new SupplierListItemDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Category,
                entity.TaxRegistrationNumber,
                entity.LeadTimeDays,
                entity.PaymentTerms))
            .ToArray();
    }

    public IReadOnlyList<PurchaseOrderListItemDto> GetPurchaseOrders(string? token)
    {
        RequirePermission(token, "erp.purchase-order.read");

        return _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Material)
            .Include(entity => entity.Warehouse)
            .OrderByDescending(entity => entity.OrderedOn)
            .ThenByDescending(entity => entity.OrderNumber)
            .ToArray()
            .Select(MapPurchaseOrder)
            .ToArray();
    }

    public PurchaseOrderListItemDto CreatePurchaseOrder(string? token, CreatePurchaseOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.purchase-order.write");
        var supplier = FindSupplier(request.SupplierId);
        var material = FindMaterialByCode(request.MaterialCode);
        var warehouse = FindWarehouseByCode(request.WarehouseCode);

        if (request.OrderedQuantity <= 0 || request.UnitPrice <= 0)
        {
            throw new RuntimeValidationException("Ordered quantity and unit price must be greater than zero.");
        }

        var purchaseOrder = new PurchaseOrderEntity
        {
            Id = Guid.NewGuid(),
            OrderNumber = NextPurchaseOrderNumber(),
            SupplierId = supplier.Id,
            MaterialId = material.Id,
            WarehouseId = warehouse.Id,
            State = PurchaseOrderState.Draft,
            OrderedQuantity = request.OrderedQuantity,
            ReceivedQuantity = 0m,
            UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.OrderedQuantity * request.UnitPrice, 2),
            OrderedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            ExpectedReceiptDate = request.ExpectedReceiptDate
        };

        _dbContext.PurchaseOrders.Add(purchaseOrder);
        AddAudit("ERP", "PurchaseOrderCreated", $"Created purchase order {purchaseOrder.OrderNumber} for {material.Code}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        purchaseOrder.Supplier = supplier;
        purchaseOrder.Material = material;
        purchaseOrder.Warehouse = warehouse;
        return MapPurchaseOrder(purchaseOrder);
    }

    public PurchaseOrderListItemDto SubmitPurchaseOrder(string? token, Guid purchaseOrderId)
    {
        var userContext = RequirePermission(token, "erp.purchase-order.write");
        var purchaseOrder = FindPurchaseOrder(purchaseOrderId);

        if (purchaseOrder.State != PurchaseOrderState.Draft)
        {
            throw new RuntimeValidationException("Only draft purchase orders can be submitted.");
        }

        purchaseOrder.State = PurchaseOrderState.Submitted;
        AddAudit("ERP", "PurchaseOrderSubmitted", $"Submitted purchase order {purchaseOrder.OrderNumber} for approval.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapPurchaseOrder(purchaseOrder);
    }

    public PurchaseOrderListItemDto ApprovePurchaseOrder(string? token, Guid purchaseOrderId)
    {
        var userContext = RequirePermission(token, "erp.purchase-order.approve");
        var purchaseOrder = FindPurchaseOrder(purchaseOrderId);

        if (purchaseOrder.State != PurchaseOrderState.Submitted)
        {
            throw new RuntimeValidationException("Only submitted purchase orders can be approved.");
        }

        purchaseOrder.State = PurchaseOrderState.Approved;
        AddAudit("ERP", "PurchaseOrderApproved", $"Approved purchase order {purchaseOrder.OrderNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapPurchaseOrder(purchaseOrder);
    }

    public GoodsReceiptListItemDto ReceivePurchaseOrder(string? token, Guid purchaseOrderId, ReceivePurchaseOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.goods-receipt.write");
        var purchaseOrder = FindPurchaseOrder(purchaseOrderId);

        if (purchaseOrder.State != PurchaseOrderState.Approved &&
            purchaseOrder.State != PurchaseOrderState.Released &&
            purchaseOrder.State != PurchaseOrderState.PartiallyReceived)
        {
            throw new RuntimeValidationException("Only approved, released, or partially received purchase orders can be received.");
        }

        if (request.ReceivedQuantity <= 0)
        {
            throw new RuntimeValidationException("Received quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            throw new RuntimeValidationException("A batch number is required for received material.");
        }

        var remainingQuantity = purchaseOrder.OrderedQuantity - purchaseOrder.ReceivedQuantity;
        if (request.ReceivedQuantity > remainingQuantity)
        {
            throw new RuntimeValidationException("Received quantity cannot exceed the outstanding purchase order quantity.");
        }

        var receipt = new GoodsReceiptEntity
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = NextGoodsReceiptNumber(),
            PurchaseOrderId = purchaseOrder.Id,
            WarehouseId = purchaseOrder.WarehouseId,
            State = GoodsReceiptState.Accepted,
            QualityStatus = InventoryQualityStatus.Released,
            ReceivedQuantity = request.ReceivedQuantity,
            UnitCost = purchaseOrder.UnitPrice,
            BatchNumber = request.BatchNumber.Trim(),
            ReceivedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        purchaseOrder.ReceivedQuantity += request.ReceivedQuantity;
        purchaseOrder.State = purchaseOrder.ReceivedQuantity >= purchaseOrder.OrderedQuantity
            ? PurchaseOrderState.Completed
            : PurchaseOrderState.PartiallyReceived;

        _dbContext.GoodsReceipts.Add(receipt);
        _dbContext.InventoryTransactions.Add(new InventoryTransactionEntity
        {
            Id = Guid.NewGuid(),
            TransactionNumber = NextInventoryTransactionNumber(),
            TransactionType = InventoryTransactionType.GoodsReceipt,
            WarehouseId = purchaseOrder.WarehouseId,
            ItemCode = purchaseOrder.Material!.Code,
            Quantity = request.ReceivedQuantity,
            BatchNumber = receipt.BatchNumber,
            UnitCost = purchaseOrder.UnitPrice,
            TotalCost = decimal.Round(request.ReceivedQuantity * purchaseOrder.UnitPrice, 2),
            QualityStatus = InventoryQualityStatus.Released,
            SourceDocumentNumber = receipt.ReceiptNumber,
            SourceEvent = "Approved purchase receipt",
            OccurredAt = DateTimeOffset.UtcNow
        });

        _dbContext.AccountingBridgeEntries.Add(
            AccountingBridge(receipt.ReceiptNumber, "Purchase receipt accrual", decimal.Round(request.ReceivedQuantity * purchaseOrder.UnitPrice, 2), "AP-GRNI"));

        AddAudit("ERP", "GoodsReceiptCreated", $"Received {request.ReceivedQuantity} of {purchaseOrder.Material.Code} on {receipt.ReceiptNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        receipt.PurchaseOrder = purchaseOrder;
        receipt.Warehouse = purchaseOrder.Warehouse;
        return MapGoodsReceipt(receipt);
    }

    public IReadOnlyList<GoodsReceiptListItemDto> GetGoodsReceipts(string? token)
    {
        RequirePermission(token, "erp.purchase-order.read");

        return _dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(entity => entity.PurchaseOrder)
                .ThenInclude(entity => entity!.Supplier)
            .Include(entity => entity.PurchaseOrder)
                .ThenInclude(entity => entity!.Material)
            .Include(entity => entity.Warehouse)
            .OrderByDescending(entity => entity.ReceivedOn)
            .ThenByDescending(entity => entity.ReceiptNumber)
            .ToArray()
            .Select(MapGoodsReceipt)
            .ToArray();
    }

    public IReadOnlyList<CustomerListItemDto> GetCustomers(string? token)
    {
        RequirePermission(token, "crm.customer.read");

        return _dbContext.Customers
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(MapCustomer)
            .ToArray();
    }

    public CustomerListItemDto CreateCustomer(string? token, CreateCustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "crm.customer.write");

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RuntimeValidationException("Customer code and name are required.");
        }

        if (_dbContext.Customers.Any(entity => entity.Code.ToLower() == request.Code.Trim().ToLower()))
        {
            throw new RuntimeValidationException($"Customer code '{request.Code}' already exists.");
        }

        if (!Enum.TryParse<CustomerSegment>(request.Segment, true, out var segment))
        {
            throw new RuntimeValidationException($"Unsupported customer segment '{request.Segment}'.");
        }

        var customer = new CustomerEntity
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Status = CustomerStatus.Active,
            Segment = segment,
            Region = request.Region.Trim(),
            Industry = request.Industry.Trim(),
            CreditRating = request.CreditRating.Trim()
        };

        _dbContext.Customers.Add(customer);
        AddAudit("CRM", "CustomerCreated", $"Created customer {customer.Code} - {customer.Name}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapCustomer(customer);
    }

    public IReadOnlyList<SalesOrderListItemDto> GetSalesOrders(string? token)
    {
        RequirePermission(token, "crm.sales-order.read");

        return _dbContext.SalesOrders
            .AsNoTracking()
            .Include(entity => entity.Customer)
            .OrderByDescending(entity => entity.OrderDate)
            .ThenByDescending(entity => entity.OrderNumber)
            .ToArray()
            .Select(MapSalesOrder)
            .ToArray();
    }

    public SalesOrderListItemDto CreateSalesOrder(string? token, CreateSalesOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "crm.sales-order.write");
        var customer = FindCustomer(request.CustomerId);
        var product = FindProductByCode(request.ProductCode);

        if (request.Quantity <= 0 || request.UnitPrice <= 0)
        {
            throw new RuntimeValidationException("Quantity and unit price must be greater than zero.");
        }

        var salesOrder = new SalesOrderEntity
        {
            Id = Guid.NewGuid(),
            OrderNumber = NextSalesOrderNumber(),
            CustomerId = customer.Id,
            ProductCode = product.Code,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalAmount = decimal.Round(request.Quantity * request.UnitPrice, 2),
            State = SalesOrderState.Draft,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            RequestedDeliveryDate = request.RequestedDeliveryDate
        };

        _dbContext.SalesOrders.Add(salesOrder);
        AddAudit("CRM", "SalesOrderCreated", $"Created sales order {salesOrder.OrderNumber} for {customer.Name}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        salesOrder.Customer = customer;
        return MapSalesOrder(salesOrder);
    }

    public SalesOrderListItemDto SubmitSalesOrder(string? token, Guid salesOrderId)
    {
        var userContext = RequirePermission(token, "crm.sales-order.write");
        var salesOrder = FindSalesOrder(salesOrderId);

        if (salesOrder.State != SalesOrderState.Draft)
        {
            throw new RuntimeValidationException("Only draft sales orders can be submitted.");
        }

        salesOrder.State = SalesOrderState.Submitted;
        AddAudit("CRM", "SalesOrderSubmitted", $"Submitted sales order {salesOrder.OrderNumber} for approval.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        salesOrder.Customer = FindCustomer(salesOrder.CustomerId);
        return MapSalesOrder(salesOrder);
    }

    public SalesOrderListItemDto ApproveSalesOrder(string? token, Guid salesOrderId)
    {
        var userContext = RequirePermission(token, "crm.sales-order.approve");
        var salesOrder = FindSalesOrder(salesOrderId);

        if (salesOrder.State != SalesOrderState.Submitted)
        {
            throw new RuntimeValidationException("Only submitted sales orders can be approved.");
        }

        salesOrder.State = SalesOrderState.Approved;
        AddAudit("CRM", "SalesOrderApproved", $"Approved sales order {salesOrder.OrderNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        salesOrder.Customer = FindCustomer(salesOrder.CustomerId);
        return MapSalesOrder(salesOrder);
    }

    public SalesOrderListItemDto ReleaseSalesOrder(string? token, Guid salesOrderId)
    {
        var userContext = RequirePermission(token, "crm.sales-order.release");
        var salesOrder = FindSalesOrder(salesOrderId);

        if (salesOrder.State != SalesOrderState.Approved)
        {
            throw new RuntimeValidationException("Only approved sales orders can be released.");
        }

        salesOrder.State = SalesOrderState.Released;

        if (!salesOrder.ProductionOrderId.HasValue)
        {
            var productionOrder = new ProductionOrderEntity
            {
                Id = Guid.NewGuid(),
                ProductionOrderNumber = NextProductionOrderNumber(),
                SalesOrderId = salesOrder.Id,
                State = ProductionOrderState.Approved,
                ProductCode = salesOrder.ProductCode,
                PlannedQuantity = salesOrder.Quantity,
                PlannedStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
                PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4)
            };

            _dbContext.ProductionOrders.Add(productionOrder);
            salesOrder.ProductionOrderId = productionOrder.Id;

            AddAudit(
                "ERP",
                "ProductionOrderCreated",
                $"Created production order {productionOrder.ProductionOrderNumber} from sales order {salesOrder.OrderNumber}.",
                userContext.UserName,
                userContext.UserId);
        }

        AddAudit("CRM", "SalesOrderReleased", $"Released sales order {salesOrder.OrderNumber} to ERP fulfillment.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        salesOrder.Customer = FindCustomer(salesOrder.CustomerId);
        return MapSalesOrder(salesOrder);
    }

    public IReadOnlyList<SalesShipmentListItemDto> GetSalesShipments(string? token)
    {
        RequirePermission(token, "erp.shipment.read");

        return _dbContext.SalesShipments
            .AsNoTracking()
            .Include(entity => entity.SalesOrder)
                .ThenInclude(entity => entity!.Customer)
            .Include(entity => entity.Warehouse)
            .OrderByDescending(entity => entity.ShippedOn)
            .ThenByDescending(entity => entity.ShipmentNumber)
            .ToArray()
            .Select(MapShipment)
            .ToArray();
    }

    public SalesShipmentListItemDto ShipSalesOrder(string? token, ShipSalesOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.shipment.write");
        var salesOrder = FindSalesOrder(request.SalesOrderId);
        var warehouse = FindWarehouseByCode(request.WarehouseCode);

        if (salesOrder.State != SalesOrderState.Released &&
            salesOrder.State != SalesOrderState.PartiallyFulfilled)
        {
            throw new RuntimeValidationException("Only released or partially fulfilled sales orders can be shipped.");
        }

        if (request.ShippedQuantity <= 0)
        {
            throw new RuntimeValidationException("Shipped quantity must be greater than zero.");
        }

        var shippedSoFar = _dbContext.SalesShipments
            .Where(entity => entity.SalesOrderId == salesOrder.Id && entity.State != SalesShipmentState.Cancelled)
            .Select(entity => entity.ShippedQuantity)
            .AsEnumerable()
            .Sum();

        var remainingDemand = salesOrder.Quantity - shippedSoFar;
        if (request.ShippedQuantity > remainingDemand)
        {
            throw new RuntimeValidationException("Shipment quantity cannot exceed the outstanding sales order quantity.");
        }

        var batchBalances = _dbContext.InventoryTransactions
            .AsNoTracking()
            .Where(entity =>
                entity.WarehouseId == warehouse.Id &&
                entity.ItemCode == salesOrder.ProductCode &&
                (entity.QualityStatus == InventoryQualityStatus.Released || entity.QualityStatus == InventoryQualityStatus.ConditionalRelease))
            .AsEnumerable()
            .GroupBy(entity => entity.BatchNumber ?? "UNBATCHED")
            .Select(group => new
            {
                BatchNumber = group.Key,
                Quantity = group.Sum(entity => entity.Quantity),
                UnitCost = group.Where(entity => entity.Quantity > 0).Select(entity => entity.UnitCost).DefaultIfEmpty(0m).Average()
            })
            .Where(group => group.Quantity > 0)
            .OrderByDescending(group => group.Quantity)
            .ToArray();

        var batch = batchBalances.FirstOrDefault(group => group.Quantity >= request.ShippedQuantity);
        if (batch is null)
        {
            throw new RuntimeValidationException("No released finished-goods batch has enough available quantity for this shipment.");
        }

        var shipment = new SalesShipmentEntity
        {
            Id = Guid.NewGuid(),
            ShipmentNumber = NextSalesShipmentNumber(),
            SalesOrderId = salesOrder.Id,
            WarehouseId = warehouse.Id,
            State = SalesShipmentState.Shipped,
            ShippedQuantity = request.ShippedQuantity,
            BatchNumber = batch.BatchNumber,
            UnitPrice = salesOrder.UnitPrice,
            ShippedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var shipmentValue = decimal.Round(request.ShippedQuantity * salesOrder.UnitPrice, 2);
        var shipmentCost = decimal.Round(request.ShippedQuantity * batch.UnitCost, 2);

        _dbContext.SalesShipments.Add(shipment);
        _dbContext.InventoryTransactions.Add(new InventoryTransactionEntity
        {
            Id = Guid.NewGuid(),
            TransactionNumber = NextInventoryTransactionNumber(),
            TransactionType = InventoryTransactionType.SalesShipment,
            WarehouseId = warehouse.Id,
            ItemCode = salesOrder.ProductCode,
            Quantity = -request.ShippedQuantity,
            BatchNumber = batch.BatchNumber,
            UnitCost = batch.UnitCost,
            TotalCost = -shipmentCost,
            QualityStatus = InventoryQualityStatus.Released,
            SourceDocumentNumber = shipment.ShipmentNumber,
            SourceEvent = "Customer shipment",
            OccurredAt = DateTimeOffset.UtcNow
        });

        var shipmentBridgeSequence = NextNumberSequence(
            "BR",
            _dbContext.AccountingBridgeEntries.AsNoTracking().Select(entity => entity.BridgeNumber).AsEnumerable(),
            _dbContext.AccountingBridgeEntries.Local.Select(entity => entity.BridgeNumber));

        _dbContext.AccountingBridgeEntries.AddRange(
            AccountingBridge(shipment.ShipmentNumber, "Sales shipment revenue", shipmentValue, "AR-SHIP", shipmentBridgeSequence),
            AccountingBridge(shipment.ShipmentNumber, "Sales shipment COGS", shipmentCost, "COGS", shipmentBridgeSequence + 1));

        var totalShipped = shippedSoFar + request.ShippedQuantity;
        salesOrder.State = totalShipped >= salesOrder.Quantity
            ? SalesOrderState.Completed
            : SalesOrderState.PartiallyFulfilled;

        AddAudit("ERP", "SalesShipmentCreated", $"Shipped {request.ShippedQuantity} of {salesOrder.ProductCode} on {shipment.ShipmentNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        shipment.SalesOrder = salesOrder;
        shipment.Warehouse = warehouse;
        return MapShipment(shipment);
    }

    public IReadOnlyList<ProductionOrderListItemDto> GetProductionOrders(string? token)
    {
        RequirePermission(token, "erp.production-order.read");

        return _dbContext.ProductionOrders
            .AsNoTracking()
            .Include(entity => entity.SalesOrder)
            .OrderBy(entity => entity.PlannedStartDate)
            .ThenBy(entity => entity.ProductionOrderNumber)
            .ToArray()
            .Select(MapProductionOrder)
            .ToArray();
    }

    public ProductionOrderListItemDto ReleaseProductionOrderToMes(string? token, Guid productionOrderId)
    {
        var userContext = RequirePermission(token, "erp.production-order.release");
        var productionOrder = FindProductionOrder(productionOrderId);

        if (productionOrder.State != ProductionOrderState.Approved)
        {
            throw new RuntimeValidationException("Only approved production orders can be released to MES.");
        }

        productionOrder.State = ProductionOrderState.ReleasedToMes;

        if (!productionOrder.WorkOrderId.HasValue)
        {
            var workOrder = new WorkOrderEntity
            {
                Id = Guid.NewGuid(),
                WorkOrderNumber = NextWorkOrderNumber(),
                ProductionOrderId = productionOrder.Id,
                State = WorkOrderState.Created,
                ProductCode = productionOrder.ProductCode,
                PlannedQuantity = productionOrder.PlannedQuantity,
                GoodQuantity = 0m,
                ScrapQuantity = 0m,
                ReworkQuantity = 0m,
                LaborHours = 0m,
                MachineHours = 0m,
                CurrentOperationName = "Cell Stringing",
                CurrentOperationSequence = 10,
                IssueMode = IssueMode.Backflush,
                IsMachineDriven = true
            };

            productionOrder.WorkOrderId = workOrder.Id;
            _dbContext.WorkOrders.Add(workOrder);
            _dbContext.WorkOrderOperations.AddRange(CreateOperations(workOrder.Id));

            AddAudit("MES", "WorkOrderCreated", $"Created work order {workOrder.WorkOrderNumber} from production order {productionOrder.ProductionOrderNumber}.", userContext.UserName, userContext.UserId);
        }

        AddAudit("ERP", "ProductionOrderReleased", $"Released production order {productionOrder.ProductionOrderNumber} to MES.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        productionOrder.SalesOrder = FindSalesOrder(productionOrder.SalesOrderId);
        return MapProductionOrder(productionOrder);
    }

    public IReadOnlyList<WorkOrderListItemDto> GetWorkOrders(string? token)
    {
        RequirePermission(token, "mes.work-order.read");

        return _dbContext.WorkOrders
            .AsNoTracking()
            .Include(entity => entity.ProductionOrder)
            .OrderBy(entity => entity.WorkOrderNumber)
            .ToArray()
            .Select(MapWorkOrder)
            .ToArray();
    }

    public WorkOrderListItemDto DispatchWorkOrder(string? token, Guid workOrderId)
    {
        var userContext = RequirePermission(token, "mes.work-order.execute");
        var workOrder = FindWorkOrder(workOrderId);

        if (workOrder.State != WorkOrderState.Created)
        {
            throw new RuntimeValidationException("Only created work orders can be dispatched.");
        }

        workOrder.State = WorkOrderState.Dispatched;

        var firstOperation = _dbContext.WorkOrderOperations
            .Where(entity => entity.WorkOrderId == workOrder.Id)
            .OrderBy(entity => entity.Sequence)
            .First();

        firstOperation.State = OperationExecutionState.Dispatched;
        workOrder.CurrentOperationName = firstOperation.OperationName;
        workOrder.CurrentOperationSequence = firstOperation.Sequence;
        workOrder.IssueMode = firstOperation.IssueMode;
        workOrder.IsMachineDriven = firstOperation.IsMachineDriven;

        AddAudit("MES", "WorkOrderDispatched", $"Dispatched work order {workOrder.WorkOrderNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapWorkOrder(workOrder);
    }

    public WorkOrderListItemDto StartWorkOrder(string? token, Guid workOrderId)
    {
        var userContext = RequirePermission(token, "mes.work-order.execute");
        var workOrder = FindWorkOrder(workOrderId);

        if (workOrder.State != WorkOrderState.Dispatched && workOrder.State != WorkOrderState.Paused)
        {
            throw new RuntimeValidationException("Only dispatched or paused work orders can be started.");
        }

        workOrder.State = WorkOrderState.Started;

        var currentOperation = _dbContext.WorkOrderOperations
            .Where(entity => entity.WorkOrderId == workOrder.Id && entity.Sequence == workOrder.CurrentOperationSequence)
            .SingleOrDefault()
            ?? _dbContext.WorkOrderOperations.Where(entity => entity.WorkOrderId == workOrder.Id).OrderBy(entity => entity.Sequence).First();

        currentOperation.State = OperationExecutionState.Started;
        workOrder.CurrentOperationName = currentOperation.OperationName;
        workOrder.CurrentOperationSequence = currentOperation.Sequence;
        workOrder.IssueMode = currentOperation.IssueMode;
        workOrder.IsMachineDriven = currentOperation.IsMachineDriven;

        var productionOrder = FindProductionOrder(workOrder.ProductionOrderId);
        productionOrder.State = ProductionOrderState.InExecution;

        AddAudit("MES", "WorkOrderStarted", $"Started work order {workOrder.WorkOrderNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapWorkOrder(workOrder);
    }

    public WorkOrderListItemDto ReportWorkOrder(string? token, Guid workOrderId, ReportWorkOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "mes.work-order.execute");
        var workOrder = FindWorkOrder(workOrderId);
        var productionOrder = FindProductionOrder(workOrder.ProductionOrderId);
        var product = FindProductByCode(workOrder.ProductCode);
        var warehouse = _dbContext.Warehouses.Single(entity => entity.Code == "FG-01");
        var rawMaterialWarehouse = _dbContext.Warehouses.Single(entity => entity.Code == "RM-01");

        if (workOrder.State != WorkOrderState.Started)
        {
            throw new RuntimeValidationException("Only started work orders can be reported.");
        }

        if (request.GoodQuantity < 0 || request.ScrapQuantity < 0 || request.ReworkQuantity < 0)
        {
            throw new RuntimeValidationException("Reported quantities cannot be negative.");
        }

        var processedQuantity = request.GoodQuantity + request.ScrapQuantity + request.ReworkQuantity;

        if (processedQuantity <= 0)
        {
            throw new RuntimeValidationException("A production report must contain a positive processed quantity.");
        }

        if (processedQuantity > workOrder.PlannedQuantity)
        {
            throw new RuntimeValidationException("Reported quantity cannot exceed the planned work order quantity.");
        }

        workOrder.GoodQuantity = request.GoodQuantity;
        workOrder.ScrapQuantity = request.ScrapQuantity;
        workOrder.ReworkQuantity = request.ReworkQuantity;
        workOrder.LaborHours = request.LaborHours;
        workOrder.MachineHours = request.MachineHours;
        workOrder.State = WorkOrderState.Reported;
        workOrder.CurrentOperationName = "Quality Inspection";
        workOrder.CurrentOperationSequence = 99;
        workOrder.FinishedGoodsBatchNumber ??= $"FG-{DateTime.UtcNow:yyyyMMdd}-{product.Code}";

        var operations = _dbContext.WorkOrderOperations.Where(entity => entity.WorkOrderId == workOrder.Id).ToArray();
        foreach (var operation in operations)
        {
            operation.State = OperationExecutionState.Completed;
        }

        productionOrder.State = ProductionOrderState.TechnicallyCompleted;

        var bomComponents = _dbContext.BomComponents
            .AsNoTracking()
            .Include(entity => entity.Material)
            .Where(entity => entity.ProductId == product.Id)
            .ToArray();

        var materialCost = 0m;
        foreach (var component in bomComponents)
        {
            var issueQuantity = decimal.Round(component.QuantityPerUnit * processedQuantity, 2);
            var issueCost = decimal.Round(issueQuantity * component.Material!.StandardUnitCost, 2);
            materialCost += issueCost;

            _dbContext.InventoryTransactions.Add(new InventoryTransactionEntity
            {
                Id = Guid.NewGuid(),
                TransactionNumber = NextInventoryTransactionNumber(),
                TransactionType = InventoryTransactionType.ProductionIssue,
                WarehouseId = rawMaterialWarehouse.Id,
                ItemCode = component.Material.Code,
                Quantity = -issueQuantity,
                BatchNumber = $"LOT-{component.Material.Code}-{DateTime.UtcNow:yyyyMMdd}",
                UnitCost = component.Material.StandardUnitCost,
                TotalCost = -issueCost,
                QualityStatus = InventoryQualityStatus.Released,
                SourceDocumentNumber = workOrder.WorkOrderNumber,
                SourceEvent = "MES report backflush",
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        var laborCost = decimal.Round(request.LaborHours * _options.Costing.LaborRatePerHour, 2);
        var machineCost = decimal.Round(request.MachineHours * _options.Costing.MachineRatePerHour, 2);
        var overheadCost = decimal.Round(processedQuantity * (product.OverheadRatePerUnit > 0 ? product.OverheadRatePerUnit : _options.Costing.DefaultOverheadRatePerUnit), 2);
        var scrapCost = request.ScrapQuantity > 0
            ? decimal.Round((materialCost + laborCost + machineCost + overheadCost) * (request.ScrapQuantity / processedQuantity), 2)
            : 0m;

        var totalCost = materialCost + laborCost + machineCost + overheadCost + scrapCost;
        var unitCost = request.GoodQuantity > 0 ? decimal.Round(totalCost / request.GoodQuantity, 2) : 0m;

        _dbContext.CostPostings.AddRange(
            CostPosting(workOrder, productionOrder, "Material", materialCost, "Actual material issue"),
            CostPosting(workOrder, productionOrder, "Labor", laborCost, "Reported labor hours"),
            CostPosting(workOrder, productionOrder, "Machine", machineCost, "Reported machine hours"),
            CostPosting(workOrder, productionOrder, "Overhead", overheadCost, "Applied overhead"),
            CostPosting(workOrder, productionOrder, "Scrap", scrapCost, request.ScrapReasonCode ?? "Scrap accrual"));

        if (request.GoodQuantity > 0)
        {
            _dbContext.InventoryTransactions.Add(new InventoryTransactionEntity
            {
                Id = Guid.NewGuid(),
                TransactionNumber = NextInventoryTransactionNumber(),
                TransactionType = InventoryTransactionType.ProductionReceipt,
                WarehouseId = warehouse.Id,
                ItemCode = product.Code,
                Quantity = request.GoodQuantity,
                BatchNumber = workOrder.FinishedGoodsBatchNumber,
                UnitCost = unitCost,
                TotalCost = decimal.Round(request.GoodQuantity * unitCost, 2),
                QualityStatus = InventoryQualityStatus.PendingInspection,
                SourceDocumentNumber = workOrder.WorkOrderNumber,
                SourceEvent = "MES completion report",
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        _dbContext.QualityInspections.Add(new QualityInspectionEntity
        {
            Id = Guid.NewGuid(),
            InspectionNumber = NextInspectionNumber(),
            InspectionType = InspectionType.Final,
            WorkOrderId = workOrder.Id,
            ProductionOrderId = productionOrder.Id,
            BatchNumber = workOrder.FinishedGoodsBatchNumber ?? "FG-UNKNOWN",
            InspectedQuantity = request.GoodQuantity,
            DefectQuantity = request.ScrapQuantity,
            State = InspectionRecordState.InInspection,
            Disposition = null,
            InspectorUserName = null,
            Notes = "Awaiting quality disposition after MES completion report.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var workOrderBridgeSequence = NextNumberSequence(
            "BR",
            _dbContext.AccountingBridgeEntries.AsNoTracking().Select(entity => entity.BridgeNumber).AsEnumerable(),
            _dbContext.AccountingBridgeEntries.Local.Select(entity => entity.BridgeNumber));

        _dbContext.AccountingBridgeEntries.AddRange(
            AccountingBridge(workOrder.WorkOrderNumber, "WIP consumption", materialCost + laborCost + machineCost + overheadCost, "WIP-MFG", workOrderBridgeSequence),
            AccountingBridge(workOrder.WorkOrderNumber, "FG receipt pending inspection", decimal.Round(request.GoodQuantity * unitCost, 2), "FG-READY", workOrderBridgeSequence + 1),
            AccountingBridge(workOrder.WorkOrderNumber, "Scrap loss", scrapCost, "SCRAP-LOSS", workOrderBridgeSequence + 2));

        AddAudit("MES", "WorkOrderReported", $"Reported work order {workOrder.WorkOrderNumber}: good {request.GoodQuantity}, scrap {request.ScrapQuantity}.", userContext.UserName, userContext.UserId);
        AddAudit("ERP", "CostPosted", $"Posted actual cost entries for work order {workOrder.WorkOrderNumber}.", userContext.UserName, userContext.UserId);
        AddAudit("Quality", "InspectionCreated", $"Created final inspection for batch {workOrder.FinishedGoodsBatchNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapWorkOrder(workOrder);
    }

    public IReadOnlyList<InventoryBalanceDto> GetInventoryBalances(string? token)
    {
        RequirePermission(token, "erp.inventory.read");

        var materials = _dbContext.Materials.AsNoTracking().ToDictionary(entity => entity.Code, entity => entity.Name, StringComparer.OrdinalIgnoreCase);
        var products = _dbContext.Products.AsNoTracking().ToDictionary(entity => entity.Code, entity => entity.Name, StringComparer.OrdinalIgnoreCase);

        return _dbContext.InventoryTransactions
            .AsNoTracking()
            .Include(entity => entity.Warehouse)
            .AsEnumerable()
            .GroupBy(entity => new { entity.ItemCode, WarehouseCode = entity.Warehouse!.Code })
            .Select(group =>
            {
                var onHand = group.Sum(entity => entity.Quantity);
                var pendingInspection = group.Where(entity => entity.QualityStatus == InventoryQualityStatus.PendingInspection).Sum(entity => entity.Quantity);
                var released = group.Where(entity => entity.QualityStatus == InventoryQualityStatus.Released || entity.QualityStatus == InventoryQualityStatus.ConditionalRelease).Sum(entity => entity.Quantity);
                var averageUnitCost = group.Where(entity => entity.Quantity > 0).DefaultIfEmpty().Average(entity => entity?.UnitCost ?? 0m);
                var itemName = materials.TryGetValue(group.Key.ItemCode, out var materialName)
                    ? materialName
                    : products.GetValueOrDefault(group.Key.ItemCode, group.Key.ItemCode);

                return new InventoryBalanceDto(group.Key.ItemCode, itemName, group.Key.WarehouseCode, onHand, pendingInspection, released, decimal.Round(averageUnitCost, 2));
            })
            .OrderBy(item => item.ItemCode)
            .ToArray();
    }

    public IReadOnlyList<CostLedgerItemDto> GetCostLedgers(string? token)
    {
        RequirePermission(token, "erp.cost.read");

        var workOrders = _dbContext.WorkOrders
            .AsNoTracking()
            .Include(entity => entity.ProductionOrder)
            .ToDictionary(entity => entity.Id);

        return _dbContext.CostPostings
            .AsNoTracking()
            .AsEnumerable()
            .GroupBy(entity => entity.WorkOrderId)
            .Select(group =>
            {
                var workOrder = workOrders[group.Key];
                var materialCost = group.Where(entity => entity.PostingType == "Material").Sum(entity => entity.Amount);
                var laborCost = group.Where(entity => entity.PostingType == "Labor").Sum(entity => entity.Amount);
                var machineCost = group.Where(entity => entity.PostingType == "Machine").Sum(entity => entity.Amount);
                var overheadCost = group.Where(entity => entity.PostingType == "Overhead").Sum(entity => entity.Amount);
                var scrapCost = group.Where(entity => entity.PostingType == "Scrap").Sum(entity => entity.Amount);
                var totalCost = materialCost + laborCost + machineCost + overheadCost + scrapCost;
                var unitCost = workOrder.GoodQuantity > 0 ? decimal.Round(totalCost / workOrder.GoodQuantity, 2) : 0m;

                return new CostLedgerItemDto(
                    workOrder.Id,
                    workOrder.WorkOrderNumber,
                    workOrder.ProductionOrder!.ProductionOrderNumber,
                    materialCost,
                    laborCost,
                    machineCost,
                    overheadCost,
                    scrapCost,
                    totalCost,
                    unitCost);
            })
            .OrderByDescending(entity => entity.WorkOrderNumber)
            .ToArray();
    }

    public IReadOnlyList<AccountingBridgeItemDto> GetAccountingBridges(string? token)
    {
        RequirePermission(token, "erp.cost.read");

        return ListAccountingBridges();
    }

    private IReadOnlyList<AccountingBridgeItemDto> ListAccountingBridges()
    {
        return _dbContext.AccountingBridgeEntries
            .AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.GeneratedAt)
            .Select(entity => new AccountingBridgeItemDto(
                entity.Id,
                entity.BridgeNumber,
                entity.SourceDocumentNumber,
                entity.SourceEvent,
                entity.Amount,
                entity.PostingAccount,
                entity.State.ToString(),
                entity.GeneratedAt))
            .ToArray();
    }

    public AccountingBridgeItemDto UpdateAccountingBridgeState(string? token, Guid accountingBridgeId, UpdateAccountingBridgeStateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.accounting.manage");
        var bridge = _dbContext.AccountingBridgeEntries.SingleOrDefault(entity => entity.Id == accountingBridgeId)
            ?? throw new RuntimeNotFoundException($"Accounting bridge '{accountingBridgeId}' was not found.");

        if (!Enum.TryParse<AccountingBridgeState>(request.TargetState, true, out var targetState))
        {
            throw new RuntimeValidationException($"Unsupported accounting bridge state '{request.TargetState}'.");
        }

        if (!IsAllowedAccountingBridgeTransition(bridge.State, targetState))
        {
            throw new RuntimeValidationException($"Cannot move accounting bridge from {bridge.State} to {targetState}.");
        }

        bridge.State = targetState;
        AddAudit("ERP", "AccountingBridgeUpdated", $"Moved accounting bridge {bridge.BridgeNumber} to {targetState}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingBridge(bridge);
    }

    public IReadOnlyList<QualityInspectionItemDto> GetQualityInspections(string? token)
    {
        RequirePermission(token, "quality.inspection.read");

        return ListQualityInspections();
    }

    private IReadOnlyList<QualityInspectionItemDto> ListQualityInspections()
    {
        return _dbContext.QualityInspections
            .AsNoTracking()
            .Include(entity => entity.WorkOrder)
            .Include(entity => entity.ProductionOrder)
            .AsEnumerable()
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(MapInspection)
            .ToArray();
    }

    public QualityInspectionItemDto CompleteQualityInspection(string? token, Guid inspectionId, CompleteQualityInspectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "quality.inspection.write");
        var inspection = _dbContext.QualityInspections
            .Include(entity => entity.WorkOrder)
            .Include(entity => entity.ProductionOrder)
            .SingleOrDefault(entity => entity.Id == inspectionId)
            ?? throw new RuntimeNotFoundException($"Quality inspection '{inspectionId}' was not found.");

        if (inspection.State != InspectionRecordState.InInspection && inspection.State != InspectionRecordState.SampleCollected && inspection.State != InspectionRecordState.Planned)
        {
            throw new RuntimeValidationException("Only open inspections can be completed.");
        }

        if (!Enum.TryParse<QualityDisposition>(request.Disposition, true, out var disposition))
        {
            throw new RuntimeValidationException($"Unsupported disposition '{request.Disposition}'.");
        }

        inspection.Disposition = disposition;
        inspection.DefectQuantity = request.DefectQuantity;
        inspection.InspectorUserName = userContext.UserName;
        inspection.Notes = request.Notes?.Trim();
        inspection.CompletedAt = DateTimeOffset.UtcNow;
        inspection.State = disposition switch
        {
            QualityDisposition.Release => InspectionRecordState.Accepted,
            QualityDisposition.ConditionalRelease => InspectionRecordState.ConditionalRelease,
            _ => InspectionRecordState.Rejected
        };

        var productionReceiptTransactions = _dbContext.InventoryTransactions
            .Where(entity =>
                entity.SourceDocumentNumber == inspection.WorkOrder!.WorkOrderNumber &&
                entity.TransactionType == InventoryTransactionType.ProductionReceipt &&
                entity.BatchNumber == inspection.BatchNumber)
            .ToArray();

        var targetQualityStatus = disposition switch
        {
            QualityDisposition.Release => InventoryQualityStatus.Released,
            QualityDisposition.ConditionalRelease => InventoryQualityStatus.ConditionalRelease,
            QualityDisposition.Hold or QualityDisposition.Rework => InventoryQualityStatus.Quarantined,
            _ => InventoryQualityStatus.Rejected
        };

        foreach (var transaction in productionReceiptTransactions)
        {
            transaction.QualityStatus = targetQualityStatus;
        }

        if (disposition == QualityDisposition.Release || disposition == QualityDisposition.ConditionalRelease)
        {
            inspection.WorkOrder!.State = WorkOrderState.Closed;
            inspection.ProductionOrder!.State = ProductionOrderState.Costed;
        }
        else if (disposition == QualityDisposition.Rework || disposition == QualityDisposition.Hold)
        {
            inspection.WorkOrder!.State = WorkOrderState.Paused;
            inspection.ProductionOrder!.State = ProductionOrderState.InExecution;
        }

        AddAudit("Quality", "InspectionCompleted", $"Completed inspection {inspection.InspectionNumber} with disposition {disposition}.", userContext.UserName, userContext.UserId);
        AddAudit("ERP", "InventoryQualityUpdated", $"Updated batch {inspection.BatchNumber} to inventory quality state {targetQualityStatus}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapInspection(inspection);
    }

    public AiManagementSummaryDto GenerateManagementSummary(string? token)
    {
        var userContext = RequirePermission(token, "ai.summary.read");
        var dashboard = BuildDashboardSnapshot();
        var inspections = ListQualityInspections();
        var accountingBridges = ListAccountingBridges();

        var summary = _aiSummaryService.GenerateSummary(
            dashboard.Metrics,
            dashboard.ActiveProductionOrders,
            inspections.Take(6).ToArray(),
            accountingBridges.Take(6).ToArray());

        _dbContext.AiInsightRuns.Add(new AiInsightRunEntity
        {
            Id = Guid.NewGuid(),
            InsightType = "ManagementSummary",
            Source = summary.Source,
            Model = summary.Model,
            Summary = summary.Summary,
            ErrorMessage = summary.Warnings.Count > 0 ? string.Join(" | ", summary.Warnings) : null,
            GeneratedAt = summary.GeneratedAt
        });

        AddAudit("AI", "ManagementSummaryGenerated", $"Generated AI management summary using {summary.Source}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return summary;
    }

    public IReadOnlyList<ExternalSystemStatusDto> GetExternalSystems(string? token)
    {
        RequirePermission(token, "integration.read");

        return _dbContext.ExternalSystems
            .AsNoTracking()
            .OrderBy(entity => entity.SystemCode)
            .Select(entity => new ExternalSystemStatusDto(
                entity.Id,
                entity.SystemCode,
                entity.Name,
                entity.BaseUrl,
                entity.IsEnabled,
                entity.HealthStatus.ToString(),
                entity.LastCheckedAt))
            .ToArray();
    }

    public IReadOnlyList<AccountingExportBatchDto> GetAccountingExportBatches(string? token)
    {
        RequirePermission(token, "integration.read");

        return _dbContext.AccountingExportBatches
            .AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(MapAccountingExportBatch)
            .ToArray();
    }

    public AccountingExportBatchDto CreateAccountingExportBatch(string? token, CreateAccountingExportBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "integration.manage");
        var externalSystem = FindExternalSystem(request.TargetSystemCode);

        if (!externalSystem.IsEnabled)
        {
            throw new RuntimeValidationException($"External system '{externalSystem.SystemCode}' is disabled.");
        }

        var readyBridges = _dbContext.AccountingBridgeEntries
            .Where(entity => entity.State == AccountingBridgeState.ReadyForExport && entity.ExportBatchId == null)
            .ToArray()
            .OrderBy(entity => entity.GeneratedAt)
            .ToArray();

        if (readyBridges.Length == 0)
        {
            throw new RuntimeValidationException("No unbatched accounting bridge entries are ready for export.");
        }

        var batch = new AccountingExportBatchEntity
        {
            Id = Guid.NewGuid(),
            BatchNumber = NextAccountingExportBatchNumber(),
            TargetSystemCode = externalSystem.SystemCode,
            State = ExportBatchState.Prepared,
            BridgeCount = readyBridges.Length,
            TotalAmount = readyBridges.Sum(entity => entity.Amount),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.AccountingExportBatches.Add(batch);

        foreach (var bridge in readyBridges)
        {
            bridge.ExportBatchId = batch.Id;
        }

        AddAudit("Integration", "ExportBatchCreated", $"Prepared export batch {batch.BatchNumber} for {externalSystem.SystemCode} with {batch.BridgeCount} bridge entries.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingExportBatch(batch);
    }

    public IntegrationJobItemDto DispatchAccountingExportBatch(string? token, Guid exportBatchId)
    {
        var userContext = RequirePermission(token, "integration.manage");
        var batch = FindAccountingExportBatch(exportBatchId);

        if (batch.State != ExportBatchState.Prepared && batch.State != ExportBatchState.Failed)
        {
            throw new RuntimeValidationException("Only prepared or failed export batches can be dispatched.");
        }

        var bridges = _dbContext.AccountingBridgeEntries
            .Where(entity => entity.ExportBatchId == batch.Id)
            .ToArray();

        if (bridges.Length == 0)
        {
            throw new RuntimeValidationException("The export batch does not contain any accounting bridge entries.");
        }

        var job = new IntegrationJobEntity
        {
            Id = Guid.NewGuid(),
            JobNumber = NextIntegrationJobNumber(),
            TargetSystemCode = batch.TargetSystemCode,
            JobType = "AccountingBridgeExport",
            State = IntegrationJobState.Sent,
            ExportBatchId = batch.Id,
            PayloadSummary = $"{batch.BridgeCount} bridges / {batch.TotalAmount:0.##} CNY",
            QueuedAt = DateTimeOffset.UtcNow
        };

        batch.State = ExportBatchState.Sent;
        batch.SentAt = job.QueuedAt;
        batch.CompletedAt = null;

        foreach (var bridge in bridges)
        {
            bridge.State = AccountingBridgeState.Exported;
        }

        _dbContext.IntegrationJobs.Add(job);
        AddAudit("Integration", "ExportBatchDispatched", $"Dispatched export batch {batch.BatchNumber} to {batch.TargetSystemCode} as job {job.JobNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        job.ExportBatch = batch;
        return MapIntegrationJob(job);
    }

    public IReadOnlyList<IntegrationJobItemDto> GetIntegrationJobs(string? token)
    {
        RequirePermission(token, "integration.read");

        return _dbContext.IntegrationJobs
            .AsNoTracking()
            .Include(entity => entity.ExportBatch)
            .AsEnumerable()
            .OrderByDescending(entity => entity.QueuedAt)
            .Select(MapIntegrationJob)
            .ToArray();
    }

    public IntegrationJobItemDto CompleteIntegrationJob(string? token, Guid integrationJobId, CompleteIntegrationJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "integration.manage");
        var job = _dbContext.IntegrationJobs
            .Include(entity => entity.ExportBatch)
            .SingleOrDefault(entity => entity.Id == integrationJobId)
            ?? throw new RuntimeNotFoundException($"Integration job '{integrationJobId}' was not found.");

        if (!Enum.TryParse<IntegrationJobState>(request.Outcome, true, out var outcome) ||
            (outcome != IntegrationJobState.Succeeded && outcome != IntegrationJobState.Failed))
        {
            throw new RuntimeValidationException($"Unsupported integration outcome '{request.Outcome}'.");
        }

        if (job.State != IntegrationJobState.Sent && job.State != IntegrationJobState.Queued)
        {
            throw new RuntimeValidationException("Only queued or sent integration jobs can be completed.");
        }

        job.State = outcome;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.LastError = outcome == IntegrationJobState.Failed ? request.ErrorMessage?.Trim() : null;

        if (job.ExportBatchId.HasValue && job.ExportBatch is not null)
        {
            var bridges = _dbContext.AccountingBridgeEntries
                .Where(entity => entity.ExportBatchId == job.ExportBatchId.Value)
                .ToArray();

            if (outcome == IntegrationJobState.Succeeded)
            {
                job.ExportBatch.State = ExportBatchState.Acknowledged;
                job.ExportBatch.CompletedAt = job.CompletedAt;

                foreach (var bridge in bridges)
                {
                    bridge.State = AccountingBridgeState.Posted;
                }
            }
            else
            {
                job.ExportBatch.State = ExportBatchState.Failed;
                job.ExportBatch.CompletedAt = job.CompletedAt;

                foreach (var bridge in bridges)
                {
                    bridge.State = AccountingBridgeState.Failed;
                }
            }
        }

        AddAudit("Integration", "IntegrationJobCompleted", $"Completed integration job {job.JobNumber} with outcome {outcome}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapIntegrationJob(job);
    }

    private SessionContext RequireSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new RuntimeUnauthorizedException();
        }

        var session = _dbContext.Sessions
            .AsSplitQuery()
            .Include(entity => entity.User)
                .ThenInclude(entity => entity!.Department)
            .Include(entity => entity.User)
                .ThenInclude(entity => entity!.UserRoles)
                    .ThenInclude(entity => entity.Role)
                        .ThenInclude(entity => entity!.RolePermissions)
                            .ThenInclude(entity => entity.Permission)
            .SingleOrDefault(entity => entity.Token == token);

        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow || session.User is null || session.User.Department is null)
        {
            throw new RuntimeUnauthorizedException("Your session is missing or expired.");
        }

        return new SessionContext(
            session,
            session.User,
            session.User.UserRoles.Select(link => link.Role!.Name).OrderBy(name => name).ToArray(),
            session.User.UserRoles
                .SelectMany(link => link.Role!.RolePermissions)
                .Select(link => link.Permission!.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private SessionContext RequirePermission(string? token, string permission)
    {
        var sessionContext = RequireSession(token);

        if (!sessionContext.PermissionCodes.Contains(permission))
        {
            throw new RuntimeForbiddenException($"Missing permission '{permission}'.");
        }

        return sessionContext;
    }

    private CustomerEntity FindCustomer(Guid customerId)
    {
        return _dbContext.Customers.SingleOrDefault(entity => entity.Id == customerId)
            ?? throw new RuntimeNotFoundException($"Customer '{customerId}' was not found.");
    }

    private SupplierEntity FindSupplier(Guid supplierId)
    {
        return _dbContext.Suppliers.SingleOrDefault(entity => entity.Id == supplierId)
            ?? throw new RuntimeNotFoundException($"Supplier '{supplierId}' was not found.");
    }

    private ExternalSystemEntity FindExternalSystem(string systemCode)
    {
        return _dbContext.ExternalSystems.SingleOrDefault(entity => entity.SystemCode.ToLower() == systemCode.Trim().ToLower())
            ?? throw new RuntimeValidationException($"External system '{systemCode}' was not found.");
    }

    private ProductEntity FindProductByCode(string productCode)
    {
        return _dbContext.Products.SingleOrDefault(entity => entity.Code.ToLower() == productCode.Trim().ToLower())
            ?? throw new RuntimeValidationException($"Product '{productCode}' was not found.");
    }

    private MaterialEntity FindMaterialByCode(string materialCode)
    {
        return _dbContext.Materials.SingleOrDefault(entity => entity.Code.ToLower() == materialCode.Trim().ToLower())
            ?? throw new RuntimeValidationException($"Material '{materialCode}' was not found.");
    }

    private WarehouseEntity FindWarehouseByCode(string warehouseCode)
    {
        return _dbContext.Warehouses.SingleOrDefault(entity => entity.Code.ToLower() == warehouseCode.Trim().ToLower())
            ?? throw new RuntimeValidationException($"Warehouse '{warehouseCode}' was not found.");
    }

    private PurchaseOrderEntity FindPurchaseOrder(Guid purchaseOrderId)
    {
        return _dbContext.PurchaseOrders
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Material)
            .Include(entity => entity.Warehouse)
            .SingleOrDefault(entity => entity.Id == purchaseOrderId)
            ?? throw new RuntimeNotFoundException($"Purchase order '{purchaseOrderId}' was not found.");
    }

    private AccountingExportBatchEntity FindAccountingExportBatch(Guid exportBatchId)
    {
        return _dbContext.AccountingExportBatches.SingleOrDefault(entity => entity.Id == exportBatchId)
            ?? throw new RuntimeNotFoundException($"Accounting export batch '{exportBatchId}' was not found.");
    }

    private SalesOrderEntity FindSalesOrder(Guid salesOrderId)
    {
        return _dbContext.SalesOrders
            .Include(entity => entity.Customer)
            .Include(entity => entity.SalesShipments)
            .SingleOrDefault(entity => entity.Id == salesOrderId)
            ?? throw new RuntimeNotFoundException($"Sales order '{salesOrderId}' was not found.");
    }

    private ProductionOrderEntity FindProductionOrder(Guid productionOrderId)
    {
        return _dbContext.ProductionOrders.Include(entity => entity.SalesOrder).SingleOrDefault(entity => entity.Id == productionOrderId)
            ?? throw new RuntimeNotFoundException($"Production order '{productionOrderId}' was not found.");
    }

    private WorkOrderEntity FindWorkOrder(Guid workOrderId)
    {
        return _dbContext.WorkOrders.Include(entity => entity.ProductionOrder).SingleOrDefault(entity => entity.Id == workOrderId)
            ?? throw new RuntimeNotFoundException($"Work order '{workOrderId}' was not found.");
    }

    private SessionDto MapSession(UserEntity user, SessionEntity session)
    {
        var roleNames = user.UserRoles.Select(link => link.Role!.Name).OrderBy(name => name).ToArray();
        var permissionCodes = user.UserRoles
            .SelectMany(link => link.Role!.RolePermissions)
            .Select(link => link.Permission!.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code)
            .ToArray();

        return new SessionDto(
            session.Token,
            session.ExpiresAt,
            new SessionUserDto(user.Id, user.UserName, user.DisplayName, user.Department!.Name, roleNames, permissionCodes));
    }

    private static CustomerListItemDto MapCustomer(CustomerEntity entity)
    {
        return new CustomerListItemDto(entity.Id, entity.Code, entity.Name, entity.Status.ToString(), entity.Segment.ToString(), entity.Region, entity.Industry, entity.CreditRating);
    }

    private static SupplierListItemDto MapSupplier(SupplierEntity entity)
    {
        return new SupplierListItemDto(entity.Id, entity.Code, entity.Name, entity.Category, entity.TaxRegistrationNumber, entity.LeadTimeDays, entity.PaymentTerms);
    }

    private static AccountingExportBatchDto MapAccountingExportBatch(AccountingExportBatchEntity entity)
    {
        return new AccountingExportBatchDto(
            entity.Id,
            entity.BatchNumber,
            entity.TargetSystemCode,
            entity.State.ToString(),
            entity.BridgeCount,
            entity.TotalAmount,
            entity.CreatedAt,
            entity.SentAt,
            entity.CompletedAt);
    }

    private static PurchaseOrderListItemDto MapPurchaseOrder(PurchaseOrderEntity entity)
    {
        return new PurchaseOrderListItemDto(
            entity.Id,
            entity.OrderNumber,
            entity.SupplierId,
            entity.Supplier!.Name,
            entity.Material!.Code,
            entity.OrderedQuantity,
            entity.ReceivedQuantity,
            entity.UnitPrice,
            entity.TotalAmount,
            entity.Warehouse!.Code,
            entity.State.ToString(),
            entity.OrderedOn,
            entity.ExpectedReceiptDate);
    }

    private static GoodsReceiptListItemDto MapGoodsReceipt(GoodsReceiptEntity entity)
    {
        return new GoodsReceiptListItemDto(
            entity.Id,
            entity.ReceiptNumber,
            entity.PurchaseOrder!.OrderNumber,
            entity.PurchaseOrder.Supplier!.Name,
            entity.PurchaseOrder.Material!.Code,
            entity.ReceivedQuantity,
            entity.Warehouse!.Code,
            entity.QualityStatus.ToString(),
            entity.State.ToString(),
            entity.BatchNumber,
            entity.ReceivedOn);
    }

    private SalesOrderListItemDto MapSalesOrder(SalesOrderEntity entity)
    {
        var productionOrderNumber = entity.ProductionOrderId.HasValue
            ? _dbContext.ProductionOrders.AsNoTracking().Where(order => order.Id == entity.ProductionOrderId.Value).Select(order => order.ProductionOrderNumber).SingleOrDefault()
            : null;

        return new SalesOrderListItemDto(
            entity.Id,
            entity.OrderNumber,
            entity.CustomerId,
            entity.Customer?.Name ?? _dbContext.Customers.AsNoTracking().Where(customer => customer.Id == entity.CustomerId).Select(customer => customer.Name).Single(),
            entity.ProductCode,
            entity.Quantity,
            entity.UnitPrice,
            entity.TotalAmount,
            entity.State.ToString(),
            entity.OrderDate,
            entity.RequestedDeliveryDate,
            productionOrderNumber);
    }

    private static SalesShipmentListItemDto MapShipment(SalesShipmentEntity entity)
    {
        return new SalesShipmentListItemDto(
            entity.Id,
            entity.ShipmentNumber,
            entity.SalesOrder!.OrderNumber,
            entity.SalesOrder.Customer!.Name,
            entity.SalesOrder.ProductCode,
            entity.ShippedQuantity,
            entity.Warehouse!.Code,
            entity.BatchNumber,
            entity.State.ToString(),
            entity.ShippedOn);
    }

    private static IntegrationJobItemDto MapIntegrationJob(IntegrationJobEntity entity)
    {
        return new IntegrationJobItemDto(
            entity.Id,
            entity.JobNumber,
            entity.TargetSystemCode,
            entity.JobType,
            entity.State.ToString(),
            entity.ExportBatch?.BatchNumber,
            entity.PayloadSummary,
            entity.QueuedAt,
            entity.CompletedAt,
            entity.LastError);
    }

    private ProductionOrderListItemDto MapProductionOrder(ProductionOrderEntity entity)
    {
        var salesOrderNumber = entity.SalesOrder?.OrderNumber
            ?? _dbContext.SalesOrders.AsNoTracking().Where(order => order.Id == entity.SalesOrderId).Select(order => order.OrderNumber).Single();
        var workOrderNumber = entity.WorkOrderId.HasValue
            ? _dbContext.WorkOrders.AsNoTracking().Where(order => order.Id == entity.WorkOrderId.Value).Select(order => order.WorkOrderNumber).SingleOrDefault()
            : null;

        return new ProductionOrderListItemDto(
            entity.Id,
            entity.ProductionOrderNumber,
            salesOrderNumber,
            entity.ProductCode,
            entity.PlannedQuantity,
            entity.State.ToString(),
            entity.PlannedStartDate,
            entity.PlannedEndDate,
            workOrderNumber);
    }

    private ProductionOrderListItemDto MapProductionOrder(ProductionOrderEntity entity, string salesOrderNumber)
    {
        var workOrderNumber = entity.WorkOrderId.HasValue
            ? _dbContext.WorkOrders.AsNoTracking().Where(order => order.Id == entity.WorkOrderId.Value).Select(order => order.WorkOrderNumber).SingleOrDefault()
            : null;

        return new ProductionOrderListItemDto(entity.Id, entity.ProductionOrderNumber, salesOrderNumber, entity.ProductCode, entity.PlannedQuantity, entity.State.ToString(), entity.PlannedStartDate, entity.PlannedEndDate, workOrderNumber);
    }

    private WorkOrderListItemDto MapWorkOrder(WorkOrderEntity entity)
    {
        var productionOrderNumber = entity.ProductionOrder?.ProductionOrderNumber
            ?? _dbContext.ProductionOrders.AsNoTracking().Where(order => order.Id == entity.ProductionOrderId).Select(order => order.ProductionOrderNumber).Single();

        return new WorkOrderListItemDto(
            entity.Id,
            entity.WorkOrderNumber,
            productionOrderNumber,
            entity.ProductCode,
            entity.PlannedQuantity,
            entity.GoodQuantity,
            entity.ScrapQuantity,
            entity.State.ToString(),
            entity.CurrentOperationName,
            entity.IssueMode.ToString(),
            entity.IsMachineDriven);
    }

    private static QualityInspectionItemDto MapInspection(QualityInspectionEntity entity)
    {
        return new QualityInspectionItemDto(
            entity.Id,
            entity.InspectionNumber,
            entity.InspectionType.ToString(),
            entity.WorkOrder!.WorkOrderNumber,
            entity.ProductionOrder!.ProductionOrderNumber,
            entity.BatchNumber,
            entity.InspectedQuantity,
            entity.DefectQuantity,
            entity.State.ToString(),
            entity.Disposition?.ToString(),
            entity.InspectorUserName,
            entity.CreatedAt,
            entity.CompletedAt,
            entity.Notes);
    }

    private static AccountingBridgeItemDto MapAccountingBridge(AccountingBridgeEntryEntity entity)
    {
        return new AccountingBridgeItemDto(
            entity.Id,
            entity.BridgeNumber,
            entity.SourceDocumentNumber,
            entity.SourceEvent,
            entity.Amount,
            entity.PostingAccount,
            entity.State.ToString(),
            entity.GeneratedAt);
    }

    private static IEnumerable<WorkOrderOperationEntity> CreateOperations(Guid workOrderId)
    {
        return
        [
            new WorkOrderOperationEntity
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                OperationCode = "CUT",
                OperationName = "Cell Stringing",
                Sequence = 10,
                State = OperationExecutionState.Ready,
                IsMachineDriven = true,
                IssueMode = IssueMode.Backflush,
                WorkCenterCode = "WC-CELL",
                MachineCode = "MC-STRINGER"
            },
            new WorkOrderOperationEntity
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                OperationCode = "LAM",
                OperationName = "Lamination",
                Sequence = 20,
                State = OperationExecutionState.Created,
                IsMachineDriven = true,
                IssueMode = IssueMode.Backflush,
                WorkCenterCode = "WC-LAM",
                MachineCode = "MC-LAM-01"
            },
            new WorkOrderOperationEntity
            {
                Id = Guid.NewGuid(),
                WorkOrderId = workOrderId,
                OperationCode = "FQC",
                OperationName = "Final Test",
                Sequence = 30,
                State = OperationExecutionState.Created,
                IsMachineDriven = false,
                IssueMode = IssueMode.ManualIssue,
                WorkCenterCode = "WC-FQC"
            }
        ];
    }

    private CostPostingEntity CostPosting(WorkOrderEntity workOrder, ProductionOrderEntity productionOrder, string postingType, decimal amount, string reference)
    {
        return new CostPostingEntity
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrder.Id,
            ProductionOrderId = productionOrder.Id,
            PostingType = postingType,
            Amount = amount,
            Reference = reference,
            PostedAt = DateTimeOffset.UtcNow
        };
    }

    private AccountingBridgeEntryEntity AccountingBridge(string sourceDocumentNumber, string sourceEvent, decimal amount, string postingAccount, int? sequenceOverride = null)
    {
        return new AccountingBridgeEntryEntity
        {
            Id = Guid.NewGuid(),
            BridgeNumber = sequenceOverride.HasValue ? FormatNumber("BR", sequenceOverride.Value) : NextAccountingBridgeNumber(),
            SourceDocumentNumber = sourceDocumentNumber,
            SourceEvent = sourceEvent,
            State = AccountingBridgeState.ReadyForExport,
            Amount = decimal.Round(amount, 2),
            PostingAccount = postingAccount,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private void AddAudit(string module, string actionName, string detail, string performedBy, Guid? actorUserId)
    {
        _dbContext.AuditLogs.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Module = module,
            ActionName = actionName,
            Detail = detail,
            PerformedBy = performedBy,
            ActorUserId = actorUserId,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    private string NextSalesOrderNumber()
    {
        return NextNumber(
            "SO",
            _dbContext.SalesOrders.AsNoTracking().Select(entity => entity.OrderNumber).AsEnumerable(),
            _dbContext.SalesOrders.Local.Select(entity => entity.OrderNumber));
    }

    private string NextPurchaseOrderNumber()
    {
        return NextNumber(
            "PO",
            _dbContext.PurchaseOrders.AsNoTracking().Select(entity => entity.OrderNumber).AsEnumerable(),
            _dbContext.PurchaseOrders.Local.Select(entity => entity.OrderNumber));
    }

    private string NextGoodsReceiptNumber()
    {
        return NextNumber(
            "GR",
            _dbContext.GoodsReceipts.AsNoTracking().Select(entity => entity.ReceiptNumber).AsEnumerable(),
            _dbContext.GoodsReceipts.Local.Select(entity => entity.ReceiptNumber));
    }

    private string NextSalesShipmentNumber()
    {
        return NextNumber(
            "SH",
            _dbContext.SalesShipments.AsNoTracking().Select(entity => entity.ShipmentNumber).AsEnumerable(),
            _dbContext.SalesShipments.Local.Select(entity => entity.ShipmentNumber));
    }

    private string NextProductionOrderNumber()
    {
        return NextNumber(
            "MO",
            _dbContext.ProductionOrders.AsNoTracking().Select(entity => entity.ProductionOrderNumber).AsEnumerable(),
            _dbContext.ProductionOrders.Local.Select(entity => entity.ProductionOrderNumber));
    }

    private string NextWorkOrderNumber()
    {
        return NextNumber(
            "WO",
            _dbContext.WorkOrders.AsNoTracking().Select(entity => entity.WorkOrderNumber).AsEnumerable(),
            _dbContext.WorkOrders.Local.Select(entity => entity.WorkOrderNumber));
    }

    private string NextInventoryTransactionNumber()
    {
        return NextNumber(
            "ITX",
            _dbContext.InventoryTransactions.AsNoTracking().Select(entity => entity.TransactionNumber).AsEnumerable(),
            _dbContext.InventoryTransactions.Local.Select(entity => entity.TransactionNumber));
    }

    private string NextInspectionNumber()
    {
        return NextNumber(
            "QI",
            _dbContext.QualityInspections.AsNoTracking().Select(entity => entity.InspectionNumber).AsEnumerable(),
            _dbContext.QualityInspections.Local.Select(entity => entity.InspectionNumber));
    }

    private string NextAccountingBridgeNumber()
    {
        return NextNumber(
            "BR",
            _dbContext.AccountingBridgeEntries.AsNoTracking().Select(entity => entity.BridgeNumber).AsEnumerable(),
            _dbContext.AccountingBridgeEntries.Local.Select(entity => entity.BridgeNumber));
    }

    private string NextAccountingExportBatchNumber()
    {
        return NextNumber(
            "EB",
            _dbContext.AccountingExportBatches.AsNoTracking().Select(entity => entity.BatchNumber).AsEnumerable(),
            _dbContext.AccountingExportBatches.Local.Select(entity => entity.BatchNumber));
    }

    private string NextIntegrationJobNumber()
    {
        return NextNumber(
            "IJ",
            _dbContext.IntegrationJobs.AsNoTracking().Select(entity => entity.JobNumber).AsEnumerable(),
            _dbContext.IntegrationJobs.Local.Select(entity => entity.JobNumber));
    }

    private static bool IsAllowedAccountingBridgeTransition(AccountingBridgeState currentState, AccountingBridgeState targetState)
    {
        if (currentState == targetState)
        {
            return true;
        }

        return currentState switch
        {
            AccountingBridgeState.ReadyForExport => targetState == AccountingBridgeState.Exported || targetState == AccountingBridgeState.Failed,
            AccountingBridgeState.Exported => targetState == AccountingBridgeState.Posted || targetState == AccountingBridgeState.Failed,
            AccountingBridgeState.Posted => targetState == AccountingBridgeState.Reconciled || targetState == AccountingBridgeState.Failed,
            AccountingBridgeState.Failed => targetState == AccountingBridgeState.ReadyForExport,
            _ => false
        };
    }

    private static string NextNumber(string prefix, params IEnumerable<string?>[] numberSets)
    {
        return FormatNumber(prefix, NextNumberSequence(prefix, numberSets));
    }

    private static int NextNumberSequence(string prefix, params IEnumerable<string?>[] numberSets)
    {
        return numberSets
            .SelectMany(set => set)
            .Select(number => ParseSequence(prefix, number))
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string FormatNumber(string prefix, int sequence)
    {
        return $"{prefix}-{sequence:0000}";
    }

    private static int ParseSequence(string prefix, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var expectedPrefix = $"{prefix}-";
        if (!value.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(value[expectedPrefix.Length..], out var sequence) ? sequence : 0;
    }

    private sealed record SessionContext(SessionEntity Session, UserEntity User, IReadOnlyList<string> RoleNames, HashSet<string> PermissionCodes)
    {
        public Guid UserId => User.Id;
        public string UserName => User.UserName;
    }
}
