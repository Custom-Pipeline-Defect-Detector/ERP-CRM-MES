import type { ModuleDefinition } from '../../types/module-definition'

export const aiModule: ModuleDefinition = {
  key: 'ai',
  title: 'AI',
  phase: 'Phase 4',
  summary: 'Summaries, forecasting, anomaly warnings, and report Q&A over curated data.',
  description:
    'Adds decision-support services only after the core transactional and reporting datasets are stable, governed, and auditable.',
  capabilities: [
    'Management summary generation',
    'Production anomaly detection',
    'Trend forecasting and inventory suggestions',
    'Natural-language report assistant',
  ],
  placeholderEntities: ['InsightPrompt', 'InsightResult', 'ForecastScenario', 'AnomalyAlert'],
  integrationTouchpoints: [
    'Reads approved reporting datasets only',
    'Supports local-model or adapter-based inference',
    'Feeds explanations back into dashboards and reports',
  ],
  deliveryFocus: [
    'Controlled AI scope',
    'Read-only analytics posture',
    'Operationally auditable insights',
  ],
}
