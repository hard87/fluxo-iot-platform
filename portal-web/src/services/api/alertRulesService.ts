import type { AlertOperator, AlertRuleRevision, AlertSeverity } from "../../types";
import { apiRequest } from "./httpClient";

/** Server-fixed page size for every alerts list endpoint; there is no client-configurable pageSize. */
export const ALERTS_PAGE_SIZE = 100;

/** A full page (length === ALERTS_PAGE_SIZE) means there may be a next page; a partial page is the last one. */
export function hasPossibleNextAlertsPage<T>(items: T[]): boolean {
  return items.length === ALERTS_PAGE_SIZE;
}

/**
 * Boolean metrics: operator must be IsTrue/IsFalse; threshold/thresholdHigh must be omitted; hysteresis must be 0.
 * Numeric metrics: operator is one of GreaterThan/GreaterOrEqual/LessThan/LessOrEqual/InsideRange/OutsideRange;
 *   threshold is required; thresholdHigh is required only for InsideRange/OutsideRange.
 * Create (POST /rules): enabled must be false (server returns 400 otherwise).
 * Update (PUT /rules/{id}): expectedVersion is required and must equal the rule's current version, else 409.
 * The server is authoritative for all of the above; this is documentation only, not enforced client-side here.
 */
export interface SaveAlertRulePayload {
  name: string;
  metricDefinitionId: string;
  deviceIdentifier?: string | null;
  operator: AlertOperator;
  threshold?: number | null;
  thresholdHigh?: number | null;
  hysteresis?: number;
  durationSeconds?: number;
  cooldownSeconds?: number;
  severity?: AlertSeverity;
  enabled?: boolean;
  expectedVersion?: number | null;
}

export async function createAlertRule(
  token: string,
  workspaceId: string,
  payload: SaveAlertRulePayload,
  signal?: AbortSignal
): Promise<AlertRuleRevision> {
  return await apiRequest<AlertRuleRevision>(`/api/workspaces/${workspaceId}/alerts/rules`, {
    method: "POST",
    token,
    body: payload,
    signal
  });
}

export async function updateAlertRule(
  token: string,
  workspaceId: string,
  ruleId: string,
  payload: SaveAlertRulePayload,
  signal?: AbortSignal
): Promise<AlertRuleRevision> {
  return await apiRequest<AlertRuleRevision>(`/api/workspaces/${workspaceId}/alerts/rules/${ruleId}`, {
    method: "PUT",
    token,
    body: payload,
    signal
  });
}

export async function listAlertRules(
  token: string,
  workspaceId: string,
  page: number,
  signal?: AbortSignal
): Promise<AlertRuleRevision[]> {
  return await apiRequest<AlertRuleRevision[]>(`/api/workspaces/${workspaceId}/alerts/rules?page=${page}`, {
    token,
    signal
  });
}

export async function listAlertRuleRevisions(
  token: string,
  workspaceId: string,
  ruleId: string,
  page: number,
  signal?: AbortSignal
): Promise<AlertRuleRevision[]> {
  return await apiRequest<AlertRuleRevision[]>(
    `/api/workspaces/${workspaceId}/alerts/rules/${ruleId}/revisions?page=${page}`,
    { token, signal }
  );
}
