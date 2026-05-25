/**
 * Mock report executions for development preview mode.
 * This data is ONLY used when the backend API is unavailable in development.
 * It is NEVER used in production.
 */

import type { ReportExecution } from './report-executions-api'

/**
 * Returns mock report executions covering all important UI states.
 */
export function getMockReportExecutions(): ReportExecution[] {
  const now = new Date()

  return [
    // Completed + artifactAvailable = true + storageMode Local
    {
      executionId: 'exec-001-completed-local',
      reportId: 'rpt-sales-weekly',
      reportName: 'Weekly Sales Summary',
      templateId: 'tmpl-sales-001',
      status: 'Completed',
      rowCount: 1247,
      artifactKey: 'artifacts/sales/weekly-2024-01-15.xlsx',
      artifactAvailable: true,
      storageMode: 'Local',
      createdAtUtc: new Date(now.getTime() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
      completedAtUtc: new Date(now.getTime() - 2 * 60 * 60 * 1000 + 45000).toISOString(),
      durationMs: 45000,
      queryFingerprint: 'qf-abc123def456',
      semanticModelVersion: 'v2.3.1',
      compiledSql: `SELECT 
  date_trunc('week', order_date) AS week,
  SUM(total_amount) AS revenue,
  COUNT(DISTINCT customer_id) AS unique_customers,
  COUNT(*) AS order_count
FROM orders
WHERE order_date >= '2024-01-01'
GROUP BY 1
ORDER BY 1 DESC;`,
    },
    // Completed + artifactAvailable = true + storageMode S3
    {
      executionId: 'exec-002-completed-s3',
      reportId: 'rpt-inventory-daily',
      reportName: 'Daily Inventory Report',
      templateId: 'tmpl-inventory-001',
      status: 'Completed',
      rowCount: 5832,
      artifactKey: 's3://reports-bucket/inventory/daily-2024-01-15.pdf',
      artifactAvailable: true,
      storageMode: 'S3',
      createdAtUtc: new Date(now.getTime() - 4 * 60 * 60 * 1000).toISOString(), // 4 hours ago
      completedAtUtc: new Date(now.getTime() - 4 * 60 * 60 * 1000 + 120000).toISOString(),
      durationMs: 120000,
      queryFingerprint: 'qf-xyz789ghi012',
      semanticModelVersion: 'v2.3.1',
      compiledSql: `SELECT 
  product_id,
  product_name,
  warehouse_id,
  quantity_on_hand,
  reorder_point,
  CASE WHEN quantity_on_hand < reorder_point THEN 'Low Stock' ELSE 'OK' END AS stock_status
FROM inventory
JOIN products USING (product_id)
ORDER BY stock_status DESC, quantity_on_hand ASC;`,
    },
    // Processing
    {
      executionId: 'exec-003-processing',
      reportId: 'rpt-customer-analysis',
      reportName: 'Customer Segmentation Analysis',
      templateId: 'tmpl-customer-001',
      status: 'Processing',
      artifactAvailable: false,
      storageMode: 'S3',
      createdAtUtc: new Date(now.getTime() - 5 * 60 * 1000).toISOString(), // 5 minutes ago
      queryFingerprint: 'qf-proc001',
      semanticModelVersion: 'v2.3.1',
    },
    // Requested
    {
      executionId: 'exec-004-requested',
      reportId: 'rpt-financial-quarterly',
      reportName: 'Q4 Financial Summary',
      templateId: 'tmpl-finance-001',
      status: 'Requested',
      artifactAvailable: false,
      storageMode: 'Local',
      createdAtUtc: new Date(now.getTime() - 30 * 1000).toISOString(), // 30 seconds ago
      queryFingerprint: 'qf-req001',
      semanticModelVersion: 'v2.3.1',
    },
    // Failed with errorMessage
    {
      executionId: 'exec-005-failed',
      reportId: 'rpt-marketing-campaign',
      reportName: 'Marketing Campaign Performance',
      templateId: 'tmpl-marketing-001',
      status: 'Failed',
      artifactAvailable: false,
      storageMode: 'S3',
      createdAtUtc: new Date(now.getTime() - 1 * 60 * 60 * 1000).toISOString(), // 1 hour ago
      completedAtUtc: new Date(now.getTime() - 1 * 60 * 60 * 1000 + 15000).toISOString(),
      durationMs: 15000,
      errorMessage: 'Query execution timeout: The query exceeded the maximum execution time of 300 seconds. Consider adding filters or reducing the date range.',
      queryFingerprint: 'qf-fail001',
      semanticModelVersion: 'v2.3.1',
    },
    // ArtifactMissing
    {
      executionId: 'exec-006-artifact-missing',
      reportId: 'rpt-hr-headcount',
      reportName: 'Monthly Headcount Report',
      templateId: 'tmpl-hr-001',
      status: 'ArtifactMissing',
      rowCount: 342,
      artifactKey: 'artifacts/hr/headcount-2024-01-01.xlsx',
      artifactAvailable: false,
      storageMode: 'Local',
      createdAtUtc: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
      completedAtUtc: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000 + 30000).toISOString(),
      durationMs: 30000,
      queryFingerprint: 'qf-art001',
      semanticModelVersion: 'v2.2.0',
    },
    // Expired
    {
      executionId: 'exec-007-expired',
      reportId: 'rpt-operations-weekly',
      reportName: 'Operations Weekly Digest',
      templateId: 'tmpl-ops-001',
      status: 'Expired',
      rowCount: 891,
      artifactKey: 's3://reports-bucket/ops/weekly-2023-12-01.pdf',
      artifactAvailable: false,
      storageMode: 'S3',
      createdAtUtc: new Date(now.getTime() - 45 * 24 * 60 * 60 * 1000).toISOString(), // 45 days ago
      completedAtUtc: new Date(now.getTime() - 45 * 24 * 60 * 60 * 1000 + 60000).toISOString(),
      durationMs: 60000,
      queryFingerprint: 'qf-exp001',
      semanticModelVersion: 'v2.1.0',
    },
    // Completed but artifactAvailable = false
    {
      executionId: 'exec-008-completed-no-artifact',
      reportId: 'rpt-support-tickets',
      reportName: 'Support Ticket Analysis',
      templateId: 'tmpl-support-001',
      status: 'Completed',
      rowCount: 2156,
      artifactKey: 'artifacts/support/tickets-2024-01-10.csv',
      artifactAvailable: false,
      storageMode: 'Local',
      createdAtUtc: new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000).toISOString(), // 5 days ago
      completedAtUtc: new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000 + 25000).toISOString(),
      durationMs: 25000,
      queryFingerprint: 'qf-noart001',
      semanticModelVersion: 'v2.3.0',
      compiledSql: `SELECT 
  ticket_id,
  created_at,
  resolved_at,
  category,
  priority,
  EXTRACT(EPOCH FROM (resolved_at - created_at))/3600 AS resolution_hours
FROM support_tickets
WHERE created_at >= '2024-01-01'
ORDER BY created_at DESC;`,
    },
  ]
}

/**
 * Returns a single mock execution by ID for detail view.
 */
export function getMockReportExecution(executionId: string): ReportExecution | null {
  const executions = getMockReportExecutions()
  return executions.find((e) => e.executionId === executionId) ?? null
}