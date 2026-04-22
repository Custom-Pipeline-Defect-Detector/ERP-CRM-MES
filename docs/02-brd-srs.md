# BRD / SRS Draft

See also: `docs/03-kingdee-level-audit-upgrade.md` and Section 17 below for the architecture corrections that must govern the final SRS baseline.

## 1. Project Background

- Client:
- Factory location:
- Industry:
- Employee count:
- Current systems:
- Main business pain points:

## 2. Project Objectives

- Objective 1:
- Objective 2:
- Objective 3:
- Objective 4:

## 3. Stakeholders

- Sponsor:
- General manager:
- Sales owner:
- Procurement owner:
- Warehouse owner:
- Production owner:
- Quality owner:
- Finance owner:
- IT owner:

## 4. Scope

### In scope

- CRM
- ERP core
- MES core
- Quality
- Reporting
- Integration
- AI phase items
- On-premise deployment
- Source code delivery
- Training and handover

### Out of scope for first release

- Full statutory finance replacement unless explicitly confirmed
- Deep machine/PLC integration unless explicitly confirmed
- Full historical migration unless explicitly confirmed
- Advanced AI beyond approved reporting datasets

## 5. Current-State Summary

### 5.1 Current systems

- Kingdee:
- Existing factory system:
- Excel/manual work:

### 5.2 Current problems

- Data duplication:
- Process gaps:
- Reporting gaps:
- Cost accounting gaps:
- Traceability gaps:

## 6. Business Process Requirements

### 6.1 Sales process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.2 Procurement process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.3 Inventory process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.4 Production process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.5 Quality process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.6 Finance and cost process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

### 6.7 After-sales process

- Start event:
- End event:
- Departments involved:
- Key steps:
- Documents/data generated:

## 7. Master Data Requirements

- Customer
- Supplier
- Product
- Material
- BOM
- Process route
- Work center
- Machine
- Warehouse
- Employee
- Shift
- Unit of measure
- Quality standard
- Defect category

For each master data item, define:

- Code rule
- Owner department
- Required fields
- Approval rule
- Sync rule
- Source of truth

## 8. Functional Requirements

### 8.1 CRM

- Customer management
- Contact management
- Opportunity management
- Quote management
- Follow-up records
- Sales order creation
- Contract management
- After-sales records

### 8.2 ERP

- Supplier management
- Procurement
- Inventory
- Sales fulfillment support
- Expense registration
- Receivable/payable support
- Cost accounting support

### 8.3 MES

- Production order management
- Work order scheduling
- Route and process management
- Work center management
- Dispatching
- Material issue
- Production reporting
- Traceability
- Downtime and shift management
- Completion reporting

### 8.4 Quality

- Incoming inspection
- Process inspection
- Final inspection
- Defect recording
- Rework and rejection
- Quality statistics

### 8.5 Reporting

- Management dashboard
- Sales reports
- Inventory reports
- Production reports
- Quality reports
- Cost reports
- Export to Excel/PDF

### 8.6 AI

- Indicator summary
- Production anomaly warning
- Defect trend analysis
- Inventory suggestion
- Sales trend forecast
- Cost fluctuation interpretation
- Natural language report assistant

### 8.7 Platform and administration

- Organization structure
- User, role, permission
- Audit and operation logs
- Dictionary management
- Attachment management
- Notification and config

## 9. Integration Requirements

### 9.1 Legacy system strategy

- Kingdee kept or replaced:
- Existing factory system kept or replaced:

### 9.2 Interface scope

- Customer sync
- Supplier sync
- Product sync
- Material sync
- BOM sync
- Sales order sync
- Inventory sync
- Production sync
- Cost/accounting sync

### 9.3 Data migration

- Opening balance migration:
- Active business migration:
- Historical archive migration:

## 10. Reporting Requirements

For each report, define:

- Report name
- Business owner
- Data source
- Filter conditions
- Access permission
- Export format
- Print template requirement

## 11. Non-Functional Requirements

### 11.1 Technical

- Backend:
- Frontend:
- Database:
- API style:
- Reporting engine:

### 11.2 Security

- On-premise only
- Database LAN only
- RBAC
- Audit logs
- Password policy
- Backup and restore
- Internal HTTPS

### 11.3 Performance

- Concurrent users:
- Response targets:
- Production event latency:

## 12. Deployment Requirements

- Server topology:
- OS preference:
- Reverse proxy:
- File storage:
- Backup strategy:
- Monitoring/logging:

## 13. Acceptance Criteria

- End-to-end process acceptance:
- Traceability acceptance:
- Cost accounting acceptance:
- Reporting acceptance:
- Security acceptance:
- Deployment acceptance:
- Source code acceptance:
- Training acceptance:

## 14. Handover Requirements

- Source code package
- Deployment package
- API documentation
- Database design document
- Secondary development guide
- Report development guide
- Admin manual
- User manual
- Training recordings

## 15. Open Issues

- Issue 1:
- Issue 2:
- Issue 3:

## 16. Appendix

- Glossary
- Department abbreviations
- Document numbering rules

## 17. Enterprise Architecture Addendum

This addendum supersedes any weaker or more generic interpretation elsewhere in this draft.

### 17.1 End-to-end ownership and source of truth

