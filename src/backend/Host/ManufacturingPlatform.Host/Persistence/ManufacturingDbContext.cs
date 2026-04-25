using Microsoft.EntityFrameworkCore;

namespace ManufacturingPlatform.Host.Persistence;

public sealed class ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options) : DbContext(options)
{
    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<WarehouseEntity> Warehouses => Set<WarehouseEntity>();
    public DbSet<MaterialEntity> Materials => Set<MaterialEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<BomComponentEntity> BomComponents => Set<BomComponentEntity>();
    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();
    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
    public DbSet<PurchaseOrderEntity> PurchaseOrders => Set<PurchaseOrderEntity>();
    public DbSet<GoodsReceiptEntity> GoodsReceipts => Set<GoodsReceiptEntity>();
    public DbSet<SalesOrderEntity> SalesOrders => Set<SalesOrderEntity>();
    public DbSet<SalesShipmentEntity> SalesShipments => Set<SalesShipmentEntity>();
    public DbSet<ProductionOrderEntity> ProductionOrders => Set<ProductionOrderEntity>();
    public DbSet<WorkOrderEntity> WorkOrders => Set<WorkOrderEntity>();
    public DbSet<WorkOrderOperationEntity> WorkOrderOperations => Set<WorkOrderOperationEntity>();
    public DbSet<InventoryTransactionEntity> InventoryTransactions => Set<InventoryTransactionEntity>();
    public DbSet<WarehouseTransferEntity> WarehouseTransfers => Set<WarehouseTransferEntity>();
    public DbSet<CostPostingEntity> CostPostings => Set<CostPostingEntity>();
    public DbSet<AccountingBridgeEntryEntity> AccountingBridgeEntries => Set<AccountingBridgeEntryEntity>();
    public DbSet<SettlementEntryEntity> SettlementEntries => Set<SettlementEntryEntity>();
    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();
    public DbSet<SettlementApplicationEntity> SettlementApplications => Set<SettlementApplicationEntity>();
    public DbSet<AccountingPeriodEntity> AccountingPeriods => Set<AccountingPeriodEntity>();
    public DbSet<AccountingVoucherEntity> AccountingVouchers => Set<AccountingVoucherEntity>();
    public DbSet<AccountingVoucherLineEntity> AccountingVoucherLines => Set<AccountingVoucherLineEntity>();
    public DbSet<QualityInspectionEntity> QualityInspections => Set<QualityInspectionEntity>();
    public DbSet<AiInsightRunEntity> AiInsightRuns => Set<AiInsightRunEntity>();
    public DbSet<ReportExportJobEntity> ReportExportJobs => Set<ReportExportJobEntity>();
    public DbSet<ExternalSystemEntity> ExternalSystems => Set<ExternalSystemEntity>();
    public DbSet<AccountingExportBatchEntity> AccountingExportBatches => Set<AccountingExportBatchEntity>();
    public DbSet<IntegrationJobEntity> IntegrationJobs => Set<IntegrationJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepartmentEntity>().HasIndex(entity => entity.Code).IsUnique();

        modelBuilder.Entity<RoleEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<PermissionEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<UserEntity>().HasIndex(entity => entity.UserName).IsUnique();
        modelBuilder.Entity<SessionEntity>().HasKey(entity => entity.Token);

        modelBuilder.Entity<UserRoleEntity>().HasKey(entity => new { entity.UserId, entity.RoleId });
        modelBuilder.Entity<RolePermissionEntity>().HasKey(entity => new { entity.RoleId, entity.PermissionId });

        modelBuilder.Entity<ProductEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<MaterialEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<CustomerEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<SupplierEntity>().HasIndex(entity => entity.Code).IsUnique();
        modelBuilder.Entity<PurchaseOrderEntity>().HasIndex(entity => entity.OrderNumber).IsUnique();
        modelBuilder.Entity<GoodsReceiptEntity>().HasIndex(entity => entity.ReceiptNumber).IsUnique();
        modelBuilder.Entity<SalesOrderEntity>().HasIndex(entity => entity.OrderNumber).IsUnique();
        modelBuilder.Entity<SalesShipmentEntity>().HasIndex(entity => entity.ShipmentNumber).IsUnique();
        modelBuilder.Entity<ProductionOrderEntity>().HasIndex(entity => entity.ProductionOrderNumber).IsUnique();
        modelBuilder.Entity<WorkOrderEntity>().HasIndex(entity => entity.WorkOrderNumber).IsUnique();
        modelBuilder.Entity<InventoryTransactionEntity>().HasIndex(entity => entity.TransactionNumber).IsUnique();
        modelBuilder.Entity<WarehouseTransferEntity>().HasIndex(entity => entity.TransferNumber).IsUnique();
        modelBuilder.Entity<AccountingBridgeEntryEntity>().HasIndex(entity => entity.BridgeNumber).IsUnique();
        modelBuilder.Entity<SettlementEntryEntity>().HasIndex(entity => entity.EntryNumber).IsUnique();
        modelBuilder.Entity<InvoiceEntity>().HasIndex(entity => entity.InvoiceNumber).IsUnique();
        modelBuilder.Entity<InvoiceEntity>().HasIndex(entity => entity.SettlementEntryId).IsUnique();
        modelBuilder.Entity<SettlementApplicationEntity>().HasIndex(entity => entity.ApplicationNumber).IsUnique();
        modelBuilder.Entity<AccountingPeriodEntity>().HasIndex(entity => entity.PeriodCode).IsUnique();
        modelBuilder.Entity<AccountingVoucherEntity>().HasIndex(entity => entity.VoucherNumber).IsUnique();
        modelBuilder.Entity<AccountingVoucherEntity>().HasIndex(entity => entity.PeriodCode);
        modelBuilder.Entity<AccountingVoucherLineEntity>().HasIndex(entity => entity.AccountingBridgeEntryId);
        modelBuilder.Entity<AccountingVoucherLineEntity>().HasIndex(entity => new { entity.AccountingVoucherId, entity.LineNumber }).IsUnique();
        modelBuilder.Entity<QualityInspectionEntity>().HasIndex(entity => entity.InspectionNumber).IsUnique();
        modelBuilder.Entity<ReportExportJobEntity>().HasIndex(entity => entity.JobNumber).IsUnique();
        modelBuilder.Entity<ExternalSystemEntity>().HasIndex(entity => entity.SystemCode).IsUnique();
        modelBuilder.Entity<AccountingExportBatchEntity>().HasIndex(entity => entity.BatchNumber).IsUnique();
        modelBuilder.Entity<IntegrationJobEntity>().HasIndex(entity => entity.JobNumber).IsUnique();
        modelBuilder.Entity<IntegrationJobEntity>().HasIndex(entity => entity.ExternalJobNumber);

