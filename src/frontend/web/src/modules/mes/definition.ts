import type { ModuleDefinition } from '../../types/module-definition'

export const mesModule: ModuleDefinition = {
  key: 'mes',
  title: 'MES',
  phase: 'Phase 1',
  summary: 'Workshop execution, material issue, dispatching, and batch traceability.',
  description:
    'Controls production orders on the shop floor, connects dispatching and reporting, and captures the material and batch events required for traceability.',
  capabilities: [
    'Production order intake and work order scheduling',
    'Dispatching and completion reporting',
    'Material issue and route-step execution',
    'Batch tracking, downtime, and shift management',
  ],
  placeholderEntities: ['ProductionOrder', 'WorkOrder', 'ProcessRoute', 'ProcessStep', 'WorkCenter', 'Machine', 'JobDispatch', 'MaterialIssue', 'ProductionReport', 'DowntimeRecord', 'TraceRecord'],
  integrationTouchpoints: [
    'Demand input from CRM and ERP',
    'Material issue feedback into inventory and cost',
    'Traceability and event feeds into Quality and Reporting',
  ],
  deliveryFocus: [
    'Order-to-completion traceability',
    'Workshop operator usability',
    'Near-real-time production visibility',
  ],
}
