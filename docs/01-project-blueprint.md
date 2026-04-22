# Integrated CRM + ERP + MES + AI Manufacturing Platform

See also: `docs/03-kingdee-level-audit-upgrade.md` for the enterprise-architecture corrections that tighten finance linkage, MES ownership, CRM depth, and source-of-truth governance.

## 1. Client Requirement Summary

Build an on-premise manufacturing management platform for a solar factory in Shandong with about 200 employees.

Required characteristics:

- CRM + ERP + MES + AI in one integrated platform
- Source code delivery
- Open-source based preferred
- .NET preferred
- Local deployment only
- Database isolated inside the factory network
- IT team training and secondary development support
- Custom reporting support
- Fast delivery
- Budget target around 500,000 RMB

Current pain points:

- Kingdee and the current factory system are disconnected
- Cost accounting is weak
- Management cannot see end-to-end business analysis

## 2. Assumptions

This plan assumes the following unless discovery workshops prove otherwise:

- Recommendation: keep Kingdee in phase 1 for finance-related continuity, and replace the small factory system with the new MES gradually
- Recommendation: deliver as a modular monolith first, not microservices
- Recommendation: phase AI after core data, workflows, and reporting are stable
- Recommendation: start with browser-based intranet deployment for office and workshop terminals
- Recommendation: standard barcode/QR scanning is sufficient in phase 1; direct PLC or machine protocol integration is optional unless explicitly required
- Recommendation: financial impact and accounting-bridge rules must be defined in phase 1 even if Kingdee remains the statutory finance system of record
- Recommendation: the first release focuses on order-to-production-to-inventory traceability and usable cost accounting, not every advanced workflow edge case

Critical decisions still needed:

- Whether Kingdee remains the system of record for vouchers, receivables, payables, and statutory finance
- Whether historical data migration is full, partial, or opening-balance only
- Whether workshop equipment integration is manual, semi-automatic, or real-time
- Whether mobile devices, handheld scanners, and label printers are already available
- Whether the client IT team prefers PostgreSQL or SQL Server operations

## 3. Module Scope

### Phase 1 scope recommendation

Build these first:

- Platform foundation: organization, users, roles, permissions, dictionaries, attachments, audit logs
- Master data: customer, supplier, product, material, BOM, route, warehouse, work center, machine, shift, quality standards
- CRM core: lead, opportunity, quote, activity timeline, follow-up reminder, contract, after-sales linkage, sales order
- ERP core: procurement, inventory, inbound/outbound/transfer, stock ledger, shipment support, accounting bridge, cost baseline
- MES core: production order release intake, work order and operation execution, WIP tracking, material issue/backflush, batch genealogy, rework, scrap, downtime, completion reporting
- Quality core: incoming inspection, process inspection, final inspection, defects, rework/reject handling, inventory quality status control
- Reporting core: management dashboard, sales, inventory, production, quality, cost reports, approved semantic datasets
- Integration core: Kingdee and legacy data sync adapters

### Phase 2 scope recommendation

Add after phase 1 stabilizes:

- Advanced cost accounting depth
- Variance and scrap analysis
- NCR/CAPA
- Report designer and scheduled reports
- Role-based printable templates
- Approval workflows

### Phase 3 scope recommendation

Add last:

- AI summary assistant
- Trend forecasting
- Production anomaly detection
- Inventory suggestions
- Natural language Q&A over approved reporting data

## 4. Process Flow Backbone

The implementation should follow these end-to-end flows.

### Sales to delivery

- Start: lead or customer demand
- Owner departments: sales, planning, warehouse, finance
- End: shipment and order closure
- Core data: customer, quote, sales order, contract, shipment, invoice reference, return record

### Procurement to receipt

- Start: purchase request or MRP/manual demand
- Owner departments: planning, purchasing, warehouse, quality
- End: goods receipt accepted into inventory
- Core data: supplier, purchase request, purchase order, goods receipt, inspection result, stock transaction

### Production execution

- Start: approved production order
- Owner departments: planning, workshop, warehouse, quality
- End: finished goods receipt and work order closure
- Core data: production order, work order, route, dispatch, material issue, production report, downtime, trace record, completion report

### Quality flow

- Start: incoming material, in-process checkpoint, or final product inspection
- Owner departments: quality, warehouse, workshop
- End: accept, rework, reject, or CAPA action
- Core data: inspection plan, inspection record, defect record, NCR, CAPA

