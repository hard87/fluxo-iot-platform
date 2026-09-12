import type { AlertAcknowledgement, AlertAttemptDiagnostic, AlertEvent, AlertHistory } from "../../types";
import { apiRequest } from "./httpClient";

export async function listAlertEvents(
  token: string,
  workspaceId: string,
  page: number,
  signal?: AbortSignal
): Promise<AlertEvent[]> {
  return await apiRequest<AlertEvent[]>(`/api/workspaces/${workspaceId}/alerts/events?page=${page}`, {
    token,
    signal
  });
}

export async function getAlertHistory(
  token: string,
  workspaceId: string,
  eventId: string,
  signal?: AbortSignal
): Promise<AlertHistory> {
  return await apiRequest<AlertHistory>(`/api/workspaces/${workspaceId}/alerts/events/${eventId}`, {
    token,
    signal
  });
}

/** Idempotent: repeating the call for the same event and user returns the existing acknowledgement. */
export async function acknowledgeAlertEvent(
  token: string,
  workspaceId: string,
  eventId: string,
  signal?: AbortSignal
): Promise<AlertAcknowledgement> {
  return await apiRequest<AlertAcknowledgement>(
    `/api/workspaces/${workspaceId}/alerts/events/${eventId}/acknowledgements`,
    { method: "POST", token, signal }
  );
}

/** Requires the Admin role (not Viewer). Only returns attempts with status Failed or DeadLetter. */
export async function listAlertDiagnostics(
  token: string,
  workspaceId: string,
  page: number,
  signal?: AbortSignal
): Promise<AlertAttemptDiagnostic[]> {
  return await apiRequest<AlertAttemptDiagnostic[]>(
    `/api/workspaces/${workspaceId}/alerts/diagnostics?page=${page}`,
    { token, signal }
  );
}
