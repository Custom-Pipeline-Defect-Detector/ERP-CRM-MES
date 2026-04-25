import type { ModuleDefinition } from '../../types/module-definition'

export const reportingModule: ModuleDefinition = {
  key: 'reporting',
  title: 'Reporting',
  phase: 'Phase 1',
  summary: 'Dashboards, governed report previews, CSV exports, and extensible definitions.',
  description:
    'Presents management views across sales, inventory, production, quality, cost, and integration while supporting governed previews today and broader report customization later.',
  capabilities: [
    'Management dashboards across core operations',
    'Governed report previews across CRM, ERP, MES, Quality, and Integration',
    'CSV export jobs with persisted output',
    'Scheduled report generation foundation',
    'Custom filter and report-definition support',
  ],
  placeholderEntities: ['DashboardDefinition', 'ReportDefinition', 'ReportSnapshot', 'ExportJob'],
  integrationTouchpoints: [
    'Consumes curated data from all business modules',
    'Supports department-level report access',
    'Provides the approved dataset surface for AI',
  ],
  deliveryFocus: [
    'Management visibility from month 1',
    'Operational report exports',
    'Foundation for custom report development',
  ],
}
