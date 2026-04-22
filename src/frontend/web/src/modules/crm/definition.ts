import type { ModuleDefinition } from '../../types/module-definition'

export const crmModule: ModuleDefinition = {
  key: 'crm',
  title: 'CRM',
  phase: 'Phase 1',
  summary: 'Customer lifecycle, quotations, contracts, and sales-order handoff.',
  description:
    'Tracks prospects through signed orders and after-sales tickets, then hands approved business documents into ERP and MES planning.',
  capabilities: [
    'Customer and contact management',
    'Lead, opportunity, and quotation workflows',
    'Contract and follow-up records',
    'After-sales service and complaint capture',
  ],
  placeholderEntities: ['Customer', 'CustomerContact', 'Opportunity', 'Quote', 'SalesOrder', 'Contract', 'FollowUpRecord', 'AfterSalesTicket'],
  integrationTouchpoints: [
    'Customer master sync with Kingdee if retained',
    'Sales order handoff into ERP fulfillment',
    'Demand signal into MES planning and reporting',
  ],
  deliveryFocus: [
    'Clean customer master data',
    'Lead-to-order traceability',
    'Sales pipeline visibility for management',
  ],
}
