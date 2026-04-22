import type { ModuleDefinition } from '../../types/module-definition'

export const integrationModule: ModuleDefinition = {
  key: 'integration',
  title: 'Integration',
  phase: 'Phase 1',
  summary: 'Kingdee coexistence, legacy-system adapters, and reconciliation workflows.',
  description:
    'Isolates all external-system synchronization logic so the platform can start with coexistence and move toward gradual replacement without corrupting core domains.',
  capabilities: [
    'Legacy adapter boundaries',
    'Master-data and transaction synchronization',
    'Retry queues and sync-failure logging',
    'Daily reconciliation and migration support',
  ],
  placeholderEntities: ['ExternalSystem', 'SyncJob', 'SyncMapping', 'SyncFailure', 'ReconciliationSnapshot'],
  integrationTouchpoints: [
    'Kingdee customer, inventory, and accounting sync',
    'Legacy factory-system migration path',
    'Reconciliation reports for orders, stock, and cost',
  ],
  deliveryFocus: [
    'Controlled coexistence strategy',
    'Observable sync behavior',
    'Migration without big-bang replacement',
  ],
}
