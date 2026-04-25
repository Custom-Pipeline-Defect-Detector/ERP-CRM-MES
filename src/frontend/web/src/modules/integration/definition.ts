import type { ModuleDefinition } from '../../types/module-definition'

export const integrationModule: ModuleDefinition = {
  key: 'integration',
  title: 'Integration',
  phase: 'Phase 1',
  summary: 'Kingdee coexistence, accounting export batches, adapter jobs, and reconciliation workflows.',
  description:
    'Isolates all external-system synchronization logic so the platform can start with coexistence, batch finance exports cleanly, and move toward gradual replacement without corrupting core domains.',
  capabilities: [
    'Accounting export batches over approved bridge entries',
    'Kingdee adapter job dispatch, retry, and reconciliation tracking',
    'Master-data and transaction synchronization boundaries',
    'Retry queues, failure logging, and reconciliation support',
  ],
  placeholderEntities: ['ExternalSystem', 'AccountingExportBatch', 'IntegrationJob', 'SyncMapping', 'ReconciliationSnapshot'],
  integrationTouchpoints: [
    'Kingdee accounting export batches and acknowledgement flow',
    'Kingdee customer, inventory, and accounting sync',
    'Legacy factory-system migration path',
    'Reconciliation reports for orders, stock, and cost',
  ],
  deliveryFocus: [
    'Controlled coexistence strategy',
    'Observable export and sync behavior',
    'Migration without big-bang replacement',
  ],
}
