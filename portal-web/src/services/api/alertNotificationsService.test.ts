import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { PortalNotification } from "../../types";
import { ApiError } from "./httpClient";
import { listMyNotifications, markNotificationRead } from "./alertNotificationsService";

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

function notificationFixture(overrides: Partial<PortalNotification> = {}): PortalNotification {
  return {
    id: "notification-1",
    eventId: "event-1",
    ruleId: "rule-1",
    ruleName: "Temperatura alta",
    deviceIdentifier: "device-1",
    eventStatus: "Firing",
    transitionKind: "Firing",
    createdAtUtc: "2026-09-13T12:00:00Z",
    readAtUtc: null,
    ...overrides
  };
}

describe("alertNotificationsService", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("listMyNotifications builds the ?page= query string and returns items in the order received", async () => {
    const notifications = [notificationFixture({ id: "notification-2" }), notificationFixture({ id: "notification-1" })];
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, notifications));

    const result = await listMyNotifications("token", "workspace-1", 1);

    expect(result).toEqual(notifications);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/notifications?page=1");
  });

  it("markNotificationRead posts to the read route with no body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(204, undefined));

    await markNotificationRead("token", "workspace-1", "notification-1");

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/notifications/notification-1/read");
    expect(init?.method).toBe("POST");
    expect(init?.body).toBeUndefined();
  });

  it("markNotificationRead maps a 404 (not the recipient) to ApiError with status 404", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(404, { status: 404, title: "Resource not found", detail: "Resource not found." })
    );

    const error = await markNotificationRead("token", "workspace-1", "notification-1").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