### Cost accounting flow

- Start: actual material issue, labor hours, machine time, and overhead rules
- Owner departments: finance, planning, workshop
- End: work order cost, batch cost, finished goods cost, variance report
- Core data: cost record, material issue, time report, machine usage, overhead rule, variance analysis

### After-sales flow

- Start: complaint, warranty issue, or service request
- Owner departments: sales, service, quality
- End: ticket closure and root-cause record
- Core data: after-sales ticket, complaint type, action log, resolution result

## 5. Data Model Backbone

### Shared master data

- Customer
- Supplier
- Product
- Material
- BOM
- ProcessRoute
- ProcessStep
- WorkCenter
- Machine
- Warehouse
- Employee
- Shift
- UnitOfMeasure
- QualityStandard
- DefectCategory

### CRM

- CustomerContact
- Opportunity
- Quote
- SalesOrder
- Contract
- FollowUpRecord
- AfterSalesTicket

### ERP

- PurchaseRequest
- PurchaseOrder
- GoodsReceipt
- SalesShipment
- InventoryTransaction
- StockBalance
- ExpenseRecord
- CostRecord

### MES

- ProductionOrder
- WorkOrder
- JobDispatch
- MaterialIssue
- ProductionReport
- DowntimeRecord
- ReworkRecord
- TraceRecord

### Quality

- InspectionPlan
- InspectionRecord
- DefectRecord
- NCR
- CAPA

### Platform

- User
- Role
- Permission
- Department
- AuditLog
- Attachment
- Notification
- ReportDefinition
- IntegrationJob

## 6. Integration Plan

### Recommended implementation choice

For budget and delivery control, start with:

- Keep Kingdee in phase 1 for accounting continuity
- Replace the current small factory system step by step with the new MES and reporting backbone
- Use integration adapters so the client can later decide whether to keep or retire Kingdee

### Minimum integration scope

- Customer sync
- Supplier sync
- Product and material sync
- BOM reference sync where needed
- Sales order sync
- Inventory balance or transaction sync
- Production completion sync
- Cost summary or voucher export sync

### Sync strategy

- Master data: scheduled sync plus manual re-sync
- Transactional data: event-driven where possible, scheduled fallback where not
- Failed sync: retry queue and error log
- Reconciliation: daily comparison reports for key totals

### Historical data strategy

Prefer this order:

- Opening balances first
- Current-year active business documents second
- Deep history only if it is contractually required

### Mandatory enterprise controls

- Every approved transaction must have a defined downstream financial impact, even if the final voucher remains inside Kingdee
- Every shared master entity must have a single source of truth and approved synchronization direction
- Every key business object must use an explicit state machine with ownership and transition rules
- MES must own production execution truth; ERP may release work but may not execute work
- Reporting and AI may only read curated datasets, not raw operational tables

## 7. Deployment Architecture

### Recommended technical approach

- Backend: ASP.NET Core Web API
- Architecture: modular monolith with strong domain boundaries
- ORM: EF Core
- Frontend: React + TypeScript
- UI library: Ant Design for speed and enterprise usability
- Database: PostgreSQL if open-source preference leads, SQL Server if client operations or Kingdee integration strongly favor it
- Reporting: reporting service with Excel and PDF export
- Auth: internal JWT or cookie-based session auth with RBAC

### Why modular monolith first

- Faster to deliver than microservices
- Easier for the client IT team to understand and maintain
- Simpler on-premise deployment
- Lower integration and operational risk
- Can still split into services later if scale justifies it

### Intranet deployment topology

- Reverse proxy on the internal network
- Web application server
- Background job worker
- Database server on internal LAN only
- File storage or NAS for attachments and report exports
- Backup service

### Recommended repository layout

```text
/docs
  /01-project-blueprint.md
  /02-brd-srs.md
  /03-process-flows.md
  /04-data-dictionary.md
  /05-integration-spec.md
  /06-deployment-runbook.md

/src
  /backend
    /Host
    /BuildingBlocks
    /Modules
      /CRM
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /ERP
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /MES
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /Quality
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /Reporting
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /AI
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /Platform
        /Domain
        /Application
        /Infrastructure
        /Contracts
      /Integration
        /Domain
        /Application
        /Infrastructure
        /Contracts

  /frontend
    /web
      /src
        /app
        /modules
        /pages
        /components
        /layouts
        /services
        /store
        /utils

/tests
  /backend
  /frontend
  /integration

/infra
  /database
  /scripts
  /deploy
```