| Entity / Object | System Owner | Consuming Modules | Rule |
| --- | --- | --- | --- |
| Lead, Opportunity, Quote, Sales Activity, Contract, AfterSalesTicket | CRM | ERP, Reporting | CRM owns the commercial lifecycle and customer interaction history |
| Customer master | CRM | ERP, MES, Reporting, Integration | CRM is source of truth for customer identity and classification |
| Supplier, Warehouse, Inventory ledger, Cost allocation rule, Accounting bridge entry | ERP | MES, Reporting, Integration | ERP owns procurement, stock, and finance-support records |
| Product, Material, BOM, Route, UoM, Quality standard | Shared master data governance led by ERP + manufacturing engineering | CRM, MES, Quality, Reporting | One approved version only; changes require controlled effectivity |
| ProductionOrder planning header | ERP / production planning | MES, Reporting, Integration | ERP owns release decision and target demand |
| WorkOrder, Operation execution, WIP lot, Material consumption actuals, Machine/Labor actuals, Batch genealogy | MES | ERP, Quality, Reporting, AI | MES is the only source of truth for production execution |
| InspectionPlan, InspectionRecord, NCR, CAPA, disposition | Quality | ERP, MES, Reporting | Quality owns inspection result and release/reject decision |
| Approved dataset, KPI metric, drill-down definition | Reporting | AI | AI may only consume approved reporting datasets |

### 17.2 Mandatory transaction-to-finance linkage

- Every approved sales shipment, goods receipt, material issue, production completion, scrap event, and expense record must generate a finance-impact event.
- If Kingdee remains, the platform must generate a controlled accounting bridge output for voucher creation, reconciliation, and posting confirmation.
- Cost accounting must be driven by MES actuals for material consumption, labor time, machine time, scrap and rework, plus overhead allocation rules from ERP.
- Finished goods cost cannot be estimated only from BOM standard cost; actual work order and batch actuals are required.

### 17.3 Workflow and state-machine requirements

| Object | Mandatory states | Owner |
| --- | --- | --- |
| SalesOrder | Draft -> Submitted -> Approved -> Released -> PartiallyFulfilled -> Completed -> Closed / Cancelled | Sales / order management |
| PurchaseOrder | Draft -> Submitted -> Approved -> Released -> PartiallyReceived -> Completed -> Closed / Cancelled | Purchasing |
| ProductionOrder | DraftPlan -> Approved -> ReleasedToMES -> InExecution -> TechnicallyCompleted -> Costed -> Closed / Cancelled | Planning |
| WorkOrder | Created -> Dispatched -> Started -> Paused -> Completed -> Reported -> Closed / Cancelled | Workshop / MES |
| InspectionRecord | Planned -> SampleCollected -> InInspection -> Accepted / Rejected / ConditionalRelease -> Closed | Quality |

Rules:

- No downstream execution is allowed from `Draft`.
- No inventory-affecting or finance-affecting transaction is allowed without an approved upstream source document.
- Manual override transitions must be permission-controlled and audit-logged.

### 17.4 Updated CRM requirements

- The CRM module must support `Lead -> Opportunity -> Quote -> Contract -> SalesOrder` as a continuous pipeline, not separate disconnected forms.
- Each opportunity must store stage, probability, expected amount, expected close date, competitor context, and win/loss reason.
- CRM must maintain a customer interaction timeline including calls, visits, meetings, complaint records, and follow-up tasks.
- Sales reminders and overdue follow-up alerts are mandatory.
- Sales performance views must support salesperson, team, customer segment, region, industry, and period dimensions.
- Contract lifecycle must include draft, approval, effective, revision, fulfillment linkage, and closure.
- After-sales tickets must support linkage to shipped batch/lot, finished product, contract, and root-cause disposition.

### 17.5 Updated MES requirements

- MES must manage detailed routing with versioned process routes and operation-level control.
- MES must track WIP at operation level, including lot split, merge, hold, release, and transfer between steps.
- MES must support manual issue and backflush consumption rules by material and operation.
- MES must distinguish machine-driven and manual operations.
- MES must record operator, shift, work center, machine, start time, pause time, end time, reported quantity, good quantity, scrap quantity, and rework quantity.
- MES must support rework loops, scrap reasons, and nonconforming output segregation.
- MES must maintain full batch genealogy from raw-material lot to WIP lot to finished-goods lot.
- MES must support downtime categorization by reason, duration, work center, machine, and shift.
- MES must produce OEE-lite KPIs at minimum: availability, performance, quality, and planned vs actual output.
- ERP may create and release production demand, but MES owns execution truth.

### 17.6 Updated cost and accounting requirements

- Work order cost must accumulate actual issued material cost, actual labor time cost, actual machine time cost, and allocated overhead.
- Batch cost must roll up all execution and quality disposition effects for that batch.
- Finished goods cost must be calculated at production completion or period close according to the selected costing policy.
- Variance analysis must include standard vs actual material, labor, machine, overhead absorption, and scrap / rework variance.
- Scrap events must remain visible both operationally and financially.

### 17.7 Reporting and AI governance

- Reports must read curated datasets or semantic views, not uncontrolled operational joins.
- Dashboards must support drill-down from KPI -> document -> transaction -> execution record.
- AI inputs are restricted to approved reporting datasets and aggregated business data.
- AI outputs must include input scope, time window, explainable rationale, and confidence or rule basis where applicable.
- Black-box AI recommendations without traceable data origin are not acceptable.

### 17.8 Integration constraints

- Integration must define direction per entity, conflict rule, retry rule, and reconciliation method.
- Customer and commercial order ownership cannot be duplicated between CRM and Kingdee without a system-of-record decision.
- Inventory and accounting bridge reconciliation must run daily during coexistence with Kingdee.
- Historical migration must preserve opening balances, open orders, open production, open AP/AR references where applicable, and the minimum traceability required for active batches.
