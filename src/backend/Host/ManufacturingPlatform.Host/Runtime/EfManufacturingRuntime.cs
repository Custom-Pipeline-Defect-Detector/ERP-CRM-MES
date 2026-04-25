using System.Globalization;
using ManufacturingPlatform.BuildingBlocks.Runtime;
using ManufacturingPlatform.Host.Persistence;
using ManufacturingPlatform.Host.Security;
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
    ManufacturingAiSummaryService aiSummaryService,
    AccountingExportGatewayService accountingExportGatewayService) : IManufacturingRuntime
{
    private readonly ManufacturingDbContext _dbContext = dbContext;
    private readonly ManufacturingOptions _options = options.Value;
    private readonly ManufacturingAiSummaryService _aiSummaryService = aiSummaryService;
    private readonly AccountingExportGatewayService _accountingExportGatewayService = accountingExportGatewayService;
    private static readonly PasswordPolicyDto PasswordPolicy = new(PasswordSecurity.MinimumLength, true, true, true, true);

    public SessionDto Login(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userName = request.UserName.Trim();

        var user = _dbContext.Users
            .AsSplitQuery()
            .Include(entity => entity.Department)
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
                    .ThenInclude(entity => entity!.RolePermissions)
                        .ThenInclude(entity => entity.Permission)
            .SingleOrDefault(entity => entity.UserName.ToLower() == userName.ToLower());

        if (user is null)
        {
            throw new RuntimeUnauthorizedException("Invalid username or password.");
        }

        if (user.LockedUntil is not null && user.LockedUntil > DateTimeOffset.UtcNow)
        {
            throw new RuntimeUnauthorizedException($"Account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC.");
        }

        if (!PasswordSecurity.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedAccessCount += 1;

            if (user.FailedAccessCount >= 5)
            {
                user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
                AddAudit("Platform", "SessionLoginLocked", $"User {user.DisplayName} was locked after failed sign-in attempts.", user.UserName, user.Id);
            }
            else
            {
                AddAudit("Platform", "SessionLoginFailed", $"Failed sign-in attempt for {user.DisplayName}.", user.UserName, user.Id);
            }

            _dbContext.SaveChanges();
            throw new RuntimeUnauthorizedException("Invalid username or password.");
        }

        if (user.MustChangePassword &&
            user.PasswordResetExpiresAt is not null &&
            user.PasswordResetExpiresAt <= DateTimeOffset.UtcNow)
        {
            AddAudit("Platform", "SessionLoginResetExpired", $"Temporary password expired for {user.DisplayName}.", user.UserName, user.Id);
            _dbContext.SaveChanges();
            throw new RuntimeUnauthorizedException("Temporary password expired. Ask an administrator to reset the password again.");
        }

        var sessionEntity = new SessionEntity
        {
            Token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
        };

        user.FailedAccessCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTimeOffset.UtcNow;
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

    public SessionDto ChangePassword(string? token, ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sessionContext = RequireSession(token);
        var user = sessionContext.User;

        if (!PasswordSecurity.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            AddAudit("Platform", "PasswordChangeFailed", $"Password change failed for {user.DisplayName}.", user.UserName, user.Id);
            _dbContext.SaveChanges();
            throw new RuntimeUnauthorizedException("Current password is incorrect.");
        }

        var policyError = PasswordSecurity.ValidateNewPassword(request.NewPassword);
        if (policyError is not null)
        {
            throw new RuntimeValidationException(policyError);
        }

        if (PasswordSecurity.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            throw new RuntimeValidationException("New password must be different from the current password.");
        }

        user.PasswordHash = PasswordSecurity.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.MustChangePassword = false;
        user.PasswordResetExpiresAt = null;
        user.FailedAccessCount = 0;
        user.LockedUntil = null;

        AddAudit("Platform", "PasswordChanged", $"Password changed for {user.DisplayName}.", user.UserName, user.Id);
        _dbContext.SaveChanges();

        return MapSession(user, sessionContext.Session);
    }

    public PasswordPolicyDto GetPasswordPolicy()
    {
        return PasswordPolicy;
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

        return users.Select(entity => MapUser(entity)).ToArray();
    }

    public IReadOnlyList<DepartmentListItemDto> GetDepartments(string? token)
    {
        var userContext = RequirePermission(token, "platform.admin");

        var departments = _dbContext.Departments
            .AsNoTracking()
            .Include(entity => entity.Users)
            .OrderBy(entity => entity.Code)
            .ToArray();

        AddAudit("Platform", "DepartmentListViewed", "Viewed department directory.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return departments.Select(entity => MapDepartment(entity)).ToArray();
    }

    public DepartmentListItemDto CreateDepartment(string? token, CreateDepartmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "platform.admin");
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new RuntimeValidationException("Department code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new RuntimeValidationException("Department name is required.");
        }

        if (_dbContext.Departments.Any(entity => entity.Code.ToLower() == code.ToLower()))
        {
            throw new RuntimeValidationException($"Department code '{code}' already exists.");
        }

        var department = new DepartmentEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name
        };

        _dbContext.Departments.Add(department);
        AddAudit("Platform", "DepartmentCreated", $"Created department {department.Code} - {department.Name}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapDepartment(department);
    }

    public IReadOnlyList<RoleListItemDto> GetRoles(string? token)
    {
        var userContext = RequirePermission(token, "platform.admin");

        var roles = _dbContext.Roles
            .AsNoTracking()
            .Include(entity => entity.UserRoles)
            .Include(entity => entity.RolePermissions)
                .ThenInclude(entity => entity.Permission)
            .OrderBy(entity => entity.Name)
            .ToArray();

        AddAudit("Platform", "RoleListViewed", "Viewed role and permission directory.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return roles.Select(entity => new RoleListItemDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.UserRoles.Count,
                entity.RolePermissions
                    .Select(link => link.Permission!.Code)
                    .OrderBy(code => code)
                    .ToArray()))
            .ToArray();
    }

    public UserListItemDto UpdateUserDepartment(string? token, Guid userId, UpdateUserDepartmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "platform.admin");
        var user = FindUser(userId);
        var department = _dbContext.Departments.SingleOrDefault(entity => entity.Id == request.DepartmentId)
            ?? throw new RuntimeValidationException($"Department '{request.DepartmentId}' was not found.");

        user.DepartmentId = department.Id;
        user.Department = department;

        AddAudit("Platform", "UserDepartmentChanged", $"Moved user {user.DisplayName} to department {department.Name}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapUser(user);
    }

    public UserListItemDto UpdateUserRoles(string? token, Guid userId, UpdateUserRolesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "platform.admin");
        var user = FindUser(userId);
        var requestedRoleIds = request.RoleIds.Distinct().ToArray();

        if (requestedRoleIds.Length == 0)
        {
            throw new RuntimeValidationException("At least one role must be assigned to a user.");
        }

        var roles = _dbContext.Roles
            .Include(entity => entity.RolePermissions)
                .ThenInclude(entity => entity.Permission)
            .Where(entity => requestedRoleIds.Contains(entity.Id))
            .OrderBy(entity => entity.Name)
            .ToArray();

        if (roles.Length != requestedRoleIds.Length)
        {
            throw new RuntimeValidationException("One or more requested roles were not found.");
        }

        if (user.Id == userContext.UserId && !RolesGrantPermission(roles, "platform.admin"))
        {
            throw new RuntimeValidationException("You cannot remove your own platform administration access.");
        }

        _dbContext.UserRoles.RemoveRange(user.UserRoles);
        foreach (var role in roles)
        {
            _dbContext.UserRoles.Add(new UserRoleEntity
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        AddAudit("Platform", "UserRolesChanged", $"Changed roles for {user.DisplayName} to {string.Join(", ", roles.Select(entity => entity.Name))}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapUser(user, roles.Select(entity => entity.Name).OrderBy(name => name).ToArray());
    }

    public PasswordResetResultDto ResetUserPassword(string? token, Guid userId)
    {
        var userContext = RequirePermission(token, "platform.admin");
        var user = FindUser(userId);
        var temporaryPassword = PasswordSecurity.GenerateTemporaryPassword();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        user.PasswordHash = PasswordSecurity.HashPassword(temporaryPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.MustChangePassword = true;
        user.PasswordResetExpiresAt = expiresAt;
        user.FailedAccessCount = 0;
        user.LockedUntil = null;

        AddAudit("Platform", "UserPasswordReset", $"Reset password for {user.DisplayName}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return new PasswordResetResultDto(user.Id, user.UserName, temporaryPassword, user.MustChangePassword, expiresAt);
    }

    public UserListItemDto UnlockUser(string? token, Guid userId)
    {
        var userContext = RequirePermission(token, "platform.admin");
        var user = FindUser(userId);

        user.FailedAccessCount = 0;
        user.LockedUntil = null;

        AddAudit("Platform", "UserUnlocked", $"Unlocked user {user.DisplayName}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapUser(user);
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

    public IReadOnlyList<ReportCatalogItemDto> GetReportCatalog(string? token)
    {
        RequirePermission(token, "reporting.dashboard.read");

        return GetReportingCatalogDefinitions();
    }

    public ReportPreviewDto GetReportPreview(string? token, string reportCode, int take)
    {
        RequirePermission(token, "reporting.dashboard.read");

        var boundedTake = Math.Clamp(take, 1, 200);
        var dataset = BuildReportDataset(reportCode);

        return new ReportPreviewDto(
            dataset.ReportCode,
            dataset.Name,
            DateTimeOffset.UtcNow,
            dataset.Columns,
            dataset.Rows.Take(boundedTake).ToArray(),
            dataset.Rows.Count);
    }

    public IReadOnlyList<ReportExportJobDto> GetReportExportJobs(string? token, int take)
    {
        RequirePermission(token, "reporting.dashboard.read");
        var limit = Math.Clamp(take, 1, 50);

        return _dbContext.ReportExportJobs
            .AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.RequestedAt)
            .Take(limit)
            .Select(MapReportExportJob)
            .ToArray();
    }

    public ReportExportJobDto CreateReportExport(string? token, CreateReportExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "reporting.dashboard.read");

        if (!string.Equals(request.Format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeValidationException("Only CSV export is currently implemented.");
        }

        var dataset = BuildReportDataset(request.ReportCode);
        var content = BuildCsv(dataset.Columns, dataset.Rows);
        var requestedAt = DateTimeOffset.UtcNow;
        var safeCode = dataset.ReportCode.Replace('_', '-');
        var exportJob = new ReportExportJobEntity
        {
            Id = Guid.NewGuid(),
            JobNumber = NextReportExportJobNumber(),
            ReportCode = dataset.ReportCode,
            ReportName = dataset.Name,
            Format = "CSV",
            State = "Completed",
            RowCount = dataset.Rows.Count,
            RequestedBy = userContext.UserName,
            RequestedAt = requestedAt,
            CompletedAt = requestedAt,
            FileName = $"{safeCode}-{requestedAt:yyyyMMddHHmmss}.csv",
            Content = content,
            ErrorMessage = null
        };

        _dbContext.ReportExportJobs.Add(exportJob);
        AddAudit("Reporting", "ReportExportCreated", $"Created {exportJob.Format} export {exportJob.JobNumber} for report {dataset.ReportCode}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapReportExportJob(exportJob);
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

    private IReadOnlyList<ReportCatalogItemDto> GetReportingCatalogDefinitions()
    {
        return
        [
            new ReportCatalogItemDto("sales-orders", "Sales Orders", "CRM", "Order pipeline, commercial status, and order-to-production linkage.", 20),
            new ReportCatalogItemDto("production-status", "Production Status", "MES", "Production orders, work orders, and execution output status.", 20),
            new ReportCatalogItemDto("inventory-balances", "Inventory Balances", "ERP", "On-hand, pending-inspection, and released inventory by warehouse.", 20),
            new ReportCatalogItemDto("warehouse-transfers", "Warehouse Transfers", "ERP", "Internal warehouse transfer execution, route, batch, and valuation detail.", 20),
            new ReportCatalogItemDto("quality-inspections", "Quality Inspections", "Quality", "Inspection queue, disposition, and defect results.", 20),
            new ReportCatalogItemDto("cost-ledgers", "Cost Ledgers", "ERP", "Actual material, labor, machine, overhead, and scrap cost by work order.", 20),
            new ReportCatalogItemDto("invoices", "Invoices", "ERP", "Operational AP and AR invoice support documents with issuance and settlement state.", 20),
            new ReportCatalogItemDto("settlements", "Settlements", "ERP", "Operational receivable and payable support entries with outstanding balances.", 20),
            new ReportCatalogItemDto("settlement-applications", "Settlement Applications", "ERP", "Cash application and offset history against open receivable and payable entries.", 20),
            new ReportCatalogItemDto("settlement-aging", "Settlement Aging", "ERP", "Outstanding receivable and payable balances bucketed by due-date aging.", 20),
            new ReportCatalogItemDto("accounting-periods", "Accounting Periods", "ERP", "Operational period status and close-control checkpoints.", 20),
            new ReportCatalogItemDto("accounting-vouchers", "Accounting Vouchers", "ERP", "Balanced journal vouchers generated from posted bridge entries.", 20),
            new ReportCatalogItemDto("accounting-bridges", "Accounting Bridges", "Integration", "Finance bridge entries awaiting export or acknowledgement.", 20)
        ];
    }

    private ReportDataset BuildReportDataset(string reportCode)
    {
        var normalizedCode = reportCode.Trim().ToLowerInvariant();

        return normalizedCode switch
        {
            "sales-orders" => new ReportDataset(
                "sales-orders",
                "Sales Orders",
                ["Order Number", "Customer", "Product", "State", "Order Date", "Requested Delivery", "Quantity", "Total Amount", "Production Order"],
                ListSalesOrderRows()),
            "production-status" => new ReportDataset(
                "production-status",
                "Production Status",
                ["Production Order", "Sales Order", "Product", "State", "Planned Quantity", "Work Order", "Good Quantity", "Scrap Quantity", "Planned Start", "Planned End"],
                ListProductionStatusRows()),
            "inventory-balances" => new ReportDataset(
                "inventory-balances",
                "Inventory Balances",
                ["Item Code", "Item Name", "Warehouse", "On Hand", "Pending Inspection", "Released", "Average Unit Cost"],
                ListInventoryBalanceRows()),
            "warehouse-transfers" => new ReportDataset(
                "warehouse-transfers",
                "Warehouse Transfers",
                ["Transfer Number", "From Warehouse", "To Warehouse", "Item Code", "Item Name", "Batch", "Quantity", "Quality Status", "Unit Cost", "Total Cost", "Transferred At"],
                ListWarehouseTransferRows()),
            "quality-inspections" => new ReportDataset(
                "quality-inspections",
                "Quality Inspections",
                ["Inspection Number", "Type", "Work Order", "Production Order", "Batch", "Inspected Qty", "Defect Qty", "State", "Disposition", "Inspector"],
                ListQualityInspectionRows()),
            "cost-ledgers" => new ReportDataset(
                "cost-ledgers",
                "Cost Ledgers",
                ["Work Order", "Production Order", "Material", "Labor", "Machine", "Overhead", "Scrap", "Total Cost", "Unit Cost"],
                ListCostLedgerRows()),
            "invoices" => new ReportDataset(
                "invoices",
                "Invoices",
                ["Invoice Number", "Type", "Counterparty", "Source Type", "Source Number", "Settlement Entry", "Amount", "Issued On", "Due Date", "State"],
                ListInvoiceRows()),
            "settlements" => new ReportDataset(
                "settlements",
                "Settlements",
                ["Entry Number", "Type", "Counterparty", "Source Type", "Source Number", "Invoice Number", "Total Amount", "Settled Amount", "Outstanding Amount", "Due Date", "State", "Last Settled At"],
                ListSettlementRows()),
            "settlement-applications" => new ReportDataset(
                "settlement-applications",
                "Settlement Applications",
                ["Application Number", "Settlement Entry", "Counterparty", "Invoice Number", "Amount", "Reference", "Applied By", "Applied At"],
                ListSettlementApplicationRows()),
            "settlement-aging" => new ReportDataset(
                "settlement-aging",
                "Settlement Aging",
                ["Type", "Open Entries", "Current", "1-30 Days", "31-60 Days", "61-90 Days", "Over 90 Days", "Total Outstanding"],
                ListSettlementAgingRows()),
            "accounting-periods" => new ReportDataset(
                "accounting-periods",
                "Accounting Periods",
                ["Period", "Start Date", "End Date", "State", "Closed By", "Closed At", "Notes"],
                ListAccountingPeriodRows()),
            "accounting-vouchers" => new ReportDataset(
                "accounting-vouchers",
                "Accounting Vouchers",
                ["Voucher Number", "Period", "State", "Debit", "Credit", "Lines", "Created By", "Created At", "Posted By", "Posted At"],
                ListAccountingVoucherRows()),
            "accounting-bridges" => new ReportDataset(
                "accounting-bridges",
                "Accounting Bridges",
                ["Bridge Number", "Source Document", "Source Event", "Posting Account", "Amount", "State", "Generated At"],
                ListAccountingBridgeRows()),
            _ => throw new RuntimeValidationException($"Report '{reportCode}' is not part of the approved reporting catalog.")
        };
    }

    private IReadOnlyList<IReadOnlyList<string>> ListSalesOrderRows()
    {
        return _dbContext.SalesOrders
            .AsNoTracking()
            .Include(entity => entity.Customer)
            .OrderByDescending(entity => entity.OrderDate)
            .ThenByDescending(entity => entity.OrderNumber)
            .ToArray()
            .Select(entity =>
            {
                var productionOrderNumber = entity.ProductionOrderId.HasValue
                    ? _dbContext.ProductionOrders.AsNoTracking().Where(order => order.Id == entity.ProductionOrderId.Value).Select(order => order.ProductionOrderNumber).SingleOrDefault()
                    : null;

                return (IReadOnlyList<string>)
                [
                    entity.OrderNumber,
                    entity.Customer?.Name ?? string.Empty,
                    entity.ProductCode,
                    entity.State.ToString(),
                    entity.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    entity.RequestedDeliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    FormatDecimal(entity.Quantity),
                    FormatDecimal(entity.TotalAmount),
                    productionOrderNumber ?? string.Empty
                ];
            })
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListProductionStatusRows()
    {
        return _dbContext.ProductionOrders
            .AsNoTracking()
            .Include(entity => entity.SalesOrder)
            .Include(entity => entity.WorkOrder)
            .OrderBy(entity => entity.PlannedStartDate)
            .ThenBy(entity => entity.ProductionOrderNumber)
            .ToArray()
            .Select(entity => (IReadOnlyList<string>)
            [
                entity.ProductionOrderNumber,
                entity.SalesOrder?.OrderNumber ?? string.Empty,
                entity.ProductCode,
                entity.State.ToString(),
                FormatDecimal(entity.PlannedQuantity),
                entity.WorkOrder?.WorkOrderNumber ?? string.Empty,
                FormatDecimal(entity.WorkOrder?.GoodQuantity ?? 0m),
                FormatDecimal(entity.WorkOrder?.ScrapQuantity ?? 0m),
                entity.PlannedStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entity.PlannedEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListInventoryBalanceRows()
    {
        return ListInventoryBalances()
            .Select(item => (IReadOnlyList<string>)
            [
                item.ItemCode,
                item.ItemName,
                item.WarehouseCode,
                FormatDecimal(item.OnHandQuantity),
                FormatDecimal(item.PendingInspectionQuantity),
                FormatDecimal(item.ReleasedQuantity),
                FormatDecimal(item.AverageUnitCost)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListWarehouseTransferRows()
    {
        return _dbContext.WarehouseTransfers
            .AsNoTracking()
            .Include(entity => entity.FromWarehouse)
            .Include(entity => entity.ToWarehouse)
            .ToArray()
            .OrderByDescending(entity => entity.TransferredAt)
            .ThenByDescending(entity => entity.TransferNumber)
            .Select(item => (IReadOnlyList<string>)
            [
                item.TransferNumber,
                item.FromWarehouse?.Code ?? string.Empty,
                item.ToWarehouse?.Code ?? string.Empty,
                item.ItemCode,
                item.ItemName,
                item.BatchNumber,
                FormatDecimal(item.Quantity),
                item.QualityStatus.ToString(),
                FormatDecimal(item.UnitCost),
                FormatDecimal(item.TotalCost),
                item.TransferredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListQualityInspectionRows()
    {
        return ListQualityInspections()
            .Select(item => (IReadOnlyList<string>)
            [
                item.InspectionNumber,
                item.InspectionType,
                item.WorkOrderNumber,
                item.ProductionOrderNumber,
                item.BatchNumber,
                FormatDecimal(item.InspectedQuantity),
                FormatDecimal(item.DefectQuantity),
                item.State,
                item.Disposition ?? string.Empty,
                item.Inspector ?? string.Empty
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListCostLedgerRows()
    {
        return ListCostLedgers()
            .Select(item => (IReadOnlyList<string>)
            [
                item.WorkOrderNumber,
                item.ProductionOrderNumber,
                FormatDecimal(item.MaterialCost),
                FormatDecimal(item.LaborCost),
                FormatDecimal(item.MachineCost),
                FormatDecimal(item.OverheadCost),
                FormatDecimal(item.ScrapCost),
                FormatDecimal(item.TotalCost),
                FormatDecimal(item.UnitCost)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListInvoiceRows()
    {
        return _dbContext.Invoices
            .AsNoTracking()
            .Include(entity => entity.SettlementEntry)
            .ToArray()
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.InvoiceNumber)
            .Select(item => (IReadOnlyList<string>)
            [
                item.InvoiceNumber,
                item.InvoiceType.ToString(),
                item.CounterpartyName,
                item.SourceDocumentType,
                item.SourceDocumentNumber,
                item.SettlementEntry?.EntryNumber ?? string.Empty,
                FormatDecimal(item.Amount),
                item.IssuedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.State.ToString()
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListSettlementRows()
    {
        return _dbContext.SettlementEntries
            .AsNoTracking()
            .Include(entity => entity.Invoice)
            .ToArray()
            .OrderBy(entity => entity.State == SettlementState.Settled)
            .ThenBy(entity => entity.DueDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .Select(item => (IReadOnlyList<string>)
            [
                item.EntryNumber,
                item.SettlementType.ToString(),
                item.CounterpartyName,
                item.SourceDocumentType,
                item.SourceDocumentNumber,
                item.Invoice?.InvoiceNumber ?? string.Empty,
                FormatDecimal(item.TotalAmount),
                FormatDecimal(item.SettledAmount),
                FormatDecimal(item.TotalAmount - item.SettledAmount),
                item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.State.ToString(),
                item.LastSettledAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListSettlementApplicationRows()
    {
        return _dbContext.SettlementApplications
            .AsNoTracking()
            .Include(entity => entity.SettlementEntry)
            .Include(entity => entity.Invoice)
            .ToArray()
            .OrderByDescending(entity => entity.AppliedAt)
            .ThenByDescending(entity => entity.ApplicationNumber)
            .Select(item => (IReadOnlyList<string>)
            [
                item.ApplicationNumber,
                item.SettlementEntry?.EntryNumber ?? string.Empty,
                item.SettlementEntry?.CounterpartyName ?? string.Empty,
                item.Invoice?.InvoiceNumber ?? string.Empty,
                FormatDecimal(item.Amount),
                item.Reference ?? string.Empty,
                item.AppliedBy,
                item.AppliedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListSettlementAgingRows()
    {
        return GetSettlementAgingBuckets()
            .Select(item => (IReadOnlyList<string>)
            [
                item.SettlementType,
                item.OpenEntries.ToString(CultureInfo.InvariantCulture),
                FormatDecimal(item.CurrentAmount),
                FormatDecimal(item.Days1To30Amount),
                FormatDecimal(item.Days31To60Amount),
                FormatDecimal(item.Days61To90Amount),
                FormatDecimal(item.Over90DaysAmount),
                FormatDecimal(item.TotalOutstandingAmount)
            ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListAccountingPeriodRows()
    {
        EnsureAccountingPeriodsForHorizon(DateOnly.FromDateTime(DateTime.UtcNow));

        return _dbContext.AccountingPeriods
            .AsNoTracking()
            .ToArray()
            .OrderByDescending(entity => entity.PeriodCode)
            .Select(item => (IReadOnlyList<string>)
            [
                item.PeriodCode,
                item.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.State.ToString(),
                item.ClosedBy ?? string.Empty,
                item.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                item.CloseNotes ?? string.Empty
                ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListAccountingVoucherRows()
    {
        return _dbContext.AccountingVouchers
            .AsNoTracking()
            .ToArray()
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.VoucherNumber)
            .Select(entity => (IReadOnlyList<string>)
                [
                    entity.VoucherNumber,
                    entity.PeriodCode,
                    entity.State.ToString(),
                    entity.TotalDebitAmount.ToString("0.##", CultureInfo.InvariantCulture),
                    entity.TotalCreditAmount.ToString("0.##", CultureInfo.InvariantCulture),
                    entity.LineCount.ToString(CultureInfo.InvariantCulture),
                    entity.CreatedBy,
                    entity.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    entity.PostedBy ?? string.Empty,
                    entity.PostedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty
                ])
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyList<string>> ListAccountingBridgeRows()
    {
        return ListAccountingBridges()
            .Select(item => (IReadOnlyList<string>)
            [
                item.BridgeNumber,
                item.SourceDocumentNumber,
                item.SourceEvent,
                item.PostingAccount,
                FormatDecimal(item.Amount),
                item.State,
                item.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            ])
            .ToArray();
    }

    private static string BuildCsv(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var lines = new List<string>(rows.Count + 1)
        {
            string.Join(",", columns.Select(EscapeCsv))
        };

        lines.AddRange(rows.Select(row => string.Join(",", row.Select(EscapeCsv))));
        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value}\""
            : value;
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
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

    public IReadOnlyList<WarehouseListItemDto> GetWarehouses(string? token)
    {
        RequirePermission(token, "erp.inventory.read");

        return _dbContext.Warehouses
            .AsNoTracking()
            .OrderBy(entity => entity.Code)
            .Select(entity => new WarehouseListItemDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.IsQualityControlled,
                entity.SupportsWip))
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

        var receiptAmount = decimal.Round(request.ReceivedQuantity * purchaseOrder.UnitPrice, 2);

        _dbContext.AccountingBridgeEntries.Add(
            AccountingBridge(receipt.ReceiptNumber, "Purchase receipt accrual", receiptAmount, "AP-GRNI"));
        _dbContext.SettlementEntries.Add(
            CreateSettlementEntry(
                SettlementType.Payable,
                "GoodsReceipt",
                receipt.ReceiptNumber,
                purchaseOrder.Supplier!.Name,
                receiptAmount,
                CalculateDueDate(purchaseOrder.Supplier.PaymentTerms, 30)));

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

    public IReadOnlyList<WarehouseTransferListItemDto> GetWarehouseTransfers(string? token)
    {
        RequirePermission(token, "erp.inventory.read");

        return _dbContext.WarehouseTransfers
            .AsNoTracking()
            .Include(entity => entity.FromWarehouse)
            .Include(entity => entity.ToWarehouse)
            .ToArray()
            .OrderByDescending(entity => entity.TransferredAt)
            .ThenByDescending(entity => entity.TransferNumber)
            .Select(MapWarehouseTransfer)
            .ToArray();
    }

    public WarehouseTransferListItemDto CreateWarehouseTransfer(string? token, CreateWarehouseTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.transfer.write");

        if (string.IsNullOrWhiteSpace(request.ItemCode))
        {
            throw new RuntimeValidationException("An item code is required for warehouse transfer.");
        }

        if (request.Quantity <= 0)
        {
            throw new RuntimeValidationException("Transfer quantity must be greater than zero.");
        }

        var fromWarehouse = FindWarehouseByCode(request.FromWarehouseCode);
        var toWarehouse = FindWarehouseByCode(request.ToWarehouseCode);

        if (fromWarehouse.Id == toWarehouse.Id)
        {
            throw new RuntimeValidationException("Source and destination warehouse must be different.");
        }

        var normalizedItemCode = request.ItemCode.Trim().ToUpperInvariant();
        var itemName = ResolveInventoryItemName(normalizedItemCode);
        var selectedLot = SelectTransferLot(fromWarehouse.Id, normalizedItemCode, request.BatchNumber, request.Quantity);
        var transfer = new WarehouseTransferEntity
        {
            Id = Guid.NewGuid(),
            TransferNumber = NextWarehouseTransferNumber(),
            FromWarehouseId = fromWarehouse.Id,
            ToWarehouseId = toWarehouse.Id,
            ItemCode = normalizedItemCode,
            ItemName = itemName,
            Quantity = request.Quantity,
            BatchNumber = selectedLot.BatchNumber,
            UnitCost = selectedLot.UnitCost,
            TotalCost = decimal.Round(request.Quantity * selectedLot.UnitCost, 2),
            QualityStatus = selectedLot.QualityStatus,
            State = WarehouseTransferState.Completed,
            TransferredAt = DateTimeOffset.UtcNow
        };

        var inventorySequence = NextNumberSequence(
            "ITX",
            _dbContext.InventoryTransactions.AsNoTracking().Select(entity => entity.TransactionNumber).AsEnumerable(),
            _dbContext.InventoryTransactions.Local.Select(entity => entity.TransactionNumber));

        _dbContext.WarehouseTransfers.Add(transfer);
        _dbContext.InventoryTransactions.AddRange(
            new InventoryTransactionEntity
            {
                Id = Guid.NewGuid(),
                TransactionNumber = FormatNumber("ITX", inventorySequence),
                TransactionType = InventoryTransactionType.Transfer,
                WarehouseId = fromWarehouse.Id,
                ItemCode = transfer.ItemCode,
                Quantity = -request.Quantity,
                BatchNumber = transfer.BatchNumber,
                UnitCost = transfer.UnitCost,
                TotalCost = -transfer.TotalCost,
                QualityStatus = transfer.QualityStatus,
                SourceDocumentNumber = transfer.TransferNumber,
                SourceEvent = $"Warehouse transfer to {toWarehouse.Code}",
                OccurredAt = transfer.TransferredAt
            },
            new InventoryTransactionEntity
            {
                Id = Guid.NewGuid(),
                TransactionNumber = FormatNumber("ITX", inventorySequence + 1),
                TransactionType = InventoryTransactionType.Transfer,
                WarehouseId = toWarehouse.Id,
                ItemCode = transfer.ItemCode,
                Quantity = request.Quantity,
                BatchNumber = transfer.BatchNumber,
                UnitCost = transfer.UnitCost,
                TotalCost = transfer.TotalCost,
                QualityStatus = transfer.QualityStatus,
                SourceDocumentNumber = transfer.TransferNumber,
                SourceEvent = $"Warehouse transfer from {fromWarehouse.Code}",
                OccurredAt = transfer.TransferredAt
            });

        AddAudit(
            "ERP",
            "WarehouseTransferCompleted",
            $"Transferred {request.Quantity} of {transfer.ItemCode} from {fromWarehouse.Code} to {toWarehouse.Code} on {transfer.TransferNumber}.",
            userContext.UserName,
            userContext.UserId);
        _dbContext.SaveChanges();

        transfer.FromWarehouse = fromWarehouse;
        transfer.ToWarehouse = toWarehouse;
        return MapWarehouseTransfer(transfer);
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
        _dbContext.SettlementEntries.Add(
            CreateSettlementEntry(
                SettlementType.Receivable,
                "SalesShipment",
                shipment.ShipmentNumber,
                salesOrder.Customer!.Name,
                shipmentValue,
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30)));

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

        return ListInventoryBalances();
    }

    private IReadOnlyList<InventoryBalanceDto> ListInventoryBalances()
    {
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

        return ListCostLedgers();
    }

    private IReadOnlyList<CostLedgerItemDto> ListCostLedgers()
    {
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

    public IReadOnlyList<InvoiceListItemDto> GetInvoices(string? token)
    {
        RequirePermission(token, "erp.invoice.read");

        return _dbContext.Invoices
            .AsNoTracking()
            .Include(entity => entity.SettlementEntry)
            .ToArray()
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.InvoiceNumber)
            .Select(MapInvoice)
            .ToArray();
    }

    public InvoiceListItemDto CreateInvoice(string? token, CreateInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.invoice.write");
        var settlement = _dbContext.SettlementEntries
            .Include(entity => entity.Invoice)
            .SingleOrDefault(entity => entity.Id == request.SettlementEntryId)
            ?? throw new RuntimeNotFoundException($"Settlement entry '{request.SettlementEntryId}' was not found.");

        if (settlement.Invoice is not null)
        {
            throw new RuntimeValidationException($"Settlement entry {settlement.EntryNumber} already has invoice {settlement.Invoice.InvoiceNumber}.");
        }

        if (settlement.State != SettlementState.Open)
        {
            throw new RuntimeValidationException("Invoices must be created before any settlement is applied.");
        }

        EnsureAccountingPeriodsForHorizon(request.IssuedOn);
        var accountingPeriod = FindAccountingPeriodByDate(request.IssuedOn);
        if (accountingPeriod.State == AccountingPeriodState.Closed)
        {
            throw new RuntimeValidationException($"Accounting period {accountingPeriod.PeriodCode} is closed.");
        }

        var invoice = new InvoiceEntity
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = NextInvoiceNumber(),
            InvoiceType = settlement.SettlementType == SettlementType.Receivable ? InvoiceType.Receivable : InvoiceType.Payable,
            State = InvoiceState.Draft,
            SettlementEntryId = settlement.Id,
            SourceDocumentType = settlement.SourceDocumentType,
            SourceDocumentNumber = settlement.SourceDocumentNumber,
            CounterpartyName = settlement.CounterpartyName,
            Amount = settlement.TotalAmount,
            IssuedOn = request.IssuedOn,
            DueDate = settlement.DueDate,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Invoices.Add(invoice);
        AddAudit(
            "ERP",
            "InvoiceCreated",
            $"Created draft invoice {invoice.InvoiceNumber} from settlement {settlement.EntryNumber}.",
            userContext.UserName,
            userContext.UserId);
        _dbContext.SaveChanges();

        invoice.SettlementEntry = settlement;
        return MapInvoice(invoice);
    }

    public InvoiceListItemDto IssueInvoice(string? token, Guid invoiceId)
    {
        var userContext = RequirePermission(token, "erp.invoice.write");
        var invoice = _dbContext.Invoices
            .Include(entity => entity.SettlementEntry)
            .SingleOrDefault(entity => entity.Id == invoiceId)
            ?? throw new RuntimeNotFoundException($"Invoice '{invoiceId}' was not found.");

        if (invoice.State != InvoiceState.Draft)
        {
            throw new RuntimeValidationException("Only draft invoices can be issued.");
        }

        EnsureAccountingPeriodsForHorizon(invoice.IssuedOn);
        var accountingPeriod = FindAccountingPeriodByDate(invoice.IssuedOn);
        if (accountingPeriod.State == AccountingPeriodState.Closed)
        {
            throw new RuntimeValidationException($"Accounting period {accountingPeriod.PeriodCode} is closed.");
        }

        invoice.State = InvoiceState.Issued;

        var sourceEvent = invoice.InvoiceType == InvoiceType.Payable
            ? $"Supplier invoice registered for {invoice.SourceDocumentNumber}"
            : $"Customer invoice issued for {invoice.SourceDocumentNumber}";
        var postingAccount = invoice.InvoiceType == InvoiceType.Payable
            ? "AP-INVOICE"
            : "AR-INVOICE";

        _dbContext.AccountingBridgeEntries.Add(
            AccountingBridge(invoice.InvoiceNumber, sourceEvent, invoice.Amount, postingAccount));

        AddAudit("ERP", "InvoiceIssued", $"Issued invoice {invoice.InvoiceNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapInvoice(invoice);
    }

    public IReadOnlyList<SettlementEntryDto> GetSettlements(string? token)
    {
        RequirePermission(token, "erp.settlement.read");

        return _dbContext.SettlementEntries
            .AsNoTracking()
            .Include(entity => entity.Invoice)
            .ToArray()
            .OrderBy(entity => entity.State == SettlementState.Settled)
            .ThenBy(entity => entity.DueDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .Select(MapSettlement)
            .ToArray();
    }

    public SettlementEntryDto ApplySettlement(string? token, Guid settlementEntryId, ApplySettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.settlement.write");
        var settlement = _dbContext.SettlementEntries
            .Include(entity => entity.Invoice)
            .SingleOrDefault(entity => entity.Id == settlementEntryId)
            ?? throw new RuntimeNotFoundException($"Settlement entry '{settlementEntryId}' was not found.");

        if (settlement.State is SettlementState.Cancelled or SettlementState.Settled)
        {
            throw new RuntimeValidationException("Only open or partially settled entries can receive settlements.");
        }

        if (settlement.Invoice is not null && settlement.Invoice.State == InvoiceState.Draft)
        {
            throw new RuntimeValidationException("Draft invoices must be issued before settlement can be applied.");
        }

        if (request.Amount <= 0)
        {
            throw new RuntimeValidationException("Settlement amount must be greater than zero.");
        }

        var outstandingAmount = settlement.TotalAmount - settlement.SettledAmount;
        if (request.Amount > outstandingAmount)
        {
            throw new RuntimeValidationException("Settlement amount cannot exceed the outstanding amount.");
        }

        settlement.SettledAmount = decimal.Round(settlement.SettledAmount + request.Amount, 2);
        settlement.LastSettledAt = DateTimeOffset.UtcNow;
        settlement.State = settlement.SettledAmount >= settlement.TotalAmount
            ? SettlementState.Settled
            : SettlementState.PartiallySettled;

        var settlementApplication = new SettlementApplicationEntity
        {
            Id = Guid.NewGuid(),
            ApplicationNumber = NextSettlementApplicationNumber(),
            SettlementEntryId = settlement.Id,
            InvoiceId = settlement.Invoice?.Id,
            Amount = decimal.Round(request.Amount, 2),
            Reference = request.Reference?.Trim(),
            AppliedBy = userContext.UserName,
            AppliedAt = settlement.LastSettledAt.Value
        };

        _dbContext.SettlementApplications.Add(settlementApplication);

        if (settlement.Invoice is not null)
        {
            settlement.Invoice.State = settlement.State switch
            {
                SettlementState.Settled => InvoiceState.Settled,
                SettlementState.PartiallySettled => InvoiceState.PartiallySettled,
                _ => settlement.Invoice.State
            };
        }

        var sourceEvent = settlement.SettlementType == SettlementType.Payable
            ? $"Supplier settlement for {settlement.SourceDocumentNumber}"
            : $"Customer settlement for {settlement.SourceDocumentNumber}";
        var postingAccount = settlement.SettlementType == SettlementType.Payable
            ? "AP-SETTLEMENT"
            : "AR-SETTLEMENT";

        _dbContext.AccountingBridgeEntries.Add(
            AccountingBridge(settlement.EntryNumber, sourceEvent, request.Amount, postingAccount));

        var referenceSuffix = string.IsNullOrWhiteSpace(request.Reference)
            ? string.Empty
            : $" Reference: {request.Reference.Trim()}.";

        AddAudit(
            "ERP",
            "SettlementApplied",
            $"Applied {request.Amount.ToString("0.##", CultureInfo.InvariantCulture)} to settlement {settlement.EntryNumber}.{referenceSuffix}",
            userContext.UserName,
            userContext.UserId);
        _dbContext.SaveChanges();

        return MapSettlement(settlement);
    }

    public IReadOnlyList<SettlementApplicationDto> GetSettlementApplications(string? token)
    {
        RequirePermission(token, "erp.settlement.read");

        return _dbContext.SettlementApplications
            .AsNoTracking()
            .Include(entity => entity.SettlementEntry)
            .Include(entity => entity.Invoice)
            .ToArray()
            .OrderByDescending(entity => entity.AppliedAt)
            .ThenByDescending(entity => entity.ApplicationNumber)
            .Select(MapSettlementApplication)
            .ToArray();
    }

    public IReadOnlyList<SettlementAgingBucketDto> GetSettlementAging(string? token)
    {
        RequirePermission(token, "erp.settlement.read");

        return GetSettlementAgingBuckets();
    }

    public IReadOnlyList<AccountingPeriodDto> GetAccountingPeriods(string? token)
    {
        RequirePermission(token, "erp.invoice.read");
        EnsureAccountingPeriodsForHorizon(DateOnly.FromDateTime(DateTime.UtcNow));

        return _dbContext.AccountingPeriods
            .AsNoTracking()
            .ToArray()
            .OrderByDescending(entity => entity.PeriodCode)
            .Select(MapAccountingPeriod)
            .ToArray();
    }

    public AccountingPeriodDto CloseAccountingPeriod(string? token, Guid accountingPeriodId, CloseAccountingPeriodRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.period.close");
        var period = FindAccountingPeriod(accountingPeriodId);

        if (period.State == AccountingPeriodState.Closed)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} is already closed.");
        }

        var unresolvedBridgeExists = _dbContext.AccountingBridgeEntries
            .AsNoTracking()
            .ToArray()
            .Any(entity =>
            {
                var generatedDate = DateOnly.FromDateTime(entity.GeneratedAt.UtcDateTime);
                return generatedDate >= period.StartDate &&
                       generatedDate <= period.EndDate &&
                       entity.State is AccountingBridgeState.ReadyForExport or AccountingBridgeState.Exported or AccountingBridgeState.Failed;
            });

        if (unresolvedBridgeExists)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} still has unresolved accounting bridge entries.");
        }

        var settlementInvoiceGapExists = _dbContext.SettlementEntries
            .AsNoTracking()
            .Include(entity => entity.Invoice)
            .ToArray()
            .Any(entity =>
            {
                var createdDate = DateOnly.FromDateTime(entity.CreatedAt.UtcDateTime);
                return createdDate >= period.StartDate &&
                       createdDate <= period.EndDate &&
                       entity.State != SettlementState.Cancelled &&
                       (entity.Invoice is null || entity.Invoice.State == InvoiceState.Draft);
            });

        if (settlementInvoiceGapExists)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} has settlement entries without issued invoices.");
        }

        var bridgeIdsWithVouchers = _dbContext.AccountingVoucherLines
            .AsNoTracking()
            .Select(entity => entity.AccountingBridgeEntryId)
            .Distinct()
            .ToHashSet();

        var unvoucheredPostedBridgeExists = _dbContext.AccountingBridgeEntries
            .AsNoTracking()
            .ToArray()
            .Any(entity =>
            {
                var generatedDate = DateOnly.FromDateTime(entity.GeneratedAt.UtcDateTime);
                return generatedDate >= period.StartDate &&
                       generatedDate <= period.EndDate &&
                       entity.State is AccountingBridgeState.Posted or AccountingBridgeState.Reconciled &&
                       !bridgeIdsWithVouchers.Contains(entity.Id);
            });

        if (unvoucheredPostedBridgeExists)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} has posted bridge entries without accounting vouchers.");
        }

        var unpostedVoucherExists = _dbContext.AccountingVouchers
            .AsNoTracking()
            .Any(entity =>
                entity.PeriodCode == period.PeriodCode &&
                (entity.State == AccountingVoucherState.Draft || entity.State == AccountingVoucherState.Approved));

        if (unpostedVoucherExists)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} has accounting vouchers that are not posted.");
        }

        period.State = AccountingPeriodState.Closed;
        period.ClosedBy = userContext.UserName;
        period.ClosedAt = DateTimeOffset.UtcNow;
        period.CloseNotes = request.Notes?.Trim();

        AddAudit("ERP", "AccountingPeriodClosed", $"Closed accounting period {period.PeriodCode}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingPeriod(period);
    }

    public IReadOnlyList<AccountingVoucherDto> GetAccountingVouchers(string? token)
    {
        RequirePermission(token, "erp.accounting.manage");

        return _dbContext.AccountingVouchers
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .ToArray()
            .OrderByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.VoucherNumber)
            .Select(MapAccountingVoucher)
            .ToArray();
    }

    public AccountingVoucherDto CreateAccountingVoucher(string? token, CreateAccountingVoucherRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "erp.accounting.manage");
        var bridgeIds = request.AccountingBridgeIds.Distinct().ToArray();

        if (bridgeIds.Length == 0)
        {
            throw new RuntimeValidationException("Select at least one accounting bridge entry.");
        }

        var bridges = _dbContext.AccountingBridgeEntries
            .Where(entity => bridgeIds.Contains(entity.Id))
            .ToArray()
            .OrderBy(entity => entity.GeneratedAt)
            .ThenBy(entity => entity.BridgeNumber)
            .ToArray();

        if (bridges.Length != bridgeIds.Length)
        {
            throw new RuntimeValidationException("One or more accounting bridge entries were not found.");
        }

        var invalidStateBridge = bridges.FirstOrDefault(entity => entity.State is not (AccountingBridgeState.Posted or AccountingBridgeState.Reconciled));
        if (invalidStateBridge is not null)
        {
            throw new RuntimeValidationException($"Bridge {invalidStateBridge.BridgeNumber} must be posted or reconciled before voucher creation.");
        }

        var zeroAmountBridge = bridges.FirstOrDefault(entity => entity.Amount <= 0);
        if (zeroAmountBridge is not null)
        {
            throw new RuntimeValidationException($"Bridge {zeroAmountBridge.BridgeNumber} has zero or negative amount and cannot be vouchered.");
        }

        var alreadyVoucheredBridgeNumbers = _dbContext.AccountingVoucherLines
            .AsNoTracking()
            .Where(entity => bridgeIds.Contains(entity.AccountingBridgeEntryId))
            .Select(entity => entity.SourceBridgeNumber)
            .Distinct()
            .ToArray();

        if (alreadyVoucheredBridgeNumbers.Length > 0)
        {
            throw new RuntimeValidationException($"Bridge entries already vouchered: {string.Join(", ", alreadyVoucheredBridgeNumbers)}.");
        }

        string? periodCode = null;
        foreach (var bridge in bridges)
        {
            var generatedDate = DateOnly.FromDateTime(bridge.GeneratedAt.UtcDateTime);
            var accountingPeriod = FindAccountingPeriodByDate(generatedDate);

            if (accountingPeriod.State == AccountingPeriodState.Closed)
            {
                throw new RuntimeValidationException($"Accounting period {accountingPeriod.PeriodCode} is closed.");
            }

            periodCode ??= accountingPeriod.PeriodCode;
            if (!string.Equals(periodCode, accountingPeriod.PeriodCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new RuntimeValidationException("A voucher can only include bridge entries from one accounting period.");
            }
        }

        var voucher = new AccountingVoucherEntity
        {
            Id = Guid.NewGuid(),
            VoucherNumber = NextAccountingVoucherNumber(),
            PeriodCode = periodCode ?? throw new RuntimeValidationException("Unable to resolve accounting period for voucher."),
            State = AccountingVoucherState.Draft,
            TotalDebitAmount = 0m,
            TotalCreditAmount = 0m,
            LineCount = 0,
            CreatedBy = userContext.UserName,
            CreatedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes?.Trim()
        };

        var lineNumber = 1;
        foreach (var bridge in bridges)
        {
            foreach (var line in BuildAccountingVoucherLines(bridge))
            {
                voucher.Lines.Add(new AccountingVoucherLineEntity
                {
                    Id = Guid.NewGuid(),
                    AccountingVoucherId = voucher.Id,
                    AccountingBridgeEntryId = bridge.Id,
                    LineNumber = lineNumber++,
                    SourceBridgeNumber = bridge.BridgeNumber,
                    SourceDocumentNumber = bridge.SourceDocumentNumber,
                    SourceEvent = bridge.SourceEvent,
                    AccountCode = line.AccountCode,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    Narrative = line.Narrative
                });
            }
        }

        voucher.TotalDebitAmount = decimal.Round(voucher.Lines.Sum(line => line.DebitAmount), 2);
        voucher.TotalCreditAmount = decimal.Round(voucher.Lines.Sum(line => line.CreditAmount), 2);
        voucher.LineCount = voucher.Lines.Count;

        if (voucher.TotalDebitAmount != voucher.TotalCreditAmount)
        {
            throw new RuntimeValidationException("Generated voucher is not balanced.");
        }

        _dbContext.AccountingVouchers.Add(voucher);
        AddAudit("ERP", "AccountingVoucherCreated", $"Created accounting voucher {voucher.VoucherNumber} from {bridges.Length} bridge entries.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingVoucher(voucher);
    }

    public AccountingVoucherDto ApproveAccountingVoucher(string? token, Guid accountingVoucherId)
    {
        var userContext = RequirePermission(token, "erp.accounting.manage");
        var voucher = FindAccountingVoucher(accountingVoucherId);

        if (voucher.State != AccountingVoucherState.Draft)
        {
            throw new RuntimeValidationException("Only draft vouchers can be approved.");
        }

        EnsureVoucherPeriodIsOpen(voucher);
        voucher.State = AccountingVoucherState.Approved;
        voucher.ApprovedBy = userContext.UserName;
        voucher.ApprovedAt = DateTimeOffset.UtcNow;

        AddAudit("ERP", "AccountingVoucherApproved", $"Approved accounting voucher {voucher.VoucherNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingVoucher(voucher);
    }

    public AccountingVoucherDto PostAccountingVoucher(string? token, Guid accountingVoucherId)
    {
        var userContext = RequirePermission(token, "erp.accounting.manage");
        var voucher = FindAccountingVoucher(accountingVoucherId);

        if (voucher.State != AccountingVoucherState.Approved)
        {
            throw new RuntimeValidationException("Only approved vouchers can be posted.");
        }

        EnsureVoucherPeriodIsOpen(voucher);
        voucher.State = AccountingVoucherState.Posted;
        voucher.PostedBy = userContext.UserName;
        voucher.PostedAt = DateTimeOffset.UtcNow;

        AddAudit("ERP", "AccountingVoucherPosted", $"Posted accounting voucher {voucher.VoucherNumber}.", userContext.UserName, userContext.UserId);
        _dbContext.SaveChanges();

        return MapAccountingVoucher(voucher);
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
                entity.AdapterMode.ToString(),
                entity.IsEnabled,
                entity.HealthStatus.ToString(),
                entity.LastCheckedAt,
                entity.LastError))
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
            AttemptCount = 0,
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
        var externalSystem = FindExternalSystem(batch.TargetSystemCode);

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

        var nextAttemptNumber = _dbContext.IntegrationJobs.Count(entity => entity.ExportBatchId == batch.Id) + 1;
        var job = new IntegrationJobEntity
        {
            Id = Guid.NewGuid(),
            JobNumber = NextIntegrationJobNumber(),
            TargetSystemCode = batch.TargetSystemCode,
            JobType = "AccountingBridgeExport",
            State = IntegrationJobState.Queued,
            ExportBatchId = batch.Id,
            AttemptNumber = nextAttemptNumber,
            PayloadSummary = $"{batch.BridgeCount} bridges / {batch.TotalAmount:0.##} CNY",
            QueuedAt = DateTimeOffset.UtcNow
        };

        _dbContext.IntegrationJobs.Add(job);
        _dbContext.SaveChanges();

        var dispatchResult = _accountingExportGatewayService.Dispatch(externalSystem, batch, bridges);
        externalSystem.LastCheckedAt = DateTimeOffset.UtcNow;
        externalSystem.LastError = dispatchResult.ErrorMessage;
        externalSystem.HealthStatus = dispatchResult.Accepted || dispatchResult.Succeeded
            ? ExternalSystemHealthStatus.Reachable
            : ExternalSystemHealthStatus.Failed;

        job.DispatchEndpoint = dispatchResult.Endpoint;
        job.ExternalJobNumber = dispatchResult.ExternalJobNumber;
        job.ExternalBatchNumber = dispatchResult.ExternalBatchNumber;
        job.RequestPayloadJson = dispatchResult.RequestPayloadJson;
        job.ResponsePayloadJson = dispatchResult.ResponsePayloadJson;
        job.ResponseCode = dispatchResult.ResponseCode;
        job.LastMessage = dispatchResult.Message;
        job.LastError = dispatchResult.ErrorMessage;
        job.LastAttemptedAt = DateTimeOffset.UtcNow;

        batch.AttemptCount = nextAttemptNumber;
        batch.ExternalBatchNumber = dispatchResult.ExternalBatchNumber ?? batch.ExternalBatchNumber;
        batch.LastMessage = dispatchResult.ErrorMessage ?? dispatchResult.Message;
        batch.CompletedAt = null;

        if (!dispatchResult.Accepted)
        {
            job.State = IntegrationJobState.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            batch.State = ExportBatchState.Failed;
            batch.SentAt = null;
            batch.CompletedAt = job.CompletedAt;

            foreach (var bridge in bridges)
            {
                bridge.State = AccountingBridgeState.Failed;
            }
        }
        else if (dispatchResult.IsFinal && dispatchResult.Succeeded)
        {
            job.State = IntegrationJobState.Succeeded;
            job.CompletedAt = DateTimeOffset.UtcNow;
            batch.State = ExportBatchState.Acknowledged;
            batch.SentAt = job.LastAttemptedAt;
            batch.CompletedAt = job.CompletedAt;

            foreach (var bridge in bridges)
            {
                bridge.State = AccountingBridgeState.Posted;
            }
        }
        else
        {
            job.State = IntegrationJobState.Sent;
            job.CompletedAt = null;
            batch.State = ExportBatchState.Sent;
            batch.SentAt = job.LastAttemptedAt;

            foreach (var bridge in bridges)
            {
                bridge.State = AccountingBridgeState.Exported;
            }
        }

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
        var job = FindIntegrationJob(integrationJobId);

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
        job.LastAttemptedAt ??= job.CompletedAt;
        job.LastMessage = outcome == IntegrationJobState.Succeeded ? "Manually completed from integration console." : request.ErrorMessage?.Trim();
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
                job.ExportBatch.LastMessage = "Manually completed from integration console.";
                foreach (var bridge in bridges)
                {
                    bridge.State = AccountingBridgeState.Posted;
                }
            }
            else
            {
                job.ExportBatch.State = ExportBatchState.Failed;
                job.ExportBatch.CompletedAt = job.CompletedAt;
                job.ExportBatch.LastMessage = request.ErrorMessage?.Trim();

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

    public IntegrationJobItemDto ReconcileIntegrationJob(string? token, Guid integrationJobId, ReconcileIntegrationJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userContext = RequirePermission(token, "integration.manage");
        var job = FindIntegrationJob(integrationJobId);
        var externalSystem = FindExternalSystem(job.TargetSystemCode);

        if (job.State != IntegrationJobState.Sent && job.State != IntegrationJobState.Queued)
        {
            throw new RuntimeValidationException("Only queued or sent integration jobs can be reconciled.");
        }

        var reconciliation = _accountingExportGatewayService.Reconcile(externalSystem, job);
        externalSystem.LastCheckedAt = DateTimeOffset.UtcNow;
        externalSystem.LastError = reconciliation.ErrorMessage;
        externalSystem.HealthStatus = reconciliation.Reachable
            ? ExternalSystemHealthStatus.Reachable
            : ExternalSystemHealthStatus.Warning;

        job.DispatchEndpoint ??= reconciliation.Endpoint;
        job.ResponseCode = reconciliation.ResponseCode ?? job.ResponseCode;
        job.ResponsePayloadJson = reconciliation.ResponsePayloadJson ?? job.ResponsePayloadJson;
        job.LastMessage = reconciliation.Message ?? request.Notes?.Trim();
        job.LastError = reconciliation.ErrorMessage;
        job.LastAttemptedAt = DateTimeOffset.UtcNow;

        if (reconciliation.IsFinal)
        {
            job.State = reconciliation.Succeeded ? IntegrationJobState.Succeeded : IntegrationJobState.Failed;
            job.CompletedAt = job.LastAttemptedAt;

            if (job.ExportBatchId.HasValue && job.ExportBatch is not null)
            {
                var bridges = _dbContext.AccountingBridgeEntries
                    .Where(entity => entity.ExportBatchId == job.ExportBatchId.Value)
                    .ToArray();

                if (reconciliation.Succeeded)
                {
                    job.ExportBatch.State = ExportBatchState.Acknowledged;
                    job.ExportBatch.CompletedAt = job.CompletedAt;
                    job.ExportBatch.LastMessage = reconciliation.Message;

                    foreach (var bridge in bridges)
                    {
                        bridge.State = AccountingBridgeState.Posted;
                    }
                }
                else
                {
                    job.ExportBatch.State = ExportBatchState.Failed;
                    job.ExportBatch.CompletedAt = job.CompletedAt;
                    job.ExportBatch.LastMessage = reconciliation.ErrorMessage ?? reconciliation.Message;

                    foreach (var bridge in bridges)
                    {
                        bridge.State = AccountingBridgeState.Failed;
                    }
                }
            }
        }

        AddAudit("Integration", "IntegrationJobReconciled", $"Reconciled integration job {job.JobNumber} against {job.TargetSystemCode}.", userContext.UserName, userContext.UserId);
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

    private static bool RolesGrantPermission(IEnumerable<RoleEntity> roles, string permission)
    {
        return roles
            .SelectMany(entity => entity.RolePermissions)
            .Any(link => string.Equals(link.Permission?.Code, permission, StringComparison.OrdinalIgnoreCase));
    }

    private UserEntity FindUser(Guid userId)
    {
        return _dbContext.Users
            .Include(entity => entity.Department)
            .Include(entity => entity.UserRoles)
                .ThenInclude(entity => entity.Role)
            .SingleOrDefault(entity => entity.Id == userId)
            ?? throw new RuntimeNotFoundException($"User '{userId}' was not found.");
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

    private SettlementEntryEntity FindSettlementEntry(Guid settlementEntryId)
    {
        return _dbContext.SettlementEntries
            .Include(entity => entity.Invoice)
            .SingleOrDefault(entity => entity.Id == settlementEntryId)
            ?? throw new RuntimeNotFoundException($"Settlement entry '{settlementEntryId}' was not found.");
    }

    private InvoiceEntity FindInvoice(Guid invoiceId)
    {
        return _dbContext.Invoices
            .Include(entity => entity.SettlementEntry)
            .SingleOrDefault(entity => entity.Id == invoiceId)
            ?? throw new RuntimeNotFoundException($"Invoice '{invoiceId}' was not found.");
    }

    private AccountingPeriodEntity FindAccountingPeriod(Guid accountingPeriodId)
    {
        return _dbContext.AccountingPeriods.SingleOrDefault(entity => entity.Id == accountingPeriodId)
            ?? throw new RuntimeNotFoundException($"Accounting period '{accountingPeriodId}' was not found.");
    }

    private AccountingVoucherEntity FindAccountingVoucher(Guid accountingVoucherId)
    {
        return _dbContext.AccountingVouchers
            .Include(entity => entity.Lines)
            .SingleOrDefault(entity => entity.Id == accountingVoucherId)
            ?? throw new RuntimeNotFoundException($"Accounting voucher '{accountingVoucherId}' was not found.");
    }

    private AccountingPeriodEntity FindAccountingPeriodByDate(DateOnly date)
    {
        EnsureAccountingPeriodsForHorizon(date);

        var periodCode = $"{date:yyyy-MM}";

        return _dbContext.AccountingPeriods.Local.SingleOrDefault(entity => entity.PeriodCode == periodCode)
            ?? _dbContext.AccountingPeriods.Single(entity => entity.PeriodCode == periodCode);
    }

    private AccountingExportBatchEntity FindAccountingExportBatch(Guid exportBatchId)
    {
        return _dbContext.AccountingExportBatches.SingleOrDefault(entity => entity.Id == exportBatchId)
            ?? throw new RuntimeNotFoundException($"Accounting export batch '{exportBatchId}' was not found.");
    }

    private IntegrationJobEntity FindIntegrationJob(Guid integrationJobId)
    {
        return _dbContext.IntegrationJobs
            .Include(entity => entity.ExportBatch)
            .SingleOrDefault(entity => entity.Id == integrationJobId)
            ?? throw new RuntimeNotFoundException($"Integration job '{integrationJobId}' was not found.");
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
            new SessionUserDto(user.Id, user.UserName, user.DisplayName, user.Department!.Name, user.MustChangePassword, roleNames, permissionCodes));
    }

    private static DepartmentListItemDto MapDepartment(DepartmentEntity entity)
    {
        return new DepartmentListItemDto(entity.Id, entity.Code, entity.Name, entity.Users.Count);
    }

    private static UserListItemDto MapUser(UserEntity entity, IReadOnlyList<string>? roleNames = null)
    {
        return new UserListItemDto(
            entity.Id,
            entity.UserName,
            entity.DisplayName,
            entity.DepartmentId,
            entity.Department!.Name,
            entity.MustChangePassword,
            entity.PasswordResetExpiresAt,
            entity.FailedAccessCount,
            entity.LockedUntil,
            entity.LastLoginAt,
            roleNames ?? entity.UserRoles.Select(link => link.Role!.Name).OrderBy(name => name).ToArray());
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
            entity.AttemptCount,
            entity.ExternalBatchNumber,
            entity.LastMessage,
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

    private static WarehouseTransferListItemDto MapWarehouseTransfer(WarehouseTransferEntity entity)
    {
        return new WarehouseTransferListItemDto(
            entity.Id,
            entity.TransferNumber,
            entity.FromWarehouse!.Code,
            entity.ToWarehouse!.Code,
            entity.ItemCode,
            entity.ItemName,
            entity.Quantity,
            entity.BatchNumber,
            entity.QualityStatus.ToString(),
            entity.State.ToString(),
            entity.UnitCost,
            entity.TotalCost,
            entity.TransferredAt);
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
            entity.AttemptNumber,
            entity.ExportBatch?.BatchNumber,
            entity.ExternalJobNumber,
            entity.ExternalBatchNumber,
            entity.PayloadSummary,
            entity.ResponseCode,
            entity.LastMessage,
            entity.QueuedAt,
            entity.LastAttemptedAt,
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

    private static InvoiceListItemDto MapInvoice(InvoiceEntity entity)
    {
        return new InvoiceListItemDto(
            entity.Id,
            entity.InvoiceNumber,
            entity.InvoiceType.ToString(),
            entity.SourceDocumentType,
            entity.SourceDocumentNumber,
            entity.CounterpartyName,
            entity.SettlementEntry?.EntryNumber ?? string.Empty,
            entity.Amount,
            entity.IssuedOn,
            entity.DueDate,
            entity.State.ToString(),
            entity.CreatedAt);
    }

    private static SettlementEntryDto MapSettlement(SettlementEntryEntity entity)
    {
        var outstandingAmount = decimal.Round(entity.TotalAmount - entity.SettledAmount, 2);

        return new SettlementEntryDto(
            entity.Id,
            entity.EntryNumber,
            entity.SettlementType.ToString(),
            entity.SourceDocumentType,
            entity.SourceDocumentNumber,
            entity.CounterpartyName,
            entity.TotalAmount,
            entity.SettledAmount,
            outstandingAmount,
            entity.DueDate,
            entity.State.ToString(),
            entity.Invoice?.InvoiceNumber,
            entity.Invoice?.State.ToString(),
            entity.CreatedAt,
            entity.LastSettledAt);
    }

    private static SettlementApplicationDto MapSettlementApplication(SettlementApplicationEntity entity)
    {
        return new SettlementApplicationDto(
            entity.Id,
            entity.ApplicationNumber,
            entity.SettlementEntry?.EntryNumber ?? string.Empty,
            entity.SettlementEntry?.CounterpartyName ?? string.Empty,
            entity.Invoice?.InvoiceNumber,
            entity.Amount,
            entity.Reference,
            entity.AppliedBy,
            entity.AppliedAt);
    }

    private static AccountingPeriodDto MapAccountingPeriod(AccountingPeriodEntity entity)
    {
        return new AccountingPeriodDto(
            entity.Id,
            entity.PeriodCode,
            entity.StartDate,
            entity.EndDate,
            entity.State.ToString(),
            entity.ClosedBy,
            entity.ClosedAt,
            entity.CloseNotes);
    }

    private static AccountingVoucherDto MapAccountingVoucher(AccountingVoucherEntity entity)
    {
        return new AccountingVoucherDto(
            entity.Id,
            entity.VoucherNumber,
            entity.PeriodCode,
            entity.State.ToString(),
            entity.TotalDebitAmount,
            entity.TotalCreditAmount,
            entity.LineCount,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.ApprovedBy,
            entity.ApprovedAt,
            entity.PostedBy,
            entity.PostedAt,
            entity.Notes,
            entity.Lines
                .OrderBy(line => line.LineNumber)
                .Select(MapAccountingVoucherLine)
                .ToArray());
    }

    private static AccountingVoucherLineDto MapAccountingVoucherLine(AccountingVoucherLineEntity entity)
    {
        return new AccountingVoucherLineDto(
            entity.Id,
            entity.LineNumber,
            entity.SourceBridgeNumber,
            entity.SourceDocumentNumber,
            entity.SourceEvent,
            entity.AccountCode,
            entity.DebitAmount,
            entity.CreditAmount,
            entity.Narrative);
    }

    private static ReportExportJobDto MapReportExportJob(ReportExportJobEntity entity)
    {
        return new ReportExportJobDto(
            entity.Id,
            entity.JobNumber,
            entity.ReportCode,
            entity.ReportName,
            entity.Format,
            entity.State,
            entity.RowCount,
            entity.RequestedBy,
            entity.RequestedAt,
            entity.CompletedAt,
            entity.FileName,
            entity.Content,
            entity.ErrorMessage);
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

    private string ResolveInventoryItemName(string itemCode)
    {
        var materialName = _dbContext.Materials
            .AsNoTracking()
            .Where(entity => entity.Code.ToLower() == itemCode.ToLower())
            .Select(entity => entity.Name)
            .SingleOrDefault();

        if (!string.IsNullOrWhiteSpace(materialName))
        {
            return materialName;
        }

        var productName = _dbContext.Products
            .AsNoTracking()
            .Where(entity => entity.Code.ToLower() == itemCode.ToLower())
            .Select(entity => entity.Name)
            .SingleOrDefault();

        if (!string.IsNullOrWhiteSpace(productName))
        {
            return productName;
        }

        throw new RuntimeValidationException($"Item '{itemCode}' was not found in product or material master.");
    }

    private AvailableInventoryLot SelectTransferLot(Guid warehouseId, string itemCode, string? batchNumber, decimal requiredQuantity)
    {
        var lots = _dbContext.InventoryTransactions
            .AsNoTracking()
            .Where(entity => entity.WarehouseId == warehouseId && entity.ItemCode.ToLower() == itemCode.ToLower())
            .AsEnumerable()
            .GroupBy(entity => new { BatchNumber = entity.BatchNumber ?? "UNBATCHED", entity.QualityStatus })
            .Select(group => new AvailableInventoryLot(
                group.Key.BatchNumber,
                group.Key.QualityStatus,
                group.Sum(entity => entity.Quantity),
                decimal.Round(group.Where(entity => entity.Quantity > 0).Select(entity => entity.UnitCost).DefaultIfEmpty(0m).Average(), 2)))
            .Where(lot => lot.Quantity > 0)
            .OrderByDescending(lot => lot.Quantity)
            .ToArray();

        var matchingLots = string.IsNullOrWhiteSpace(batchNumber)
            ? lots
            : lots.Where(lot => string.Equals(lot.BatchNumber, batchNumber.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

        var selectedLot = matchingLots.FirstOrDefault(lot => lot.Quantity >= requiredQuantity);
        if (selectedLot is null)
        {
            throw new RuntimeValidationException("No source batch has enough available quantity for this warehouse transfer.");
        }

        return selectedLot;
    }

    private SettlementEntryEntity CreateSettlementEntry(
        SettlementType settlementType,
        string sourceDocumentType,
        string sourceDocumentNumber,
        string counterpartyName,
        decimal totalAmount,
        DateOnly dueDate)
    {
        return new SettlementEntryEntity
        {
            Id = Guid.NewGuid(),
            EntryNumber = NextSettlementEntryNumber(),
            SettlementType = settlementType,
            State = SettlementState.Open,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentNumber = sourceDocumentNumber,
            CounterpartyName = counterpartyName,
            TotalAmount = decimal.Round(totalAmount, 2),
            SettledAmount = 0m,
            DueDate = dueDate,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private IReadOnlyList<SettlementAgingBucketDto> GetSettlementAgingBuckets()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return _dbContext.SettlementEntries
            .AsNoTracking()
            .ToArray()
            .Where(entity => entity.State is SettlementState.Open or SettlementState.PartiallySettled)
            .GroupBy(entity => entity.SettlementType)
            .OrderBy(group => group.Key.ToString())
            .Select(group =>
            {
                decimal currentAmount = 0m;
                decimal days1To30Amount = 0m;
                decimal days31To60Amount = 0m;
                decimal days61To90Amount = 0m;
                decimal over90DaysAmount = 0m;

                foreach (var settlement in group)
                {
                    var outstandingAmount = decimal.Round(settlement.TotalAmount - settlement.SettledAmount, 2);
                    var daysPastDue = today.DayNumber - settlement.DueDate.DayNumber;

                    if (daysPastDue <= 0)
                    {
                        currentAmount += outstandingAmount;
                    }
                    else if (daysPastDue <= 30)
                    {
                        days1To30Amount += outstandingAmount;
                    }
                    else if (daysPastDue <= 60)
                    {
                        days31To60Amount += outstandingAmount;
                    }
                    else if (daysPastDue <= 90)
                    {
                        days61To90Amount += outstandingAmount;
                    }
                    else
                    {
                        over90DaysAmount += outstandingAmount;
                    }
                }

                var totalOutstanding = currentAmount + days1To30Amount + days31To60Amount + days61To90Amount + over90DaysAmount;

                return new SettlementAgingBucketDto(
                    group.Key.ToString(),
                    group.Count(),
                    decimal.Round(currentAmount, 2),
                    decimal.Round(days1To30Amount, 2),
                    decimal.Round(days31To60Amount, 2),
                    decimal.Round(days61To90Amount, 2),
                    decimal.Round(over90DaysAmount, 2),
                    decimal.Round(totalOutstanding, 2));
            })
            .ToArray();
    }

    private void EnsureVoucherPeriodIsOpen(AccountingVoucherEntity voucher)
    {
        var period = _dbContext.AccountingPeriods.SingleOrDefault(entity => entity.PeriodCode == voucher.PeriodCode)
            ?? throw new RuntimeValidationException($"Accounting period {voucher.PeriodCode} was not found.");

        if (period.State == AccountingPeriodState.Closed)
        {
            throw new RuntimeValidationException($"Accounting period {period.PeriodCode} is closed.");
        }
    }

    private static IReadOnlyList<AccountingVoucherLineDraft> BuildAccountingVoucherLines(AccountingBridgeEntryEntity bridge)
    {
        var amount = decimal.Round(bridge.Amount, 2);
        var (debitAccount, creditAccount) = ResolveVoucherAccounts(bridge.PostingAccount);
        var sourceNarrative = $"{bridge.SourceEvent} | {bridge.SourceDocumentNumber}";

        return
        [
            new AccountingVoucherLineDraft(debitAccount, amount, 0m, sourceNarrative),
            new AccountingVoucherLineDraft(creditAccount, 0m, amount, sourceNarrative)
        ];
    }

    private static (string DebitAccount, string CreditAccount) ResolveVoucherAccounts(string postingAccount)
    {
        var account = postingAccount.Trim().ToUpperInvariant();

        return account switch
        {
            "AP-GRNI" => ("RAW-MATERIAL-INVENTORY", "AP-GRNI"),
            "AP-INVOICE" => ("AP-GRNI-CLEARING", "AP-INVOICE"),
            "AP-SETTLEMENT" => ("AP-SETTLEMENT", "BANK-CASH"),
            "AR-SHIP" => ("AR-SHIP", "SALES-REVENUE"),
            "AR-INVOICE" => ("AR-INVOICE", "AR-SHIP-CLEARING"),
            "AR-SETTLEMENT" => ("BANK-CASH", "AR-SETTLEMENT"),
            "COGS" => ("COGS", "FINISHED-GOODS-INVENTORY"),
            "WIP-MFG" => ("WIP-MFG", "MATERIAL-LABOR-OH-CLEARING"),
            "FG-READY" => ("FINISHED-GOODS-PENDING-QA", "WIP-MFG"),
            "SCRAP-LOSS" => ("SCRAP-LOSS", "WIP-MFG"),
            _ when account.StartsWith("AR", StringComparison.OrdinalIgnoreCase) => (account, "FINANCE-CLEARING"),
            _ when account.StartsWith("AP", StringComparison.OrdinalIgnoreCase) => ("FINANCE-CLEARING", account),
            _ => (account, "FINANCE-CLEARING")
        };
    }

    private void EnsureAccountingPeriodsForHorizon(DateOnly anchorDate)
    {
        var requiredPeriodCodes = new[]
        {
            anchorDate.AddMonths(-1),
            anchorDate,
            anchorDate.AddMonths(1),
            anchorDate.AddMonths(2)
        }
        .Select(value => $"{value:yyyy-MM}")
        .ToArray();

        var existingPeriodCodes = _dbContext.AccountingPeriods
            .AsNoTracking()
            .Select(entity => entity.PeriodCode)
            .AsEnumerable()
            .Concat(_dbContext.AccountingPeriods.Local.Select(entity => entity.PeriodCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var periodsAdded = false;

        foreach (var periodCode in requiredPeriodCodes)
        {
            if (existingPeriodCodes.Contains(periodCode))
            {
                continue;
            }

            var year = int.Parse(periodCode[..4], CultureInfo.InvariantCulture);
            var month = int.Parse(periodCode[5..], CultureInfo.InvariantCulture);
            var startDate = new DateOnly(year, month, 1);

            _dbContext.AccountingPeriods.Add(new AccountingPeriodEntity
            {
                Id = Guid.NewGuid(),
                PeriodCode = periodCode,
                State = AccountingPeriodState.Open,
                StartDate = startDate,
                EndDate = startDate.AddMonths(1).AddDays(-1)
            });

            existingPeriodCodes.Add(periodCode);
            periodsAdded = true;
        }

        if (periodsAdded)
        {
            _dbContext.SaveChanges();
        }
    }

    private static DateOnly CalculateDueDate(string? paymentTerms, int fallbackDays)
    {
        var days = fallbackDays;

        if (!string.IsNullOrWhiteSpace(paymentTerms))
        {
            var digits = new string(paymentTerms.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var parsedDays) && parsedDays > 0)
            {
                days = parsedDays;
            }
        }

        return DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days);
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

    private string NextWarehouseTransferNumber()
    {
        return NextNumber(
            "TR",
            _dbContext.WarehouseTransfers.AsNoTracking().Select(entity => entity.TransferNumber).AsEnumerable(),
            _dbContext.WarehouseTransfers.Local.Select(entity => entity.TransferNumber));
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

    private string NextReportExportJobNumber()
    {
        return NextNumber(
            "RJ",
            _dbContext.ReportExportJobs.AsNoTracking().Select(entity => entity.JobNumber).AsEnumerable(),
            _dbContext.ReportExportJobs.Local.Select(entity => entity.JobNumber));
    }

    private string NextSettlementEntryNumber()
    {
        return NextNumber(
            "SE",
            _dbContext.SettlementEntries.AsNoTracking().Select(entity => entity.EntryNumber).AsEnumerable(),
            _dbContext.SettlementEntries.Local.Select(entity => entity.EntryNumber));
    }

    private string NextInvoiceNumber()
    {
        return NextNumber(
            "INV",
            _dbContext.Invoices.AsNoTracking().Select(entity => entity.InvoiceNumber).AsEnumerable(),
            _dbContext.Invoices.Local.Select(entity => entity.InvoiceNumber));
    }

    private string NextSettlementApplicationNumber()
    {
        return NextNumber(
            "SA",
            _dbContext.SettlementApplications.AsNoTracking().Select(entity => entity.ApplicationNumber).AsEnumerable(),
            _dbContext.SettlementApplications.Local.Select(entity => entity.ApplicationNumber));
    }

    private string NextAccountingVoucherNumber()
    {
        return NextNumber(
            "JV",
            _dbContext.AccountingVouchers.AsNoTracking().Select(entity => entity.VoucherNumber).AsEnumerable(),
            _dbContext.AccountingVouchers.Local.Select(entity => entity.VoucherNumber));
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

    private sealed record AvailableInventoryLot(
        string BatchNumber,
        InventoryQualityStatus QualityStatus,
        decimal Quantity,
        decimal UnitCost);

    private sealed record AccountingVoucherLineDraft(
        string AccountCode,
        decimal DebitAmount,
        decimal CreditAmount,
        string Narrative);

    private sealed record SessionContext(SessionEntity Session, UserEntity User, IReadOnlyList<string> RoleNames, HashSet<string> PermissionCodes)
    {
        public Guid UserId => User.Id;
        public string UserName => User.UserName;
    }

    private sealed record ReportDataset(
        string ReportCode,
        string Name,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows);
}
