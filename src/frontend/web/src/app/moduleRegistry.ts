import { aiModule } from '../modules/ai/definition'
import { crmModule } from '../modules/crm/definition'
import { erpModule } from '../modules/erp/definition'
import { integrationModule } from '../modules/integration/definition'
import { mesModule } from '../modules/mes/definition'
import { platformModule } from '../modules/platform/definition'
import { qualityModule } from '../modules/quality/definition'
import { reportingModule } from '../modules/reporting/definition'
import type { ModuleDefinition } from '../types/module-definition'

export const moduleRegistry: ModuleDefinition[] = [
  platformModule,
  crmModule,
  erpModule,
  mesModule,
  qualityModule,
  reportingModule,
  aiModule,
  integrationModule,
]

export function getModuleDefinition(moduleKey: string | undefined) {
  return moduleRegistry.find((module) => module.key === moduleKey)
}
