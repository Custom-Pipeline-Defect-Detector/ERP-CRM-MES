# Kingdee-Level Architecture Audit And Upgrade

## 1. Issues Found By Severity

### Critical

- Business-finance integration is not strict enough. The blueprint mentions cost accounting and voucher export, but it does not enforce that every approved transaction produces a defined financial impact.
- Source-of-truth ownership is unclear. Customer, product, BOM, route, warehouse, and quality standards are listed as shared data, but no ownership matrix prevents duplication or conflicting edits.
- MES and ERP boundaries are not hard enough. The current design allows ERP and MES responsibilities to blur around production execution, which is dangerous in real factory operation.
- State machines are missing for key business objects. Without mandatory states and transitions, approval, execution, and closure behavior will vary by screen or developer.
- The SRS is still largely a placeholder and does not enforce the same integration rigor described in the blueprint.

### High

- CRM is too light for real commercial process management. It lacks stage history, activity timeline, reminder discipline, sales funnel conversion tracking, and meaningful after-sales linkage.
- MES is too generic for shop-floor control. It lacks WIP tracking, lot split/merge, operator accountability, manual vs backflush consumption rules, and OEE-lite measurement.
- Cost accounting is structurally incomplete. Material, labor, machine, overhead, scrap, and variance are mentioned, but not tied to document events or settlement logic.
- Quality does not fully control inventory status. The design does not clearly enforce quarantine, hold, release, and reject effects on stock availability.
- Reporting and AI governance are under-specified. There is no semantic dataset layer, drill-down rule, or explainability boundary.

### Medium

- Integration direction and conflict resolution are too generic. Sync scope exists, but authoritative direction, retry behavior, and reconciliation rules are missing.
- After-sales service is not linked strongly enough to production genealogy, shipped batches, or warranty scope.
- The current data model placeholders do not yet reflect the real operational granularity required for factory execution and cost.

## 2. Inconsistencies Between Blueprint And SRS

- The blueprint expects usable cost accounting in the first release, but the SRS only says `cost accounting support`, which is too weak.
- The blueprint says AI should use approved reporting data, while the SRS does not enforce dataset governance or explainability.
- The blueprint includes after-sales flow and contract emphasis, but the SRS CRM section is missing a real lead-to-order commercial pipeline and activity management.
- The blueprint suggests strong integration with Kingdee coexistence, but the SRS integration section does not define ownership, direction, or reconciliation rules.
- The blueprint positions management analysis as a core goal, but the SRS reporting section does not require drill-down or governed reporting datasets.

## 3. Missing Critical Flows

### Commercial to financial flow

- Lead -> Opportunity -> Quote -> Contract -> SalesOrder -> Shipment -> Receivable / voucher bridge

### Planning to execution to cost flow

- SalesOrder -> demand planning -> ProductionOrder -> WorkOrder / operation execution -> WIP -> finished goods receipt -> work order cost settlement -> finished goods cost

### Procurement to production to finance flow

- PurchaseRequest -> PurchaseOrder -> GoodsReceipt -> IncomingInspection -> Released stock -> MaterialIssue / backflush -> production cost -> payable / voucher bridge

### Quality-controlled inventory flow

- Receipt / production completion -> Quality status -> quarantine / release / reject -> available stock or nonconforming stock

### After-sales closed-loop flow

- AfterSalesTicket -> shipped lot / batch genealogy -> defect / root cause -> NCR / CAPA -> corrective action -> reporting

## 4. Incorrect Assumptions

- `Finance depth stays lightweight initially` is not acceptable at enterprise level. Even if Kingdee remains the finance ledger, transaction-to-finance linkage must be designed in phase 1.
- Treating MES as mostly work reporting is insufficient. A real factory MES must own execution truth, WIP movement, consumption actuals, and traceability.
- Treating CRM as customer records plus orders is insufficient. A factory with B2B sales needs pipeline management, activities, contracts, reminders, and performance tracking.
- Leaving report data sources unspecified is unsafe. Reporting and AI cannot read arbitrary transactional tables.

