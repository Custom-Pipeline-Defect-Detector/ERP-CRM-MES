using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.CRM.Domain;

namespace ManufacturingPlatform.Modules.CRM.Application;

public static class CrmGovernance
{
    public static readonly DataOwnershipDefinition[] OwnershipRules =
    [
        new(nameof(Customer), "CRM", ["ERP", "MES", "Reporting", "Integration"], "CRM owns customer identity, classification, and relationship context."),
        new(nameof(Lead), "CRM", ["Reporting"], "Leads exist only in CRM until qualified."),
        new(nameof(Opportunity), "CRM", ["ERP", "Reporting"], "Commercial demand intent is owned by CRM until converted."),
        new(nameof(Quote), "CRM", ["ERP", "Reporting"], "Quote revision history and customer-facing commercial terms remain in CRM."),
        new(nameof(Contract), "CRM", ["ERP", "Reporting"], "Contract lifecycle and revisions remain in CRM."),
        new(nameof(SalesOrder), "CRM", ["ERP", "MES", "Reporting"], "CRM owns commercial order approval; ERP consumes the released order for fulfillment and finance impact."),
        new(nameof(AfterSalesTicket), "CRM", ["MES", "Quality", "Reporting"], "CRM owns the service ticket while MES and Quality provide genealogy and defect context.")
    ];

    public static readonly WorkflowDefinition[] WorkflowDefinitions =
    [
        new(
            nameof(SalesOrder),
            "Sales / order management",
            Enum.GetNames<SalesOrderState>(),
            [
                new(nameof(SalesOrderState.Draft), nameof(SalesOrderState.Submitted), "Submit for approval", "Sales"),
                new(nameof(SalesOrderState.Submitted), nameof(SalesOrderState.Approved), "Approve order", "Sales manager"),
                new(nameof(SalesOrderState.Approved), nameof(SalesOrderState.Released), "Release to ERP fulfillment", "Order management"),
                new(nameof(SalesOrderState.Released), nameof(SalesOrderState.PartiallyFulfilled), "Ship partially", "Warehouse"),
                new(nameof(SalesOrderState.PartiallyFulfilled), nameof(SalesOrderState.Completed), "Complete shipment", "Warehouse"),
                new(nameof(SalesOrderState.Completed), nameof(SalesOrderState.Closed), "Close commercial order", "Sales"),
                new(nameof(SalesOrderState.Draft), nameof(SalesOrderState.Cancelled), "Cancel draft order", "Sales"),
                new(nameof(SalesOrderState.Approved), nameof(SalesOrderState.Cancelled), "Cancel approved order", "Sales manager")
            ])
    ];
}
