import type { WorkspaceMember } from "../../types";
import { apiRequest } from "./httpClient";

export async function listWorkspaceMembers(
  token: string,
  workspaceId: string,
  signal?: AbortSignal
): Promise<WorkspaceMember[]> {
  return await apiRequest<WorkspaceMember[]>(`/api/workspaces/${workspaceId}/members`, { token, signal });
}