## 5. Specific Improvements By Module

### CRM

- Add lead qualification, opportunity stage management, quote revisioning, contract lifecycle, interaction timeline, reminders, and sales target tracking.
- Keep SalesOrder as a commercially owned order object through approval, then let ERP consume it for fulfillment and finance effects.
- Link after-sales tickets to customer, contract, shipped batch/lot, and warranty context.

### ERP

- Add an accounting bridge layer for voucher export, reconciliation, and posting confirmation when Kingdee remains.
- Define inventory status logic with quality influence, reservations, available quantity, and batch balances.
- Add cost centers, overhead rules, cost allocation rules, and settlement objects.

### MES

- Make MES the only system of record for execution actuals: operator, machine, time, quantity, scrap, rework, and genealogy.
- Add operation-level execution, WIP lot movement, backflush/manual issue rule, and shift accountability.
- Add downtime categorization and OEE-lite KPI capture.

### Quality

- Quality must own inspection disposition and stock release status.
- Add hold, conditional release, reject, rework-request, and CAPA/NCR relationship rules.

### Reporting

- Add curated datasets, KPI definitions, drill-down paths, and versioned report definitions.
- Prevent direct ad hoc reads from raw operational tables for official dashboards.

### AI

- Restrict AI to approved reporting datasets only.
- Add explainability requirements, rule traceability, and input-window disclosure.

### Integration

- Define direction per entity, conflict rule, retry rule, and reconciliation method.
- Daily coexistence reconciliation with Kingdee is mandatory for inventory, orders, and accounting bridge totals.

## 6. Updated MES Section

The MES section should be treated as the following implementation baseline.

### MES scope

- Production order intake from ERP planning
- Work order decomposition by route and operation
- Detailed operation execution with operator login, start, pause, resume, complete, and close events
- WIP lot tracking per operation and shift
- Manual issue and backflush consumption policy by operation and material
- Batch / lot genealogy from raw material to WIP to finished goods
- Rework loops, scrap handling, and nonconforming segregation
- Machine-based and manual operation distinction
- Downtime categorization and analysis
- Shift-based and operator-based production reporting
- OEE-lite and throughput KPI generation

### MES source-of-truth rule

- ERP owns plan and release.
- MES owns execution actuals.
- Quality owns inspection result and release decision.
- ERP consumes MES actuals for inventory, costing, and finance bridge.

### MES minimum objects

- ProductionOrderRelease
- WorkOrder
- WorkOrderOperation
- OperationDispatch
- WipLot
- WipMove
- MaterialIssue
- MaterialConsumptionActual
- LaborTimeEntry
- MachineTimeEntry
- ScrapRecord
- ReworkOrder
- BatchGenealogyLink
- DowntimeRecord
- OeeSnapshot
- OperatorAssignment

### MES minimum KPIs

- planned quantity vs actual quantity
- good quantity vs scrap quantity
- rework quantity
- downtime minutes by reason
- output by shift
- output by operator
- availability, performance, quality

## 7. Updated CRM Section

The CRM section should be treated as the following implementation baseline.

### CRM scope

- Lead registration and qualification / disqualification
- Opportunity management with stage history and conversion tracking
- Quote management with versioning and approval
- Contract lifecycle management
- Sales activity timeline including calls, visits, meetings, and messages
- Follow-up task and reminder system
- SalesOrder creation from approved commercial documents
- Customer segmentation and risk notes
- Sales performance tracking by user, team, segment, region, and period
- After-sales ticket management linked to shipped lots / batches and contracts

### CRM source-of-truth rule

- CRM owns customer identity, relationship history, and commercial pipeline.
- ERP and MES may consume commercial data, but they do not own customer interaction history.
- After-sales records belong to CRM, but must reference MES genealogy and Quality dispositions when relevant.

### CRM minimum objects

