import type { PortalNotification } from "../../types";
import { apiRequest } from "./httpClient";

/** The calling user's own portal notifications across the workspace (not filtered by rule). */
export async function listMyNotifications(
  token: string,
  workspaceId: string,
  page: number,
  signal?: AbortSignal
): Promise<PortalNotification[]> {
  return await apiRequest<PortalNotification[]>(`/api/workspaces/${workspaceId}/alerts/notifications?page=${page}`, {
    token,
    signal
  });
}

/** Idempotent: marking an already-read notification again is a no-op. Only the recipient can
 * mark their own notification -- the server returns 404 for anyone else. */
export async function markNotificationRead(
  token: string,
  workspaceId: string,
  notificationId: string,
  signal?: AbortSignal
): Promise<void> {
  await apiRequest<void>(`/api/workspaces/${workspaceId}/alerts/notifications/${notificationId}/read`, {
    method: "POST",
    token,
    signal
  });
}
