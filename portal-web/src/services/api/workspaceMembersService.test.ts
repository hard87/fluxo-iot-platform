import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { WorkspaceMember } from "../../types";
import { ApiError } from "./httpClient";
import { listWorkspaceMembers } from "./workspaceMembersService";

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

describe("workspaceMembersService", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("listWorkspaceMembers fetches /workspaces/{id}/members and returns items in the order received", async () => {
    const members: WorkspaceMember[] = [
      { userId: "user-1", email: "owner@example.test", role: "Owner" },
      { userId: "user-2", email: "viewer@example.test", role: "Viewer" }
    ];
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, members));

    const result = await listWorkspaceMembers("token", "workspace-1");

    expect(result).toEqual(members);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/members");
  });

  it("listWorkspaceMembers maps a 404 (caller not a member) to ApiError with status 404", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(404, { status: 404, title: "Workspace not found", detail: "Workspace not found." })
    );

    const error = await listWorkspaceMembers("token", "workspace-1").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