        modelBuilder.Entity<PurchaseOrderEntity>()
            .HasOne(entity => entity.Supplier)
            .WithMany(entity => entity.PurchaseOrders)
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderEntity>()
            .HasOne(entity => entity.Material)
            .WithMany(entity => entity.PurchaseOrders)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderEntity>()
            .HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptEntity>()
            .HasOne(entity => entity.PurchaseOrder)
            .WithMany(entity => entity.GoodsReceipts)
            .HasForeignKey(entity => entity.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptEntity>()
            .HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesOrderEntity>()
            .HasOne(entity => entity.Customer)
            .WithMany(entity => entity.SalesOrders)
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesOrderEntity>()
            .HasOne(entity => entity.ProductionOrder)
            .WithOne(entity => entity.SalesOrder)
            .HasForeignKey<ProductionOrderEntity>(entity => entity.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesShipmentEntity>()
            .HasOne(entity => entity.SalesOrder)
            .WithMany(entity => entity.SalesShipments)
            .HasForeignKey(entity => entity.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesShipmentEntity>()
            .HasOne(entity => entity.Warehouse)
            .WithMany()
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductionOrderEntity>()
            .HasOne(entity => entity.WorkOrder)
            .WithOne(entity => entity.ProductionOrder)
            .HasForeignKey<WorkOrderEntity>(entity => entity.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkOrderOperationEntity>()
            .HasOne(entity => entity.WorkOrder)
            .WithMany(entity => entity.Operations)
            .HasForeignKey(entity => entity.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BomComponentEntity>()
            .HasOne(entity => entity.Product)
            .WithMany(entity => entity.BomComponents)
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BomComponentEntity>()
            .HasOne(entity => entity.Material)
            .WithMany(entity => entity.BomComponents)
            .HasForeignKey(entity => entity.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InventoryTransactionEntity>()
            .HasOne(entity => entity.Warehouse)
            .WithMany(entity => entity.InventoryTransactions)
            .HasForeignKey(entity => entity.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WarehouseTransferEntity>()
            .HasOne(entity => entity.FromWarehouse)
            .WithMany()
            .HasForeignKey(entity => entity.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WarehouseTransferEntity>()
            .HasOne(entity => entity.ToWarehouse)
            .WithMany()
            .HasForeignKey(entity => entity.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceEntity>()
            .HasOne(entity => entity.SettlementEntry)
            .WithOne(entity => entity.Invoice)
            .HasForeignKey<InvoiceEntity>(entity => entity.SettlementEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SettlementApplicationEntity>()
            .HasOne(entity => entity.SettlementEntry)
            .WithMany(entity => entity.Applications)
            .HasForeignKey(entity => entity.SettlementEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SettlementApplicationEntity>()
            .HasOne(entity => entity.Invoice)
            .WithMany(entity => entity.Applications)
            .HasForeignKey(entity => entity.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AccountingVoucherLineEntity>()
            .HasOne(entity => entity.AccountingVoucher)
            .WithMany(entity => entity.Lines)
            .HasForeignKey(entity => entity.AccountingVoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountingVoucherLineEntity>()
            .HasOne(entity => entity.AccountingBridgeEntry)
            .WithMany()
            .HasForeignKey(entity => entity.AccountingBridgeEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CostPostingEntity>()
            .HasOne(entity => entity.WorkOrder)
            .WithMany()
            .HasForeignKey(entity => entity.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CostPostingEntity>()
            .HasOne(entity => entity.ProductionOrder)
            .WithMany()
            .HasForeignKey(entity => entity.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<QualityInspectionEntity>()
            .HasOne(entity => entity.WorkOrder)
            .WithMany(entity => entity.QualityInspections)
            .HasForeignKey(entity => entity.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<QualityInspectionEntity>()
            .HasOne(entity => entity.ProductionOrder)
            .WithMany()
            .HasForeignKey(entity => entity.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccountingBridgeEntryEntity>()
            .HasOne(entity => entity.ExportBatch)
            .WithMany(entity => entity.AccountingBridges)
            .HasForeignKey(entity => entity.ExportBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<IntegrationJobEntity>()
            .HasOne(entity => entity.ExportBatch)
            .WithMany(entity => entity.IntegrationJobs)
            .HasForeignKey(entity => entity.ExportBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
