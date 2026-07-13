import type { Workspace } from "../../types";
import { apiRequest } from "./httpClient";

export interface CreateWorkspacePayload {
  name: string;
  tenantId?: string;
}

export async function listWorkspaces(token: string): Promise<Workspace[]> {
  return await apiRequest<Workspace[]>("/api/workspaces", { token });
}

export async function createWorkspace(token: string, payload: CreateWorkspacePayload): Promise<Workspace> {
  return await apiRequest<Workspace>("/api/workspaces", {
    method: "POST",
    token,
    body: payload
  });
}