### Module boundary rule

- `Platform` owns auth, RBAC, org, logs, dictionaries, attachments, notifications
- `CRM` owns customer-facing sales processes before fulfillment
- `ERP` owns procurement, inventory, stock movement, and finance-support records
- `MES` owns shop-floor execution and traceability
- `Quality` owns inspection and defect workflows
- `Reporting` owns dashboards, export jobs, and report definitions
- `Integration` owns all legacy and external system adapters
- `AI` can read approved reporting datasets, but should not own source-of-truth transactions

### Security baseline

- RBAC down to menu/action/data scope where needed
- Password policy
- Login log and operation log
- Audit trail for critical business records
- Internal HTTPS
- Database not routable from the public internet
- Backup and restore procedure
- Optional AD/LDAP integration if the client already uses it

## 8. Delivery Plan

### Phase 0: Discovery and solution definition

Outputs:

- BRD/SRS
- process maps
- master data definition
- integration decision
- deployment assessment
- acceptance baseline

### Phase 1: Foundation and core transactions

Outputs:

- shared platform
- master data
- CRM core
- sales, procurement, inventory core
- MES core
- basic reports
- audit and security baseline

### Phase 2: Quality and cost

Outputs:

- quality inspections
- defect handling
- traceability strengthening
- work order costing
- batch cost and variance reports
- management dashboards

### Phase 3: Reporting depth and controlled workflow

Outputs:

- report templates
- custom filters
- scheduled reports
- approval workflow where needed
- stronger reconciliation with Kingdee

### Phase 4: AI capability

Outputs:

- management summary
- trend forecasting
- defect and anomaly insights
- inventory suggestions
- report assistant using approved datasets only

### Phase 5: Handover

Outputs:

- deployment package
- source code package
- architecture and database documents
- API documents
- training sessions
- secondary development guide
- acceptance and support transition

## 9. Acceptance Criteria

The first production acceptance should require at least:

- CRM, ERP core, MES core, and quality core are available and integrated
- Order-to-production-to-inventory flow works end to end
- Traceability from batch/work order to material issue and inspection result is available
- Core cost accounting output is available
- Management dashboards and exportable reports are available
- System runs fully inside the client network
- Database is not exposed publicly
- Source code and deployment documents are delivered
- Client IT team can deploy the system in a test environment after training
- Client IT team can complete one report customization or one simple secondary development task

## 10. Source Code and Training Handover

### Code delivery scope

- frontend source code
- backend source code
- database scripts
- deployment scripts
- configuration samples
- test environment package
- production deployment package

### Documentation scope

- BRD/SRS
- architecture document
- module design document
- database design document
- integration document
- deployment manual
- operation manual
- admin manual
- API document
- secondary development guide
- report development guide

### Training scope

- system administration training
- key-user training
- deployment training
- developer handover training
- recorded sessions and handover checklist

## 11. Immediate Build Strategy

Do not start by coding all modules at once.

Build in this order:

1. Confirm phase-1 scope and legacy-system strategy
2. Freeze the core master data model
3. Scaffold the modular monolith backend and React frontend
4. Build shared platform and authentication first
5. Build sales order, procurement, inventory, and production order flows
6. Add MES execution and quality checkpoints
7. Add cost accounting and management reports
8. Add integration jobs and reconciliation
9. Add AI only after real business data is stable

## 12. Open Questions Before Scaffolding

These questions should be resolved before implementation starts:

- Is Kingdee staying for finance, or only temporarily?
- What exact solar products are produced, and what are the major production stages?
- How many workshops, lines, work centers, and warehouses exist?
- Is lot-level or serial-level traceability required?
- Are barcode printers and handheld scanners already in use?
- Which cost accounting outputs are mandatory for go-live?
- Which reports are mandatory for management in month 1?
- What approval processes must exist on day one?
- What historical data must be migrated?
- What is the client IT team's actual .NET and database support capability?

## 13. Recommended Positioning

Position this project as:

> A modular, source-code-deliverable manufacturing digital platform built on a mature .NET foundation, customized for the factory's business processes, launched in phases for fast go-live and long-term extensibility.

Do not position it as a fully custom system built from zero with full ERP, MES, finance, BI, AI, and migration all in one big-bang release.
