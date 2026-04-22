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
    public DbSet<SalesOrderEntity> SalesOrders => Set<SalesOrderEntity>();
    public DbSet<ProductionOrderEntity> ProductionOrders => Set<ProductionOrderEntity>();
    public DbSet<WorkOrderEntity> WorkOrders => Set<WorkOrderEntity>();
    public DbSet<WorkOrderOperationEntity> WorkOrderOperations => Set<WorkOrderOperationEntity>();
    public DbSet<InventoryTransactionEntity> InventoryTransactions => Set<InventoryTransactionEntity>();
    public DbSet<CostPostingEntity> CostPostings => Set<CostPostingEntity>();
    public DbSet<AccountingBridgeEntryEntity> AccountingBridgeEntries => Set<AccountingBridgeEntryEntity>();
    public DbSet<QualityInspectionEntity> QualityInspections => Set<QualityInspectionEntity>();
    public DbSet<AiInsightRunEntity> AiInsightRuns => Set<AiInsightRunEntity>();

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
        modelBuilder.Entity<SalesOrderEntity>().HasIndex(entity => entity.OrderNumber).IsUnique();
        modelBuilder.Entity<ProductionOrderEntity>().HasIndex(entity => entity.ProductionOrderNumber).IsUnique();
        modelBuilder.Entity<WorkOrderEntity>().HasIndex(entity => entity.WorkOrderNumber).IsUnique();
        modelBuilder.Entity<InventoryTransactionEntity>().HasIndex(entity => entity.TransactionNumber).IsUnique();
        modelBuilder.Entity<AccountingBridgeEntryEntity>().HasIndex(entity => entity.BridgeNumber).IsUnique();
        modelBuilder.Entity<QualityInspectionEntity>().HasIndex(entity => entity.InspectionNumber).IsUnique();

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
    }
}
