import type { ModuleDefinition } from '../../types/module-definition'

export const qualityModule: ModuleDefinition = {
  key: 'quality',
  title: 'Quality',
  phase: 'Phase 2',
  summary: 'Incoming, in-process, and final inspection with defect handling.',
  description:
    'Applies inspection rules to received materials and shop-floor output, records defects and dispositions, and supports NCR and CAPA workflows.',
  capabilities: [
    'Incoming, process, and final inspections',
    'Defect recording and classification',
    'Rework, reject, NCR, and CAPA handling',
    'Quality statistics and traceability support',
  ],
  placeholderEntities: ['QualityStandard', 'InspectionPlan', 'InspectionRecord', 'DefectRecord', 'Ncr', 'Capa'],
  integrationTouchpoints: [
    'Inspection points linked to MES process steps',
    'Incoming results affect ERP stock availability',
    'Defect trends feed Reporting and AI',
  ],
  deliveryFocus: [
    'Inspection standardization',
    'Disposition traceability',
    'Closed-loop defect management',
  ],
}
