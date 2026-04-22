using ManufacturingPlatform.BuildingBlocks.Abstractions;
using ManufacturingPlatform.Modules.MES.Domain;

namespace ManufacturingPlatform.Modules.MES.Application;

public static class MesGovernance
{
    public static readonly DataOwnershipDefinition[] OwnershipRules =
    [
        new(nameof(ProductionOrderRelease), "MES", ["ERP", "Reporting"], "MES owns the operational release receipt and execution kickoff."),
        new(nameof(WorkOrder), "MES", ["ERP", "Quality", "Reporting"], "MES owns work-order execution truth."),
        new(nameof(WorkOrderOperation), "MES", ["ERP", "Quality", "Reporting"], "Operation state, operator accountability, and execution actuals belong to MES."),
        new(nameof(WipLot), "MES", ["ERP", "Quality", "Reporting"], "WIP location and status are owned by MES."),
        new(nameof(MaterialConsumptionActual), "MES", ["ERP", "Reporting"], "MES actual consumption is the trusted cost input."),
        new(nameof(BatchGenealogyLink), "MES", ["CRM", "Quality", "Reporting"], "MES owns genealogy from raw material to finished lot.")
    ];

    public static readonly WorkflowDefinition[] WorkflowDefinitions =
    [
        new(
            nameof(WorkOrder),
            "Workshop / MES",
            Enum.GetNames<WorkOrderState>(),
            [
                new(nameof(WorkOrderState.Created), nameof(WorkOrderState.Dispatched), "Dispatch work order", "MES scheduler"),
                new(nameof(WorkOrderState.Dispatched), nameof(WorkOrderState.Started), "Start execution", "Operator / MES"),
                new(nameof(WorkOrderState.Started), nameof(WorkOrderState.Paused), "Pause execution", "Operator / MES"),
                new(nameof(WorkOrderState.Paused), nameof(WorkOrderState.Started), "Resume execution", "Operator / MES"),
                new(nameof(WorkOrderState.Started), nameof(WorkOrderState.Completed), "Finish production", "Operator / MES"),
                new(nameof(WorkOrderState.Completed), nameof(WorkOrderState.Reported), "Post completion report", "MES"),
                new(nameof(WorkOrderState.Reported), nameof(WorkOrderState.Closed), "Close work order", "MES supervisor")
            ])
    ];

    public static readonly FinancialImpactDefinition[] FinancialImpacts =
    [
        new(nameof(MaterialConsumptionActual), "Create material actual cost input", "Work order material cost", "MES actual consumption drives ERP work-order costing."),
        new(nameof(LaborTimeEntry), "Create labor actual cost input", "Work order labor cost", "Operator-reported time flows into ERP cost ledgers."),
        new(nameof(MachineTimeEntry), "Create machine actual cost input", "Work order machine cost", "Machine hours feed ERP machine-cost absorption."),
        new(nameof(ScrapRecord), "Create scrap loss and variance input", "Scrap variance", "Scrap events remain visible both operationally and financially.")
    ];
}
