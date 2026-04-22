using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.ERP.Domain;
using ManufacturingPlatform.Modules.MES.Domain;
using ManufacturingPlatform.Modules.Platform.Domain;

namespace ManufacturingPlatform.Host.Runtime;

public sealed class InMemoryManufacturingRuntime : IManufacturingRuntime
{
    private readonly object _gate = new();
    private readonly List<UserAggregate> _users = [];
    private readonly Dictionary<string, SessionRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Customer> _customers = [];
    private readonly List<Product> _products = [];
    private readonly List<SalesOrderAggregate> _salesOrders = [];
    private readonly List<ProductionOrderAggregate> _productionOrders = [];
    private readonly List<WorkOrderAggregate> _workOrders = [];
    private readonly List<AuditTrailEntry> _auditTrail = [];

    public InMemoryManufacturingRuntime()
    {
        Seed();
    }

    public SessionDto Login(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var normalizedUserName = request.UserName.Trim();
            var user = _users.SingleOrDefault(candidate =>
                string.Equals(candidate.User.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                throw new RuntimeValidationException($"Unknown user '{request.UserName}'.");
            }

            var session = CreateSession(user);
            AddAudit(
                "Platform",
                "SessionLogin",
                $"User {user.User.DisplayName} signed in.",
                user.User.UserName,
                user.User.Id);

            return session;
        }
    }

    public SessionDto GetSession(string? token)
    {
        lock (_gate)
        {
            var session = RequireSession(token);
            return MapSession(session);
        }
    }

