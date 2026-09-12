import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { AlertAttemptDiagnostic, AlertEvent, AlertHistory } from "../../types";
import { ApiError } from "./httpClient";
import { acknowledgeAlertEvent, getAlertHistory, listAlertDiagnostics, listAlertEvents } from "./alertEventsService";

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

function alertEventFixture(overrides: Partial<AlertEvent> = {}): AlertEvent {
  return {
    id: "event-1",
    workspaceId: "workspace-1",
    ruleId: "rule-1",
    revisionId: "revision-1",
    deviceIdentifier: "device-1",
    triggeredAtUtc: "2026-09-12T12:00:00Z",
    status: "Firing",
    ordinal: 1,
    ...overrides
  };
}

describe("alertEventsService", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("listAlertEvents builds the ?page= query string and returns items in the order received", async () => {
    const events = [alertEventFixture({ id: "event-2" }), alertEventFixture({ id: "event-1" })];
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, events));

    const result = await listAlertEvents("token", "workspace-1", 1);

    expect(result).toEqual(events);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/events?page=1");
  });

  it("getAlertHistory fetches /alerts/events/{eventId} and tolerates an empty deliveryIntents array", async () => {
    const history: AlertHistory = {
      event: alertEventFixture(),
      revision: {
        id: "rule-1",
        workspaceId: "workspace-1",
        ruleId: "rule-1",
        version: 1,
        name: "Temperatura alta",
        metricDefinitionId: "metric-1",
        deviceIdentifier: null,
        valueType: "Numeric",
        unit: "°C",
        operator: "GreaterThan",
        threshold: 30,
        thresholdHigh: null,
        hysteresis: 0.5,
        durationSeconds: 60,
        cooldownSeconds: 300,
        expectedIntervalSeconds: 300,
        severity: "Warning",
        enabled: true,
        activatedAtUtc: "2026-09-12T12:00:00Z",
        createdAtUtc: "2026-09-12T12:00:00Z",
        authorId: "user-1"
      },
      transitions: [],
      acknowledgements: [],
      deliveryIntents: []
    };
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, history));

    const result = await getAlertHistory("token", "workspace-1", "event-1");

    expect(result).toEqual(history);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/events/event-1");
  });

  it("acknowledgeAlertEvent posts with no body and no Content-Type header", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(200, {
        id: "ack-1",
        workspaceId: "workspace-1",
        eventId: "event-1",
        authorId: "user-1",
        createdAtUtc: "2026-09-12T12:00:00Z"
      })
    );

    await acknowledgeAlertEvent("token", "workspace-1", "event-1");

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/events/event-1/acknowledgements");
    expect(init?.method).toBe("POST");
    expect(init?.body).toBeUndefined();
    expect((init?.headers as Record<string, string>)["Content-Type"]).toBeUndefined();
  });

  it("acknowledgeAlertEvent maps a 404 to ApiError with status 404", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(404, { status: 404, title: "Resource not found", detail: "Resource not found." })
    );

    const error = await acknowledgeAlertEvent("token", "workspace-1", "missing-event").catch(
      (e: unknown) => e
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });

  it("listAlertDiagnostics builds ?page= and only ever types Failed/DeadLetter statuses", async () => {
    const diagnostics: AlertAttemptDiagnostic[] = [
      {
        id: "attempt-1",
        workItemId: "work-1",
        ruleId: "rule-1",
        deviceIdentifier: "device-1",
        status: "Failed",
        attemptCount: 2,
        nextAttemptAtUtc: "2026-09-12T12:05:00Z",
        reason: "Timeout"
      },
      {
        id: "attempt-2",
        workItemId: "work-2",
        ruleId: "rule-1",
        deviceIdentifier: "device-2",
        status: "DeadLetter",
        attemptCount: 5,
        nextAttemptAtUtc: null,
        reason: "Persistent failure"
      }
    ];
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, diagnostics));

    const result = await listAlertDiagnostics("token", "workspace-1", 1);

    expect(result).toEqual(diagnostics);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/diagnostics?page=1");
  });

  it("listAlertDiagnostics maps a 403 (non-Admin caller) to ApiError with status 403", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(403, { status: 403, title: "Forbidden", detail: "Forbidden" })
    );

    const error = await listAlertDiagnostics("token", "workspace-1", 1).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(403);
  });
});
