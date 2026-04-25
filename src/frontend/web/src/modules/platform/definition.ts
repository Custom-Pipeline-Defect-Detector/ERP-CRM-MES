import type { ModuleDefinition } from '../../types/module-definition'

export const platformModule: ModuleDefinition = {
  key: 'platform',
  title: 'Platform',
  phase: 'Phase 1',
  summary: 'Identity, RBAC, password recovery, audit, attachments, and configuration foundations.',
  description:
    'Owns the administration spine for the entire intranet platform, including organizations, users, account recovery, department ownership, role assignment, permissions, logs, and system dictionaries.',
  capabilities: [
    'Organization and department management',
    'User department assignment, role assignment, expiring password reset, and lockout management',
    'Audit logs and operation logs',
    'Dictionary, configuration, and attachment services',
  ],
  placeholderEntities: ['User', 'Role', 'Permission', 'Department', 'AuditLog', 'Attachment', 'Notification'],
  integrationTouchpoints: [
    'Optional AD or LDAP integration',
    'Centralized logging and backup processes',
    'Shared attachment storage on local server or NAS',
  ],
  deliveryFocus: [
    'Authentication and authorization baseline',
    'Admin configuration pages',
    'Shared infrastructure for all business modules',
  ],
}
