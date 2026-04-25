using ManufacturingPlatform.Modules.CRM.Domain;
using ManufacturingPlatform.Modules.ERP.Domain;
using ManufacturingPlatform.Modules.Integration.Domain;
using ManufacturingPlatform.Modules.MES.Domain;
using ManufacturingPlatform.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;
using ManufacturingPlatform.Host.Security;

namespace ManufacturingPlatform.Host.Persistence;

public static class ManufacturingDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManufacturingDbContext>();

        var hasMigrations = dbContext.Database.GetMigrations().Any();

        if (hasMigrations)
        {
            var hasMigrationHistory = await HasTableAsync(dbContext, "__EFMigrationsHistory", cancellationToken);
            var hasApplicationTables = await HasTableAsync(dbContext, "Users", cancellationToken);

            if (hasMigrationHistory || !hasApplicationTables)
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            await EnsureUserSecurityAsync(dbContext, cancellationToken);
            return;
        }

        var salesDepartment = new DepartmentEntity { Id = Id("DEPT-SALES"), Code = "SALES", Name = "Sales" };
        var planningDepartment = new DepartmentEntity { Id = Id("DEPT-PLANNING"), Code = "PLN", Name = "Production Planning" };
        var productionDepartment = new DepartmentEntity { Id = Id("DEPT-PROD"), Code = "PROD", Name = "Production" };
        var qualityDepartment = new DepartmentEntity { Id = Id("DEPT-QA"), Code = "QA", Name = "Quality" };
        var financeDepartment = new DepartmentEntity { Id = Id("DEPT-FIN"), Code = "FIN", Name = "Finance" };
        var itDepartment = new DepartmentEntity { Id = Id("DEPT-IT"), Code = "IT", Name = "IT" };

        dbContext.Departments.AddRange(salesDepartment, planningDepartment, productionDepartment, qualityDepartment, financeDepartment, itDepartment);

        var permissions = CreatePermissions();
        dbContext.Permissions.AddRange(permissions.Values);

        var adminRole = new RoleEntity { Id = Id("ROLE-ADMIN"), Code = "ADMIN", Name = "System Administrator" };
        var salesRole = new RoleEntity { Id = Id("ROLE-SALES"), Code = "SALES", Name = "Sales Manager" };
        var plannerRole = new RoleEntity { Id = Id("ROLE-PLANNER"), Code = "PLANNER", Name = "Production Planner" };
        var operatorRole = new RoleEntity { Id = Id("ROLE-OPERATOR"), Code = "OPERATOR", Name = "Workshop Operator" };
        var qualityRole = new RoleEntity { Id = Id("ROLE-QUALITY"), Code = "QUALITY", Name = "Quality Engineer" };
        var financeRole = new RoleEntity { Id = Id("ROLE-FINANCE"), Code = "FINANCE", Name = "Finance Controller" };

        dbContext.Roles.AddRange(adminRole, salesRole, plannerRole, operatorRole, qualityRole, financeRole);

        LinkRolePermissions(dbContext, adminRole, permissions.Values);
        LinkRolePermissions(
            dbContext,
            salesRole,
            permissions["crm.customer.read"],
            permissions["crm.customer.write"],
            permissions["crm.sales-order.read"],
            permissions["crm.sales-order.write"],
            permissions["crm.sales-order.approve"],
            permissions["crm.sales-order.release"],
            permissions["erp.product.read"],
            permissions["erp.shipment.read"],
            permissions["reporting.dashboard.read"],
            permissions["ai.summary.read"]);
        LinkRolePermissions(
            dbContext,
            plannerRole,
            permissions["crm.sales-order.read"],
            permissions["erp.supplier.read"],
            permissions["erp.purchase-order.read"],
            permissions["erp.purchase-order.write"],
            permissions["erp.purchase-order.approve"],
            permissions["erp.goods-receipt.write"],
            permissions["erp.product.read"],
            permissions["erp.production-order.read"],
            permissions["erp.production-order.release"],
            permissions["erp.inventory.read"],
            permissions["erp.transfer.write"],
            permissions["erp.cost.read"],
            permissions["erp.shipment.read"],
            permissions["erp.shipment.write"],
            permissions["erp.invoice.read"],
            permissions["erp.settlement.read"],
            permissions["erp.accounting.manage"],
            permissions["integration.read"],
            permissions["integration.manage"],
            permissions["reporting.dashboard.read"],
            permissions["ai.summary.read"]);
        LinkRolePermissions(
            dbContext,
            operatorRole,
            permissions["mes.work-order.read"],
            permissions["mes.work-order.execute"],
            permissions["reporting.dashboard.read"]);
        LinkRolePermissions(
            dbContext,
            qualityRole,
            permissions["quality.inspection.read"],
            permissions["quality.inspection.write"],
            permissions["erp.inventory.read"],
            permissions["reporting.dashboard.read"],
            permissions["ai.summary.read"]);
        LinkRolePermissions(
            dbContext,
            financeRole,
            permissions["erp.inventory.read"],
            permissions["erp.cost.read"],
            permissions["erp.invoice.read"],
            permissions["erp.invoice.write"],
            permissions["erp.settlement.read"],
            permissions["erp.settlement.write"],
            permissions["erp.period.close"],
            permissions["erp.accounting.manage"],
            permissions["integration.read"],
            permissions["integration.manage"],
            permissions["reporting.dashboard.read"],
            permissions["ai.summary.read"]);

        var users = new[]
        {
            CreateUser("admin", "Platform Admin", itDepartment, adminRole),
            CreateUser("sales", "Sales Director", salesDepartment, salesRole),
            CreateUser("planner", "Planning Manager", planningDepartment, plannerRole),
            CreateUser("operator", "Workshop Operator", productionDepartment, operatorRole),
            CreateUser("quality", "Quality Engineer", qualityDepartment, qualityRole),
            CreateUser("finance", "Finance Controller", financeDepartment, financeRole)
        };

        dbContext.Users.AddRange(users.Select(user => user.User));
        dbContext.UserRoles.AddRange(users.Select(user => new UserRoleEntity { UserId = user.User.Id, RoleId = user.Role.Id }));

        var rmWarehouse = new WarehouseEntity { Id = Id("WH-RM"), Code = "RM-01", Name = "Raw Material Warehouse", IsQualityControlled = true, SupportsWip = false };
        var fgWarehouse = new WarehouseEntity { Id = Id("WH-FG"), Code = "FG-01", Name = "Finished Goods Warehouse", IsQualityControlled = true, SupportsWip = false };
        var wipWarehouse = new WarehouseEntity { Id = Id("WH-WIP"), Code = "WIP-01", Name = "WIP Buffer Warehouse", IsQualityControlled = false, SupportsWip = true };
        var quarantineWarehouse = new WarehouseEntity { Id = Id("WH-QA"), Code = "QA-01", Name = "Quality Hold Warehouse", IsQualityControlled = true, SupportsWip = false };
        dbContext.Warehouses.AddRange(rmWarehouse, fgWarehouse, wipWarehouse, quarantineWarehouse);

        var cell = new MaterialEntity { Id = Id("MAT-CELL"), Code = "CELL-182N", Name = "182N Solar Cell", BaseUnitCode = "PCS", StandardUnitCost = 1.85m, ReorderLevel = 2000m, IsLotControlled = true };
        var glass = new MaterialEntity { Id = Id("MAT-GLASS"), Code = "GLASS-2MM", Name = "2mm PV Glass", BaseUnitCode = "PCS", StandardUnitCost = 24.50m, ReorderLevel = 200m, IsLotControlled = true };
        var eva = new MaterialEntity { Id = Id("MAT-EVA"), Code = "EVA-FOIL", Name = "EVA Film", BaseUnitCode = "M", StandardUnitCost = 4.20m, ReorderLevel = 500m, IsLotControlled = true };
        dbContext.Materials.AddRange(cell, glass, eva);

        var panel580 = new ProductEntity
        {
            Id = Id("PROD-580"),
            Code = "PANEL-580W",
            Name = "182N Mono 580W",
            ProductFamily = "Solar Panel",
            BaseUnitCode = "PCS",
            IsLotControlled = true,
            StandardLaborHoursPerUnit = 0.08m,
            StandardMachineHoursPerUnit = 0.06m,
            OverheadRatePerUnit = 14m
        };

        var panel610 = new ProductEntity
        {
            Id = Id("PROD-610"),
            Code = "PANEL-610W",
            Name = "210R Mono 610W",
            ProductFamily = "Solar Panel",
            BaseUnitCode = "PCS",
            IsLotControlled = true,
            StandardLaborHoursPerUnit = 0.09m,
            StandardMachineHoursPerUnit = 0.07m,
            OverheadRatePerUnit = 16m
        };

        dbContext.Products.AddRange(panel580, panel610);
        dbContext.BomComponents.AddRange(
            new BomComponentEntity { Id = Id("BOM-580-1"), ProductId = panel580.Id, MaterialId = cell.Id, QuantityPerUnit = 120m },
            new BomComponentEntity { Id = Id("BOM-580-2"), ProductId = panel580.Id, MaterialId = glass.Id, QuantityPerUnit = 2m },
            new BomComponentEntity { Id = Id("BOM-580-3"), ProductId = panel580.Id, MaterialId = eva.Id, QuantityPerUnit = 8m },
            new BomComponentEntity { Id = Id("BOM-610-1"), ProductId = panel610.Id, MaterialId = cell.Id, QuantityPerUnit = 132m },
            new BomComponentEntity { Id = Id("BOM-610-2"), ProductId = panel610.Id, MaterialId = glass.Id, QuantityPerUnit = 2m },
            new BomComponentEntity { Id = Id("BOM-610-3"), ProductId = panel610.Id, MaterialId = eva.Id, QuantityPerUnit = 9m });

        var supplierA = new SupplierEntity
        {
            Id = Id("SUP-001"),
            Code = "S-0001",
            Name = "Shandong Materials Group",
            Category = "Core Material",
            TaxRegistrationNumber = "91370100SMG000001",
            LeadTimeDays = 5,
            PaymentTerms = "30D"
        };

        var supplierB = new SupplierEntity
        {
            Id = Id("SUP-002"),
            Code = "S-0002",
            Name = "Qingdao Glass Supply Co.",
            Category = "Glass",
            TaxRegistrationNumber = "91370200QGS000002",
            LeadTimeDays = 7,
            PaymentTerms = "45D"
        };

        dbContext.Suppliers.AddRange(supplierA, supplierB);

        var purchaseOrder1 = new PurchaseOrderEntity
        {
            Id = Id("PO-001"),
            OrderNumber = "PO-0001",
            SupplierId = supplierA.Id,
            MaterialId = eva.Id,
            WarehouseId = rmWarehouse.Id,
            State = PurchaseOrderState.Completed,
            OrderedQuantity = 1200m,
            ReceivedQuantity = 1200m,
            UnitPrice = 4.35m,
            TotalAmount = 5220m,
            OrderedOn = new DateOnly(2026, 4, 15),
            ExpectedReceiptDate = new DateOnly(2026, 4, 20)
        };

        var purchaseOrder2 = new PurchaseOrderEntity
        {
            Id = Id("PO-002"),
            OrderNumber = "PO-0002",
            SupplierId = supplierB.Id,
            MaterialId = glass.Id,
            WarehouseId = rmWarehouse.Id,
            State = PurchaseOrderState.Approved,
            OrderedQuantity = 180m,
            ReceivedQuantity = 0m,
            UnitPrice = 25.10m,
            TotalAmount = 4518m,
            OrderedOn = new DateOnly(2026, 4, 21),
            ExpectedReceiptDate = new DateOnly(2026, 4, 27)
        };

        dbContext.PurchaseOrders.AddRange(purchaseOrder1, purchaseOrder2);

        dbContext.InventoryTransactions.AddRange(
            OpeningReceipt(rmWarehouse, cell.Code, 24000m, cell.StandardUnitCost, "LOT-CELL-OPEN", "OPENING"),
            OpeningReceipt(rmWarehouse, glass.Code, 600m, glass.StandardUnitCost, "LOT-GLASS-OPEN", "OPENING"),
            OpeningReceipt(rmWarehouse, eva.Code, 4000m, eva.StandardUnitCost, "LOT-EVA-OPEN", "OPENING"),
            new InventoryTransactionEntity
            {
                Id = Id("ITX-PO-001"),
                TransactionNumber = "ITX-PO-0001",
                TransactionType = InventoryTransactionType.GoodsReceipt,
                WarehouseId = rmWarehouse.Id,
                ItemCode = eva.Code,
                Quantity = 1200m,
                BatchNumber = "LOT-EVA-20260420",
                UnitCost = 4.35m,
                TotalCost = 5220m,
                QualityStatus = InventoryQualityStatus.Released,
                SourceDocumentNumber = purchaseOrder1.OrderNumber,
                SourceEvent = "Purchase receipt",
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-2)
            });

        dbContext.GoodsReceipts.Add(
            new GoodsReceiptEntity
            {
                Id = Id("GR-001"),
                ReceiptNumber = "GR-0001",
                PurchaseOrderId = purchaseOrder1.Id,
                WarehouseId = rmWarehouse.Id,
                State = GoodsReceiptState.Accepted,
                QualityStatus = InventoryQualityStatus.Released,
                ReceivedQuantity = 1200m,
                UnitCost = 4.35m,
                BatchNumber = "LOT-EVA-20260420",
                ReceivedOn = new DateOnly(2026, 4, 20)
            });

        dbContext.SettlementEntries.Add(
            new SettlementEntryEntity
            {
                Id = Id("SETTLEMENT-001"),
                EntryNumber = "SE-0001",
                SettlementType = SettlementType.Payable,
                State = SettlementState.Open,
                SourceDocumentType = "GoodsReceipt",
                SourceDocumentNumber = "GR-0001",
                CounterpartyName = supplierA.Name,
                TotalAmount = 5220m,
                SettledAmount = 0m,
                DueDate = new DateOnly(2026, 5, 20),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            });

        dbContext.AccountingPeriods.AddRange(
            new AccountingPeriodEntity
            {
                Id = Id("PERIOD-2026-03"),
                PeriodCode = "2026-03",
                State = AccountingPeriodState.Closed,
                StartDate = new DateOnly(2026, 3, 1),
                EndDate = new DateOnly(2026, 3, 31),
                ClosedBy = "system",
                ClosedAt = DateTimeOffset.UtcNow.AddDays(-20),
                CloseNotes = "Seeded historical closed period."
            },
            new AccountingPeriodEntity
            {
                Id = Id("PERIOD-2026-04"),
                PeriodCode = "2026-04",
                State = AccountingPeriodState.Open,
                StartDate = new DateOnly(2026, 4, 1),
                EndDate = new DateOnly(2026, 4, 30)
            },
            new AccountingPeriodEntity
            {
                Id = Id("PERIOD-2026-05"),
                PeriodCode = "2026-05",
                State = AccountingPeriodState.Open,
                StartDate = new DateOnly(2026, 5, 1),
                EndDate = new DateOnly(2026, 5, 31)
            },
            new AccountingPeriodEntity
            {
                Id = Id("PERIOD-2026-06"),
                PeriodCode = "2026-06",
                State = AccountingPeriodState.Open,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30)
            });

        dbContext.ExternalSystems.Add(
            new ExternalSystemEntity
            {
                Id = Id("EXT-KINGDEE"),
                SystemCode = "KINGDEE",
                Name = "Kingdee Finance",
                BaseUrl = "mock://kingdee-finance",
                AdapterMode = ExternalSystemAdapterMode.Mock,
                AccountingExportPath = "/accounting-export-batches",
                JobStatusPathTemplate = "/jobs/{externalJobNumber}",
                IsEnabled = true,
                HealthStatus = ExternalSystemHealthStatus.Configured,
                LastCheckedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
            });

        var customerA = new CustomerEntity
        {
            Id = Id("CUST-JN"),
            Code = "C-0001",
            Name = "Jinan New Energy Co.",
            Status = CustomerStatus.Active,
            Segment = CustomerSegment.KeyAccount,
            Region = "Shandong",
            Industry = "Solar EPC",
            CreditRating = "A"
        };

        var customerB = new CustomerEntity
        {
            Id = Id("CUST-QD"),
            Code = "C-0002",
            Name = "Qingdao Power Integration Ltd.",
            Status = CustomerStatus.Active,
            Segment = CustomerSegment.Strategic,
            Region = "Shandong",
            Industry = "Industrial Power",
            CreditRating = "A+"
        };

        dbContext.Customers.AddRange(customerA, customerB);

        var salesOrder1 = new SalesOrderEntity
        {
            Id = Id("SO-001"),
            OrderNumber = "SO-0001",
            CustomerId = customerA.Id,
            ProductCode = panel580.Code,
            Quantity = 120m,
            UnitPrice = 780m,
            TotalAmount = 93600m,
            State = SalesOrderState.Released,
            OrderDate = new DateOnly(2026, 4, 18),
            RequestedDeliveryDate = new DateOnly(2026, 4, 30)
        };

        var salesOrder2 = new SalesOrderEntity
        {
            Id = Id("SO-002"),
            OrderNumber = "SO-0002",
            CustomerId = customerB.Id,
            ProductCode = panel610.Code,
            Quantity = 78m,
            UnitPrice = 820m,
            TotalAmount = 63960m,
            State = SalesOrderState.Released,
            OrderDate = new DateOnly(2026, 4, 19),
            RequestedDeliveryDate = new DateOnly(2026, 4, 29)
        };

        var salesOrder3 = new SalesOrderEntity
        {
            Id = Id("SO-003"),
            OrderNumber = "SO-0003",
            CustomerId = customerB.Id,
            ProductCode = panel580.Code,
            Quantity = 30m,
            UnitPrice = 840m,
            TotalAmount = 25200m,
            State = SalesOrderState.Draft,
            OrderDate = new DateOnly(2026, 4, 22),
            RequestedDeliveryDate = new DateOnly(2026, 5, 5)
        };

        dbContext.SalesOrders.AddRange(salesOrder1, salesOrder2, salesOrder3);

        var productionOrder1 = new ProductionOrderEntity
        {
            Id = Id("MO-001"),
            ProductionOrderNumber = "MO-0001",
            SalesOrderId = salesOrder1.Id,
            State = ProductionOrderState.Approved,
            ProductCode = panel580.Code,
            PlannedQuantity = 120m,
            PlannedStartDate = new DateOnly(2026, 4, 23),
            PlannedEndDate = new DateOnly(2026, 4, 27)
        };

        var productionOrder2 = new ProductionOrderEntity
        {
            Id = Id("MO-002"),
            ProductionOrderNumber = "MO-0002",
            SalesOrderId = salesOrder2.Id,
            State = ProductionOrderState.TechnicallyCompleted,
            ProductCode = panel610.Code,
            PlannedQuantity = 78m,
            PlannedStartDate = new DateOnly(2026, 4, 22),
            PlannedEndDate = new DateOnly(2026, 4, 26)
        };

        dbContext.ProductionOrders.AddRange(productionOrder1, productionOrder2);

        salesOrder1.ProductionOrderId = productionOrder1.Id;
        salesOrder2.ProductionOrderId = productionOrder2.Id;

        var workOrder2 = new WorkOrderEntity
        {
            Id = Id("WO-001"),
            WorkOrderNumber = "WO-0001",
            ProductionOrderId = productionOrder2.Id,
            State = WorkOrderState.Reported,
            ProductCode = panel610.Code,
            PlannedQuantity = 78m,
            GoodQuantity = 36m,
            ScrapQuantity = 2m,
            ReworkQuantity = 0m,
            LaborHours = 12.5m,
            MachineHours = 10m,
            CurrentOperationName = "Quality Inspection",
            CurrentOperationSequence = 99,
            IssueMode = IssueMode.Backflush,
            IsMachineDriven = true,
            FinishedGoodsBatchNumber = "FG-20260422-610-01"
        };

        dbContext.WorkOrders.Add(workOrder2);
        productionOrder2.WorkOrderId = workOrder2.Id;

        dbContext.WorkOrderOperations.AddRange(
            new WorkOrderOperationEntity
            {
                Id = Id("WO-001-OP10"),
                WorkOrderId = workOrder2.Id,
                OperationCode = "CUT",
                OperationName = "Cell Stringing",
                Sequence = 10,
                State = OperationExecutionState.Completed,
                IsMachineDriven = true,
                IssueMode = IssueMode.Backflush,
                WorkCenterCode = "WC-CELL",
                MachineCode = "MC-STRINGER"
            },
            new WorkOrderOperationEntity
            {
                Id = Id("WO-001-OP20"),
                WorkOrderId = workOrder2.Id,
                OperationCode = "LAM",
                OperationName = "Lamination",
                Sequence = 20,
                State = OperationExecutionState.Completed,
                IsMachineDriven = true,
                IssueMode = IssueMode.Backflush,
                WorkCenterCode = "WC-LAM",
                MachineCode = "MC-LAM-01"
            },
            new WorkOrderOperationEntity
            {
                Id = Id("WO-001-OP30"),
                WorkOrderId = workOrder2.Id,
                OperationCode = "FQC",
                OperationName = "Final Test",
                Sequence = 30,
                State = OperationExecutionState.Completed,
                IsMachineDriven = false,
                IssueMode = IssueMode.ManualIssue,
                WorkCenterCode = "WC-FQC"
            });

        dbContext.InventoryTransactions.AddRange(
            new InventoryTransactionEntity
            {
                Id = Id("ITX-001"),
                TransactionNumber = "ITX-0001",
                TransactionType = InventoryTransactionType.ProductionIssue,
                WarehouseId = rmWarehouse.Id,
                ItemCode = cell.Code,
                Quantity = -5148m,
                BatchNumber = "LOT-CELL-OPEN",
                UnitCost = cell.StandardUnitCost,
                TotalCost = -9523.80m,
                QualityStatus = InventoryQualityStatus.Released,
                SourceDocumentNumber = workOrder2.WorkOrderNumber,
                SourceEvent = "MES backflush",
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-1)
            },
            new InventoryTransactionEntity
            {
                Id = Id("ITX-002"),
                TransactionNumber = "ITX-0002",
                TransactionType = InventoryTransactionType.ProductionIssue,
                WarehouseId = rmWarehouse.Id,
                ItemCode = glass.Code,
                Quantity = -78m,
                BatchNumber = "LOT-GLASS-OPEN",
                UnitCost = glass.StandardUnitCost,
                TotalCost = -1911m,
                QualityStatus = InventoryQualityStatus.Released,
                SourceDocumentNumber = workOrder2.WorkOrderNumber,
                SourceEvent = "MES backflush",
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-1)
            },
            new InventoryTransactionEntity
            {
                Id = Id("ITX-003"),
                TransactionNumber = "ITX-0003",
                TransactionType = InventoryTransactionType.ProductionIssue,
                WarehouseId = rmWarehouse.Id,
                ItemCode = eva.Code,
                Quantity = -351m,
                BatchNumber = "LOT-EVA-OPEN",
                UnitCost = eva.StandardUnitCost,
                TotalCost = -1474.20m,
                QualityStatus = InventoryQualityStatus.Released,
                SourceDocumentNumber = workOrder2.WorkOrderNumber,
                SourceEvent = "MES backflush",
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-1)
            },
            new InventoryTransactionEntity
            {
                Id = Id("ITX-004"),
                TransactionNumber = "ITX-0004",
                TransactionType = InventoryTransactionType.ProductionReceipt,
                WarehouseId = fgWarehouse.Id,
                ItemCode = panel610.Code,
                Quantity = 36m,
                BatchNumber = workOrder2.FinishedGoodsBatchNumber,
                UnitCost = 412.75m,
                TotalCost = 14859m,
                QualityStatus = InventoryQualityStatus.PendingInspection,
                SourceDocumentNumber = workOrder2.WorkOrderNumber,
                SourceEvent = "MES completion report",
                OccurredAt = DateTimeOffset.UtcNow.AddHours(-6)
            });

        dbContext.CostPostings.AddRange(
            CostPosting("COST-001", workOrder2, productionOrder2, "Material", 12909m, "Backflush material issue"),
            CostPosting("COST-002", workOrder2, productionOrder2, "Labor", 450m, "Reported labor hours"),
            CostPosting("COST-003", workOrder2, productionOrder2, "Machine", 520m, "Reported machine hours"),
            CostPosting("COST-004", workOrder2, productionOrder2, "Overhead", 608m, "Standard overhead accrual"),
            CostPosting("COST-005", workOrder2, productionOrder2, "Scrap", 372m, "Scrap loss accrual"));

        dbContext.AccountingBridgeEntries.Add(
            new AccountingBridgeEntryEntity
            {
                Id = Id("BRIDGE-001"),
                BridgeNumber = "BR-0001",
                SourceDocumentNumber = workOrder2.WorkOrderNumber,
                SourceEvent = "WIP consumption",
                State = AccountingBridgeState.ReadyForExport,
                Amount = 14859m,
                PostingAccount = "WIP-MFG",
                GeneratedAt = DateTimeOffset.UtcNow.AddHours(-4)
            });

        dbContext.QualityInspections.Add(
            new QualityInspectionEntity
            {
                Id = Id("INSP-001"),
                InspectionNumber = "QI-0001",
                InspectionType = InspectionType.Final,
                WorkOrderId = workOrder2.Id,
                ProductionOrderId = productionOrder2.Id,
                BatchNumber = workOrder2.FinishedGoodsBatchNumber ?? "FG-UNKNOWN",
                InspectedQuantity = 36m,
                DefectQuantity = 2m,
                State = InspectionRecordState.InInspection,
                Disposition = null,
                InspectorUserName = null,
                Notes = "Final inspection pending quality release.",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            });

        dbContext.AuditLogs.Add(
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Module = "Seed",
                ActionName = "DatabaseInitialized",
                Detail = "Loaded baseline manufacturing dataset.",
                PerformedBy = "system",
                ActorUserId = null,
                OccurredAt = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static InventoryTransactionEntity OpeningReceipt(
        WarehouseEntity warehouse,
        string itemCode,
        decimal quantity,
        decimal unitCost,
        string batchNumber,
        string sourceEvent)
    {
        return new InventoryTransactionEntity
        {
            Id = Guid.NewGuid(),
            TransactionNumber = $"ITX-OPEN-{itemCode}",
            TransactionType = InventoryTransactionType.GoodsReceipt,
            WarehouseId = warehouse.Id,
            ItemCode = itemCode,
            Quantity = quantity,
            BatchNumber = batchNumber,
            UnitCost = unitCost,
            TotalCost = quantity * unitCost,
            QualityStatus = InventoryQualityStatus.Released,
            SourceDocumentNumber = "OPENING",
            SourceEvent = sourceEvent,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-7)
        };
    }

    private static CostPostingEntity CostPosting(
        string seed,
        WorkOrderEntity workOrder,
        ProductionOrderEntity productionOrder,
        string postingType,
        decimal amount,
        string reference)
    {
        return new CostPostingEntity
        {
            Id = Id(seed),
            WorkOrderId = workOrder.Id,
            ProductionOrderId = productionOrder.Id,
            PostingType = postingType,
            Amount = amount,
            Reference = reference,
            PostedAt = DateTimeOffset.UtcNow.AddHours(-3)
        };
    }

    private static (UserEntity User, RoleEntity Role) CreateUser(string userName, string displayName, DepartmentEntity department, RoleEntity role)
    {
        return (
            new UserEntity
            {
                Id = Id($"USER-{userName.ToUpperInvariant()}"),
                UserName = userName,
                DisplayName = displayName,
                PasswordHash = PasswordSecurity.HashPassword(PasswordSecurity.DefaultSeedPassword),
                PasswordChangedAt = DateTimeOffset.UtcNow,
                MustChangePassword = false,
                PasswordResetExpiresAt = null,
                FailedAccessCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                DepartmentId = department.Id
            },
            role);
    }

    private static async Task EnsureUserSecurityAsync(ManufacturingDbContext dbContext, CancellationToken cancellationToken)
    {
        var usersNeedingPassword = await dbContext.Users
            .Where(entity => string.IsNullOrWhiteSpace(entity.PasswordHash))
            .ToArrayAsync(cancellationToken);

        if (usersNeedingPassword.Length == 0)
        {
            return;
        }

        foreach (var user in usersNeedingPassword)
        {
            user.PasswordHash = PasswordSecurity.HashPassword(PasswordSecurity.DefaultSeedPassword);
            user.PasswordChangedAt = DateTimeOffset.UtcNow;
            user.MustChangePassword = true;
            user.PasswordResetExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
            user.FailedAccessCount = 0;
            user.LockedUntil = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, PermissionEntity> CreatePermissions()
    {
        return new[]
        {
            Permission("platform.admin", "Platform administration"),
            Permission("crm.customer.read", "Read customers"),
            Permission("crm.customer.write", "Maintain customers"),
            Permission("crm.sales-order.read", "Read sales orders"),
            Permission("crm.sales-order.write", "Maintain sales orders"),
            Permission("crm.sales-order.approve", "Approve sales orders"),
            Permission("crm.sales-order.release", "Release sales orders"),
            Permission("erp.supplier.read", "Read suppliers"),
            Permission("erp.purchase-order.read", "Read purchase orders"),
            Permission("erp.purchase-order.write", "Maintain purchase orders"),
            Permission("erp.purchase-order.approve", "Approve purchase orders"),
            Permission("erp.goods-receipt.write", "Receive purchased materials"),
            Permission("erp.product.read", "Read product master"),
            Permission("erp.production-order.read", "Read production orders"),
            Permission("erp.production-order.release", "Release production orders"),
            Permission("erp.inventory.read", "Read inventory"),
            Permission("erp.transfer.write", "Execute warehouse transfers"),
            Permission("erp.cost.read", "Read cost ledgers"),
            Permission("erp.shipment.read", "Read sales shipments"),
            Permission("erp.shipment.write", "Create sales shipments"),
            Permission("erp.invoice.read", "Read invoice support documents"),
            Permission("erp.invoice.write", "Create and issue invoice support documents"),
            Permission("erp.settlement.read", "Read receivable and payable entries"),
            Permission("erp.settlement.write", "Apply receivable and payable settlements"),
            Permission("erp.period.close", "Close accounting periods"),
            Permission("erp.accounting.manage", "Manage accounting bridge"),
            Permission("integration.read", "Read integration status"),
            Permission("integration.manage", "Manage integration jobs"),
            Permission("mes.work-order.read", "Read work orders"),
            Permission("mes.work-order.execute", "Execute work orders"),
            Permission("quality.inspection.read", "Read quality inspections"),
            Permission("quality.inspection.write", "Complete quality inspections"),
            Permission("reporting.dashboard.read", "Read dashboard"),
            Permission("ai.summary.read", "Read AI summary")
        }.ToDictionary(permission => permission.Code, StringComparer.OrdinalIgnoreCase);
    }

    private static PermissionEntity Permission(string code, string name)
    {
        return new PermissionEntity
        {
            Id = Id($"PERM-{code}"),
            Code = code,
            Name = name
        };
    }

    private static void LinkRolePermissions(ManufacturingDbContext dbContext, RoleEntity role, params IEnumerable<PermissionEntity>[] groups)
    {
        foreach (var permission in groups.SelectMany(group => group))
        {
            dbContext.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            });
        }
    }

    private static void LinkRolePermissions(ManufacturingDbContext dbContext, RoleEntity role, params PermissionEntity[] permissions)
    {
        foreach (var permission in permissions)
        {
            dbContext.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            });
        }
    }

    private static Guid Id(string seed)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes);
    }

    private static async Task<bool> HasTableAsync(ManufacturingDbContext dbContext, string tableName, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var count = await dbContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {tableName}")
                .SingleAsync(cancellationToken);

            return count > 0;
        }

        if (dbContext.Database.IsSqlServer())
        {
            var count = await dbContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = {tableName}")
                .SingleAsync(cancellationToken);

            return count > 0;
        }

        if (string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var count = await dbContext.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM information_schema.tables WHERE table_schema = 'public' AND table_name = {tableName}")
                .SingleAsync(cancellationToken);

            return count > 0;
        }

        return false;
    }
}