- Lead
- LeadQualificationRecord
- Opportunity
- OpportunityStageHistory
- Quote
- QuoteRevision
- SalesActivity
- FollowUpTask
- Reminder
- Contract
- ContractRevision
- SalesTarget
- SalesPerformanceSnapshot
- AfterSalesTicket
- WarrantyClaim

## 8. Updated Cost / Accounting Flow

### Mandatory cost inputs

- material issue actuals from MES
- labor time actuals from MES
- machine time actuals from MES
- overhead allocation rules from ERP
- quality loss and scrap actuals from MES / Quality

### Recommended flow

1. ERP planning releases ProductionOrder to MES.
2. MES records actual issue, labor, machine, and execution quantities by operation.
3. MES sends trusted execution actuals to ERP costing.
4. ERP accumulates work order cost and batch cost.
5. ERP settles finished goods cost at completion or close.
6. ERP creates accounting bridge entries for inventory movement, WIP movement, variance, and scrap.
7. Kingdee receives voucher bridge output and returns posting confirmation where coexistence applies.
8. Reconciliation compares operational totals with finance-posted totals daily.

### Mandatory outputs

- work order actual cost
- batch actual cost
- finished goods unit cost
- standard vs actual variance
- scrap loss
- rework cost
- accounting bridge entry status

## 9. Data Model Changes Needed

### Additions needed in CRM

- Lead
- OpportunityStageHistory
- SalesActivity
- FollowUpTask
- Reminder
- QuoteRevision
- ContractRevision
- SalesTarget
- SalesPerformanceSnapshot
- WarrantyClaim

### Additions needed in ERP

- PurchaseRequest
- SalesShipment
- SalesReturn
- StockReservation
- BatchBalance
- InventoryLedgerEntry
- CostCenter
- OverheadRule
- CostAllocationRule
- WorkOrderCostLedger
- BatchCostLedger
- AccountingBridgeEntry
- VoucherExportBatch

### Additions needed in MES

- WorkOrderOperation
- OperationDispatch
- WipLot
- WipMove
- LaborTimeEntry
- MachineTimeEntry
- MaterialConsumptionActual
- ScrapRecord
- ReworkOrder
- OperatorAssignment
- EquipmentStatusLog
- BatchGenealogyLink
- OeeSnapshot

### Additions needed in Quality

- InspectionLot
- QualityStatus
- HoldReleaseRecord
- NonconformanceDisposition
- DefectTrendSnapshot

### Additions needed in Reporting / AI

- DatasetDefinition
- SemanticMetricDefinition
- DrillDownDefinition
- InsightRun
- ForecastScenario
- AnomalyRule

## 10. Architecture And Module Boundary Changes Needed

### Mandatory corrections

- Add an explicit `AccountingBridge` capability inside ERP if Kingdee remains.
- Add an explicit governed reporting dataset layer between transactional modules and AI.
- Add a cross-module source-of-truth matrix as an architecture artifact, not just a business note.
- Add state-machine definitions for every approval-controlled or inventory-affecting document.

### Recommended boundary refinement

- Introduce a `MasterData` or `ManufacturingMasterData` boundary for shared controlled entities such as Product, Material, BOM, Route, UoM, and QualityStandard.
- Keep CRM responsible for customer and commercial pipeline, not inventory or execution.
- Keep ERP responsible for procurement, stock ledger, cost settlement, and accounting bridge.
- Keep MES responsible for execution actuals only, not commercial or financial approval logic.
- Keep Quality responsible for inspection truth and disposition, not stock valuation.

## 11. Implementation Priority Correction

- Phase 1 must include finance-impact design, not only operational workflows.
- Phase 1 must include state machines, ownership rules, and accounting bridge design.
- Phase 1 MES must include WIP, genealogy, and operator accountability.
- Phase 1 CRM must include pipeline stages, activities, reminders, and contract linkage.
- AI remains a later phase, but the reporting dataset layer required for AI must start in phase 1.