    public IReadOnlyList<UserListItemDto> GetUsers(string? token)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "platform.admin");

            AddAudit("Platform", "UserListViewed", "Viewed user directory.", session.User.UserName, session.User.Id);

            return _users
                .OrderBy(user => user.Department.Name)
                .ThenBy(user => user.User.DisplayName)
                .Select(user => new UserListItemDto(
                    user.User.Id,
                    user.User.UserName,
                    user.User.DisplayName,
                    user.Department.Name,
                    user.Roles.Select(role => role.Name).ToArray()))
                .ToArray();
        }
    }

    public IReadOnlyList<AuditLogItemDto> GetAuditLogs(string? token, int take)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "platform.admin");
            var limit = Math.Clamp(take, 1, 100);

            AddAudit("Platform", "AuditTrailViewed", $"Viewed {limit} audit events.", session.User.UserName, session.User.Id);

            return _auditTrail
                .OrderByDescending(entry => entry.Log.OccurredAt)
                .Take(limit)
                .Select(entry => new AuditLogItemDto(
                    entry.Log.Id,
                    entry.Log.OccurredAt,
                    entry.Log.EntityName,
                    entry.Log.ActionName,
                    entry.Detail,
                    entry.PerformedBy))
                .ToArray();
        }
    }

    public DashboardSnapshotDto GetDashboard(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "reporting.dashboard.read");

            var metrics = new DashboardMetricsDto(
                _customers.Count,
                _salesOrders.Count(order => order.Order.State is not SalesOrderState.Cancelled and not SalesOrderState.Closed),
                _productionOrders.Count(order => order.Order.State is not ProductionOrderState.Cancelled and not ProductionOrderState.Closed),
                _workOrders.Count(order => order.WorkOrder.State is WorkOrderState.Created or WorkOrderState.Dispatched or WorkOrderState.Started or WorkOrderState.Paused),
                _salesOrders
                    .Where(order => order.Order.State is not SalesOrderState.Cancelled and not SalesOrderState.Closed)
                    .Sum(order => order.Order.TotalAmount),
                _workOrders.Sum(order => order.GoodQuantity));

            return new DashboardSnapshotDto(
                metrics,
                _salesOrders
                    .OrderByDescending(order => order.Order.OrderDate)
                    .ThenByDescending(order => order.Order.OrderNumber)
                    .Take(5)
                    .Select(MapSalesOrder)
                    .ToArray(),
                _productionOrders
                    .Where(order => order.Order.State is not ProductionOrderState.Cancelled and not ProductionOrderState.Closed)
                    .OrderBy(order => order.Order.PlannedStartDate)
                    .Take(5)
                    .Select(MapProductionOrder)
                    .ToArray(),
                _workOrders
                    .Where(order => order.WorkOrder.State is WorkOrderState.Created or WorkOrderState.Dispatched or WorkOrderState.Started or WorkOrderState.Paused)
                    .OrderBy(order => order.WorkOrder.WorkOrderNumber)
                    .Take(5)
                    .Select(MapWorkOrder)
                    .ToArray(),
                _auditTrail
                    .OrderByDescending(entry => entry.Log.OccurredAt)
                    .Take(8)
                    .Select(entry => new ActivityFeedItemDto(entry.Log.OccurredAt, entry.Module, entry.Detail))
                    .ToArray());
        }
    }

    public IReadOnlyList<ProductListItemDto> GetProducts(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "erp.product.read");

            return _products
                .OrderBy(product => product.Code)
                .Select(product => new ProductListItemDto(
                    product.Id,
                    product.Code,
                    product.Name,
                    product.ProductFamily,
                    product.BaseUnitCode))
                .ToArray();
        }
    }

    public IReadOnlyList<CustomerListItemDto> GetCustomers(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "crm.customer.read");

            return _customers
                .OrderBy(customer => customer.Code)
                .Select(MapCustomer)
                .ToArray();
        }
    }

    public CustomerListItemDto CreateCustomer(string? token, CreateCustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var session = RequirePermission(token, "crm.customer.write");

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RuntimeValidationException("Customer code and name are required.");
            }

            if (_customers.Any(customer => string.Equals(customer.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RuntimeValidationException($"Customer code '{request.Code}' already exists.");
            }

            if (!Enum.TryParse<CustomerSegment>(request.Segment, ignoreCase: true, out var segment))
            {
                throw new RuntimeValidationException($"Unsupported customer segment '{request.Segment}'.");
            }

            var customer = new Customer(
                Guid.NewGuid(),
                request.Code.Trim().ToUpperInvariant(),
                request.Name.Trim(),
                CustomerStatus.Active,
                segment,
                request.Region.Trim(),
                request.Industry.Trim(),
                request.CreditRating.Trim());

            _customers.Add(customer);
            AddAudit(
                "CRM",
                "CustomerCreated",
                $"Created customer {customer.Code} - {customer.Name}.",
                session.User.UserName,
                session.User.Id);

            return MapCustomer(customer);
        }
    }

    public IReadOnlyList<SalesOrderListItemDto> GetSalesOrders(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "crm.sales-order.read");

            return _salesOrders
                .OrderByDescending(order => order.Order.OrderDate)
                .ThenByDescending(order => order.Order.OrderNumber)
                .Select(MapSalesOrder)
                .ToArray();
        }
    }

    public SalesOrderListItemDto CreateSalesOrder(string? token, CreateSalesOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var session = RequirePermission(token, "crm.sales-order.write");
            var customer = FindCustomer(request.CustomerId);
            var product = FindProduct(request.ProductCode);

            if (request.Quantity <= 0 || request.UnitPrice <= 0)
            {
                throw new RuntimeValidationException("Quantity and unit price must be greater than zero.");
            }

            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            var order = new SalesOrder(
                Guid.NewGuid(),
                NextNumber("SO", _salesOrders.Count + 1),
                customer.Id,
                null,
                SalesOrderState.Draft,
                now,
                decimal.Round(request.Quantity * request.UnitPrice, 2));

            var aggregate = new SalesOrderAggregate
            {
                Order = order,
                ProductCode = product.Code,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                RequestedDeliveryDate = request.RequestedDeliveryDate
            };

            _salesOrders.Add(aggregate);
            AddAudit(
                "CRM",
                "SalesOrderCreated",
                $"Created sales order {order.OrderNumber} for {customer.Name}.",
                session.User.UserName,
                session.User.Id);

            return MapSalesOrder(aggregate);
        }
    }

    public SalesOrderListItemDto SubmitSalesOrder(string? token, Guid salesOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "crm.sales-order.write");
            var salesOrder = FindSalesOrder(salesOrderId);

            if (salesOrder.Order.State != SalesOrderState.Draft)
            {
                throw new RuntimeValidationException("Only draft sales orders can be submitted.");
            }

            salesOrder.Order = salesOrder.Order with { State = SalesOrderState.Submitted };
            AddAudit(
                "CRM",
                "SalesOrderSubmitted",
                $"Submitted sales order {salesOrder.Order.OrderNumber} for approval.",
                session.User.UserName,
                session.User.Id);

            return MapSalesOrder(salesOrder);
        }
    }

    public SalesOrderListItemDto ApproveSalesOrder(string? token, Guid salesOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "crm.sales-order.approve");
            var salesOrder = FindSalesOrder(salesOrderId);

            if (salesOrder.Order.State != SalesOrderState.Submitted)
            {
                throw new RuntimeValidationException("Only submitted sales orders can be approved.");
            }

            salesOrder.Order = salesOrder.Order with { State = SalesOrderState.Approved };
            AddAudit(
                "CRM",
                "SalesOrderApproved",
                $"Approved sales order {salesOrder.Order.OrderNumber}.",
                session.User.UserName,
                session.User.Id);

            return MapSalesOrder(salesOrder);
        }
    }

    public SalesOrderListItemDto ReleaseSalesOrder(string? token, Guid salesOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "crm.sales-order.release");
            var salesOrder = FindSalesOrder(salesOrderId);

            if (salesOrder.Order.State != SalesOrderState.Approved)
            {
                throw new RuntimeValidationException("Only approved sales orders can be released.");
            }

            salesOrder.Order = salesOrder.Order with { State = SalesOrderState.Released };

            if (!salesOrder.ProductionOrderId.HasValue)
            {
                var productionOrder = new ProductionOrder(
                    Guid.NewGuid(),
                    NextNumber("MO", _productionOrders.Count + 1),
                    ProductionOrderState.Approved,
                    salesOrder.ProductCode,
                    salesOrder.Quantity,
                    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
                    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4));

                _productionOrders.Add(new ProductionOrderAggregate
                {
                    Order = productionOrder,
                    SalesOrderId = salesOrder.Order.Id,
                    SalesOrderNumber = salesOrder.Order.OrderNumber
                });

                salesOrder.ProductionOrderId = productionOrder.Id;

                AddAudit(
                    "ERP",
                    "ProductionOrderCreated",
                    $"Created production order {productionOrder.ProductionOrderNumber} from sales order {salesOrder.Order.OrderNumber}.",
                    session.User.UserName,
                    session.User.Id);
            }

            AddAudit(
                "CRM",
                "SalesOrderReleased",
                $"Released sales order {salesOrder.Order.OrderNumber} to ERP fulfillment.",
                session.User.UserName,
                session.User.Id);

            return MapSalesOrder(salesOrder);
        }
    }

    public IReadOnlyList<ProductionOrderListItemDto> GetProductionOrders(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "erp.production-order.read");

            return _productionOrders
                .OrderBy(order => order.Order.PlannedStartDate)
                .ThenBy(order => order.Order.ProductionOrderNumber)
                .Select(MapProductionOrder)
                .ToArray();
        }
    }

    public ProductionOrderListItemDto ReleaseProductionOrderToMes(string? token, Guid productionOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "erp.production-order.release");
            var productionOrder = FindProductionOrder(productionOrderId);

            if (productionOrder.Order.State != ProductionOrderState.Approved)
            {
                throw new RuntimeValidationException("Only approved production orders can be released to MES.");
            }

            productionOrder.Order = productionOrder.Order with { State = ProductionOrderState.ReleasedToMes };

            if (!productionOrder.WorkOrderId.HasValue)
            {
                var firstOperationId = Guid.NewGuid();
                var secondOperationId = Guid.NewGuid();
                var thirdOperationId = Guid.NewGuid();

                var operations = new List<WorkOrderOperation>
                {
                    new(
                        firstOperationId,
                        Guid.Empty,
                        "CUT",
                        "Cell Stringing",
                        10,
                        OperationExecutionState.Ready,
                        DeterministicGuid("WC-CELL"),
                        DeterministicGuid("MC-STRINGER"),
                        true,
                        IssueMode.Backflush),
                    new(
                        secondOperationId,
                        Guid.Empty,
                        "LAM",
                        "Lamination",
                        20,
                        OperationExecutionState.Created,
                        DeterministicGuid("WC-LAM"),
                        DeterministicGuid("MC-LAM-01"),
                        true,
                        IssueMode.Backflush),
                    new(
                        thirdOperationId,
                        Guid.Empty,
                        "FQC",
                        "Final Test",
                        30,
                        OperationExecutionState.Created,
                        DeterministicGuid("WC-FQC"),
                        null,
                        false,
                        IssueMode.ManualIssue)
                };

                var workOrderId = Guid.NewGuid();
                var workOrderNumber = NextNumber("WO", _workOrders.Count + 1);
                var workOrder = new WorkOrder(
                    workOrderId,
                    workOrderNumber,
                    productionOrder.Order.Id,
                    WorkOrderState.Created,
                    productionOrder.Order.ProductCode,
                    productionOrder.Order.PlannedQuantity);

                for (var index = 0; index < operations.Count; index++)
                {
                    operations[index] = operations[index] with { WorkOrderId = workOrderId };
                }

                _workOrders.Add(new WorkOrderAggregate
                {
                    WorkOrder = workOrder,
                    ProductionOrderId = productionOrder.Order.Id,
                    Operations = operations,
                    CurrentOperationId = firstOperationId,
                    CurrentOperationName = operations[0].OperationName
                });

                productionOrder.WorkOrderId = workOrderId;

                AddAudit(
                    "MES",
                    "WorkOrderCreated",
                    $"Created work order {workOrderNumber} from production order {productionOrder.Order.ProductionOrderNumber}.",
                    session.User.UserName,
                    session.User.Id);
            }

            AddAudit(
                "ERP",
                "ProductionOrderReleased",
                $"Released production order {productionOrder.Order.ProductionOrderNumber} to MES.",
                session.User.UserName,
                session.User.Id);

            return MapProductionOrder(productionOrder);
        }
    }

    public IReadOnlyList<WorkOrderListItemDto> GetWorkOrders(string? token)
    {
        lock (_gate)
        {
            RequirePermission(token, "mes.work-order.read");

            return _workOrders
                .OrderBy(order => order.WorkOrder.WorkOrderNumber)
                .Select(MapWorkOrder)
                .ToArray();
        }
    }

    public WorkOrderListItemDto DispatchWorkOrder(string? token, Guid workOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "mes.work-order.execute");
            var workOrder = FindWorkOrder(workOrderId);

            if (workOrder.WorkOrder.State != WorkOrderState.Created)
            {
                throw new RuntimeValidationException("Only created work orders can be dispatched.");
            }

            workOrder.WorkOrder = workOrder.WorkOrder with { State = WorkOrderState.Dispatched };
            workOrder.Operations[0] = workOrder.Operations[0] with { State = OperationExecutionState.Dispatched };
            workOrder.CurrentOperationName = workOrder.Operations[0].OperationName;

            AddAudit(
                "MES",
                "WorkOrderDispatched",
                $"Dispatched work order {workOrder.WorkOrder.WorkOrderNumber}.",
                session.User.UserName,
                session.User.Id);

            return MapWorkOrder(workOrder);
        }
    }

    public WorkOrderListItemDto StartWorkOrder(string? token, Guid workOrderId)
    {
        lock (_gate)
        {
            var session = RequirePermission(token, "mes.work-order.execute");
            var workOrder = FindWorkOrder(workOrderId);

            if (workOrder.WorkOrder.State is not WorkOrderState.Dispatched and not WorkOrderState.Paused)
            {
                throw new RuntimeValidationException("Only dispatched or paused work orders can be started.");
            }

            workOrder.WorkOrder = workOrder.WorkOrder with { State = WorkOrderState.Started };

            var operationIndex = workOrder.Operations.FindIndex(operation => operation.Id == workOrder.CurrentOperationId);
            workOrder.Operations[operationIndex] = workOrder.Operations[operationIndex] with { State = OperationExecutionState.Started };
            workOrder.CurrentOperationName = workOrder.Operations[operationIndex].OperationName;

            var productionOrder = FindProductionOrder(workOrder.ProductionOrderId);
            productionOrder.Order = productionOrder.Order with { State = ProductionOrderState.InExecution };

            AddAudit(
                "MES",
                "WorkOrderStarted",
                $"Started work order {workOrder.WorkOrder.WorkOrderNumber}.",
                session.User.UserName,
                session.User.Id);

            return MapWorkOrder(workOrder);
        }
    }

    public WorkOrderListItemDto ReportWorkOrder(string? token, Guid workOrderId, ReportWorkOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            var session = RequirePermission(token, "mes.work-order.execute");
            var workOrder = FindWorkOrder(workOrderId);

            if (workOrder.WorkOrder.State != WorkOrderState.Started)
            {
                throw new RuntimeValidationException("Only started work orders can be reported.");
            }

            if (request.GoodQuantity < 0 || request.ScrapQuantity < 0 || request.ReworkQuantity < 0)
            {
                throw new RuntimeValidationException("Reported quantities cannot be negative.");
            }

            if (request.GoodQuantity + request.ScrapQuantity + request.ReworkQuantity > workOrder.WorkOrder.PlannedQuantity)
            {
                throw new RuntimeValidationException("Reported quantity cannot exceed the planned work order quantity.");
            }

            workOrder.GoodQuantity = request.GoodQuantity;
            workOrder.ScrapQuantity = request.ScrapQuantity;
            workOrder.ReworkQuantity = request.ReworkQuantity;
            workOrder.LaborHours = request.LaborHours;
            workOrder.MachineHours = request.MachineHours;
            workOrder.WorkOrder = workOrder.WorkOrder with { State = WorkOrderState.Reported };
            workOrder.CurrentOperationName = "Reported to ERP";

            for (var index = 0; index < workOrder.Operations.Count; index++)
            {
                workOrder.Operations[index] = workOrder.Operations[index] with { State = OperationExecutionState.Completed };
            }

            var productionOrder = FindProductionOrder(workOrder.ProductionOrderId);
            productionOrder.Order = productionOrder.Order with { State = ProductionOrderState.TechnicallyCompleted };

            AddAudit(
                "MES",
                "WorkOrderReported",
                $"Reported work order {workOrder.WorkOrder.WorkOrderNumber}: good {request.GoodQuantity}, scrap {request.ScrapQuantity}.",
                session.User.UserName,
                session.User.Id);

            AddAudit(
                "ERP",
                "ProductionOrderCompleted",
                $"Production order {productionOrder.Order.ProductionOrderNumber} reached technical completion.",
                session.User.UserName,
                session.User.Id);

            if (request.ScrapQuantity > 0)
            {
                AddAudit(
                    "ERP",
                    "ScrapRecorded",
                    $"Scrap of {request.ScrapQuantity} recorded for {workOrder.WorkOrder.WorkOrderNumber} with reason {request.ScrapReasonCode ?? "UNSPECIFIED"}.",
                    session.User.UserName,
                    session.User.Id);
            }

            return MapWorkOrder(workOrder);
        }
    }

    private SessionDto CreateSession(UserAggregate user)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        var session = new SessionRecord(token, user.User.Id, DateTimeOffset.UtcNow.AddHours(8));
        _sessions[token] = session;
        return MapSession(session);
    }

    private SessionRecord RequireSession(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new RuntimeUnauthorizedException();
        }

        if (!_sessions.TryGetValue(token, out var session) || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new RuntimeUnauthorizedException("Your session is missing or expired.");
        }

        return session;
    }

    private UserAggregate RequirePermission(string? token, string permission)
    {
        var session = RequireSession(token);
        var user = FindUser(session.UserId);

        if (!user.PermissionCodes.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            throw new RuntimeForbiddenException($"Missing permission '{permission}'.");
        }

        return user;
    }

    private UserAggregate FindUser(Guid userId)
    {
        return _users.SingleOrDefault(user => user.User.Id == userId)
            ?? throw new RuntimeNotFoundException($"User '{userId}' was not found.");
    }

    private Customer FindCustomer(Guid customerId)
    {
        return _customers.SingleOrDefault(customer => customer.Id == customerId)
            ?? throw new RuntimeNotFoundException($"Customer '{customerId}' was not found.");
    }

    private Product FindProduct(string productCode)
    {
        return _products.SingleOrDefault(product => string.Equals(product.Code, productCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new RuntimeValidationException($"Product '{productCode}' was not found.");
    }

    private SalesOrderAggregate FindSalesOrder(Guid salesOrderId)
    {
        return _salesOrders.SingleOrDefault(order => order.Order.Id == salesOrderId)
            ?? throw new RuntimeNotFoundException($"Sales order '{salesOrderId}' was not found.");
    }

    private ProductionOrderAggregate FindProductionOrder(Guid productionOrderId)
    {
        return _productionOrders.SingleOrDefault(order => order.Order.Id == productionOrderId)
            ?? throw new RuntimeNotFoundException($"Production order '{productionOrderId}' was not found.");
    }

    private WorkOrderAggregate FindWorkOrder(Guid workOrderId)
    {
        return _workOrders.SingleOrDefault(order => order.WorkOrder.Id == workOrderId)
            ?? throw new RuntimeNotFoundException($"Work order '{workOrderId}' was not found.");
    }

    private SessionDto MapSession(SessionRecord session)
    {
        var user = FindUser(session.UserId);
        return new SessionDto(
            session.Token,
            session.ExpiresAt,
            new SessionUserDto(
                user.User.Id,
                user.User.UserName,
                user.User.DisplayName,
                user.Department.Name,
                user.Roles.Select(role => role.Name).ToArray(),
                user.PermissionCodes.OrderBy(code => code).ToArray()));
    }

    private CustomerListItemDto MapCustomer(Customer customer)
    {
        return new CustomerListItemDto(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.Status.ToString(),
            customer.Segment.ToString(),
            customer.Region,
            customer.Industry,
            customer.CreditRating);
    }

    private SalesOrderListItemDto MapSalesOrder(SalesOrderAggregate order)
    {
        var customer = FindCustomer(order.Order.CustomerId);
        var productionOrderNumber = order.ProductionOrderId.HasValue
            ? FindProductionOrder(order.ProductionOrderId.Value).Order.ProductionOrderNumber
            : null;

        return new SalesOrderListItemDto(
            order.Order.Id,
            order.Order.OrderNumber,
            customer.Id,
            customer.Name,
            order.ProductCode,
            order.Quantity,
            order.UnitPrice,
            order.Order.TotalAmount,
            order.Order.State.ToString(),
            order.Order.OrderDate,
            order.RequestedDeliveryDate,
            productionOrderNumber);
    }

    private ProductionOrderListItemDto MapProductionOrder(ProductionOrderAggregate order)
    {
        var workOrderNumber = order.WorkOrderId.HasValue
            ? FindWorkOrder(order.WorkOrderId.Value).WorkOrder.WorkOrderNumber
            : null;

        return new ProductionOrderListItemDto(
            order.Order.Id,
            order.Order.ProductionOrderNumber,
            order.SalesOrderNumber,
            order.Order.ProductCode,
            order.Order.PlannedQuantity,
            order.Order.State.ToString(),
            order.Order.PlannedStartDate,
            order.Order.PlannedEndDate,
            workOrderNumber);
    }

    private WorkOrderListItemDto MapWorkOrder(WorkOrderAggregate order)
    {
        var productionOrder = FindProductionOrder(order.ProductionOrderId);
        var currentOperation = order.Operations.Single(operation => operation.Id == order.CurrentOperationId);

        return new WorkOrderListItemDto(
            order.WorkOrder.Id,
            order.WorkOrder.WorkOrderNumber,
            productionOrder.Order.ProductionOrderNumber,
            order.WorkOrder.ProductCode,
            order.WorkOrder.PlannedQuantity,
            order.GoodQuantity,
            order.ScrapQuantity,
            order.WorkOrder.State.ToString(),
            order.CurrentOperationName,
            currentOperation.IssueMode.ToString(),
            currentOperation.IsMachineDriven);
    }

    private void AddAudit(string module, string actionName, string detail, string performedBy, Guid actorUserId)
    {
        _auditTrail.Add(
            new AuditTrailEntry(
                new AuditLog(Guid.NewGuid(), module, actionName, DateTimeOffset.UtcNow),
                detail,
                performedBy,
                actorUserId,
                module));
    }

    private void Seed()
    {
        var salesDepartment = new Department(DeterministicGuid("DEPT-SALES"), "SALES", "Sales");
        var planningDepartment = new Department(DeterministicGuid("DEPT-PLANNING"), "PLN", "Production Planning");
        var productionDepartment = new Department(DeterministicGuid("DEPT-PROD"), "PROD", "Production");
        var itDepartment = new Department(DeterministicGuid("DEPT-IT"), "IT", "IT");

        var adminRole = new Role(DeterministicGuid("ROLE-ADMIN"), "ADMIN", "System Administrator");
        var salesRole = new Role(DeterministicGuid("ROLE-SALES"), "SALES", "Sales Manager");
        var plannerRole = new Role(DeterministicGuid("ROLE-PLANNER"), "PLANNER", "Production Planner");
        var operatorRole = new Role(DeterministicGuid("ROLE-OPERATOR"), "OPERATOR", "Workshop Operator");

        _users.Add(
            CreateUser(
                "admin",
                "Platform Admin",
                itDepartment,
                [adminRole],
                [
                    "platform.admin",
                    "crm.customer.read",
                    "crm.customer.write",
                    "crm.sales-order.read",
                    "crm.sales-order.write",
                    "crm.sales-order.approve",
                    "crm.sales-order.release",
                    "erp.product.read",
                    "erp.production-order.read",
                    "erp.production-order.release",
                    "mes.work-order.read",
                    "mes.work-order.execute",
                    "reporting.dashboard.read"
                ]));

        _users.Add(
            CreateUser(
                "sales",
                "Sales Director",
                salesDepartment,
                [salesRole],
                [
                    "crm.customer.read",
                    "crm.customer.write",
                    "crm.sales-order.read",
                    "crm.sales-order.write",
                    "crm.sales-order.approve",
                    "crm.sales-order.release",
                    "erp.product.read",
                    "reporting.dashboard.read"
                ]));

        _users.Add(
            CreateUser(
                "planner",
                "Planning Manager",
                planningDepartment,
                [plannerRole],
                [
                    "erp.product.read",
                    "erp.production-order.read",
                    "erp.production-order.release",
                    "mes.work-order.read",
                    "reporting.dashboard.read"
                ]));

        _users.Add(
            CreateUser(
                "operator",
                "Workshop Operator",
                productionDepartment,
                [operatorRole],
                [
                    "mes.work-order.read",
                    "mes.work-order.execute",
                    "reporting.dashboard.read"
                ]));

        var product580 = new Product(DeterministicGuid("PROD-580"), "PANEL-580W", "182N Mono 580W", "Solar Panel", "PCS", true);
        var product610 = new Product(DeterministicGuid("PROD-610"), "PANEL-610W", "210R Mono 610W", "Solar Panel", "PCS", true);
        _products.AddRange([product580, product610]);

        var customerA = new Customer(DeterministicGuid("CUST-JN"), "C-0001", "Jinan New Energy Co.", CustomerStatus.Active, CustomerSegment.KeyAccount, "Shandong", "Solar EPC", "A");
        var customerB = new Customer(DeterministicGuid("CUST-QD"), "C-0002", "Qingdao Power Integration Ltd.", CustomerStatus.Active, CustomerSegment.Strategic, "Shandong", "Industrial Power", "A+");
        _customers.AddRange([customerA, customerB]);

        var salesOrder1 = new SalesOrderAggregate
        {
            Order = new SalesOrder(DeterministicGuid("SO-001"), "SO-0001", customerA.Id, null, SalesOrderState.Released, new DateOnly(2026, 4, 18), 93600m),
            ProductCode = product580.Code,
            Quantity = 120m,
            UnitPrice = 780m,
            RequestedDeliveryDate = new DateOnly(2026, 4, 30)
        };

        var salesOrder2 = new SalesOrderAggregate
        {
            Order = new SalesOrder(DeterministicGuid("SO-002"), "SO-0002", customerB.Id, null, SalesOrderState.Released, new DateOnly(2026, 4, 19), 63960m),
            ProductCode = product610.Code,
            Quantity = 78m,
            UnitPrice = 820m,
            RequestedDeliveryDate = new DateOnly(2026, 4, 29)
        };

        var salesOrder3 = new SalesOrderAggregate
        {
            Order = new SalesOrder(DeterministicGuid("SO-003"), "SO-0003", customerB.Id, null, SalesOrderState.Draft, new DateOnly(2026, 4, 22), 25200m),
            ProductCode = product580.Code,
            Quantity = 30m,
            UnitPrice = 840m,
            RequestedDeliveryDate = new DateOnly(2026, 5, 5)
        };

        _salesOrders.AddRange([salesOrder1, salesOrder2, salesOrder3]);

        var productionOrder1 = new ProductionOrderAggregate
        {
            Order = new ProductionOrder(DeterministicGuid("MO-001"), "MO-0001", ProductionOrderState.Approved, product580.Code, 120m, new DateOnly(2026, 4, 23), new DateOnly(2026, 4, 27)),
            SalesOrderId = salesOrder1.Order.Id,
            SalesOrderNumber = salesOrder1.Order.OrderNumber
        };

        var productionOrder2 = new ProductionOrderAggregate
        {
            Order = new ProductionOrder(DeterministicGuid("MO-002"), "MO-0002", ProductionOrderState.InExecution, product610.Code, 78m, new DateOnly(2026, 4, 22), new DateOnly(2026, 4, 26)),
            SalesOrderId = salesOrder2.Order.Id,
            SalesOrderNumber = salesOrder2.Order.OrderNumber
        };

        salesOrder1.ProductionOrderId = productionOrder1.Order.Id;
        salesOrder2.ProductionOrderId = productionOrder2.Order.Id;
        _productionOrders.AddRange([productionOrder1, productionOrder2]);

        var operations = new List<WorkOrderOperation>
        {
            new(
                DeterministicGuid("WO-002-OP-10"),
                DeterministicGuid("WO-001"),
                "CUT",
                "Cell Stringing",
                10,
                OperationExecutionState.Completed,
                DeterministicGuid("WC-CELL"),
                DeterministicGuid("MC-STRINGER"),
                true,
                IssueMode.Backflush),
            new(
                DeterministicGuid("WO-002-OP-20"),
                DeterministicGuid("WO-001"),
                "LAM",
                "Lamination",
                20,
                OperationExecutionState.Started,
                DeterministicGuid("WC-LAM"),
                DeterministicGuid("MC-LAM-01"),
                true,
                IssueMode.Backflush),
            new(
                DeterministicGuid("WO-002-OP-30"),
                DeterministicGuid("WO-001"),
                "FQC",
                "Final Test",
                30,
                OperationExecutionState.Created,
                DeterministicGuid("WC-FQC"),
                null,
                false,
                IssueMode.ManualIssue)
        };

        var workOrder = new WorkOrderAggregate
        {
            WorkOrder = new WorkOrder(DeterministicGuid("WO-001"), "WO-0001", productionOrder2.Order.Id, WorkOrderState.Started, product610.Code, 78m),
            ProductionOrderId = productionOrder2.Order.Id,
            Operations = operations,
            CurrentOperationId = operations[1].Id,
            CurrentOperationName = operations[1].OperationName,
            GoodQuantity = 36m,
            ScrapQuantity = 2m,
            LaborHours = 12.5m,
            MachineHours = 10m
        };

        productionOrder2.WorkOrderId = workOrder.WorkOrder.Id;
        _workOrders.Add(workOrder);

        AddAudit("Seed", "DataLoaded", "Loaded demo users, customers, products, and order flow data.", "system", DeterministicGuid("SYSTEM"));
    }

    private static UserAggregate CreateUser(
        string userName,
        string displayName,
        Department department,
        IReadOnlyList<Role> roles,
        IReadOnlyList<string> permissions)
    {
        return new UserAggregate
        {
            User = new User(DeterministicGuid($"USER-{userName.ToUpperInvariant()}"), userName, displayName),
            Department = department,
            Roles = roles.ToArray(),
            PermissionCodes = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static string NextNumber(string prefix, int sequence)
    {
        return $"{prefix}-{sequence:0000}";
    }

    private static Guid DeterministicGuid(string seed)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(seed.PadRight(16, '_')[..16]);
        return new Guid(bytes);
    }

    private sealed record SessionRecord(string Token, Guid UserId, DateTimeOffset ExpiresAt);

    private sealed class UserAggregate
    {
        public required User User { get; init; }
        public required Department Department { get; init; }
        public required Role[] Roles { get; init; }
        public required string[] PermissionCodes { get; init; }
    }

    private sealed class SalesOrderAggregate
    {
        public required SalesOrder Order { get; set; }
        public required string ProductCode { get; init; }
        public required decimal Quantity { get; init; }
        public required decimal UnitPrice { get; init; }
        public required DateOnly RequestedDeliveryDate { get; init; }
        public Guid? ProductionOrderId { get; set; }
    }

    private sealed class ProductionOrderAggregate
    {
        public required ProductionOrder Order { get; set; }
        public required Guid SalesOrderId { get; init; }
        public required string SalesOrderNumber { get; init; }
        public Guid? WorkOrderId { get; set; }
    }

    private sealed class WorkOrderAggregate
    {
        public required WorkOrder WorkOrder { get; set; }
        public required Guid ProductionOrderId { get; init; }
        public required List<WorkOrderOperation> Operations { get; init; }
        public required Guid CurrentOperationId { get; set; }
        public required string CurrentOperationName { get; set; }
        public decimal GoodQuantity { get; set; }
        public decimal ScrapQuantity { get; set; }
        public decimal ReworkQuantity { get; set; }
        public decimal LaborHours { get; set; }
        public decimal MachineHours { get; set; }
    }

    private sealed record AuditTrailEntry(
        AuditLog Log,
        string Detail,
        string PerformedBy,
        Guid ActorUserId,
        string Module);
}
