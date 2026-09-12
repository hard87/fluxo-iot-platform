import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { AlertRuleRevision } from "../../types";
import { ApiError } from "./httpClient";
import {
  ALERTS_PAGE_SIZE,
  createAlertRule,
  hasPossibleNextAlertsPage,
  listAlertRuleRevisions,
  listAlertRules,
  updateAlertRule
} from "./alertRulesService";

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

function alertRuleRevisionFixture(overrides: Partial<AlertRuleRevision> = {}): AlertRuleRevision {
  return {
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
    authorId: "user-1",
    ...overrides
  };
}

describe("alertRulesService", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("createAlertRule posts to /alerts/rules with the payload and token", async () => {
    const revision = alertRuleRevisionFixture();
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, revision));

    const payload = {
      name: "Temperatura alta",
      metricDefinitionId: "metric-1",
      operator: "GreaterThan" as const,
      threshold: 30,
      enabled: false
    };

    const result = await createAlertRule("token", "workspace-1", payload);

    expect(result).toEqual(revision);
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/rules");
    expect(init?.method).toBe("POST");
    expect((init?.headers as Record<string, string>).Authorization).toBe("Bearer token");
    expect(JSON.parse(init?.body as string)).toEqual(payload);
  });

  it("updateAlertRule puts to /alerts/rules/{ruleId} including expectedVersion", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, alertRuleRevisionFixture({ version: 2 })));

    const payload = {
      name: "Temperatura alta",
      metricDefinitionId: "metric-1",
      operator: "GreaterThan" as const,
      threshold: 32,
      enabled: true,
      expectedVersion: 1
    };

    await updateAlertRule("token", "workspace-1", "rule-1", payload);

    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/rules/rule-1");
    expect(init?.method).toBe("PUT");
    expect(JSON.parse(init?.body as string).expectedVersion).toBe(1);
  });

  it("listAlertRules builds the ?page= query string for the requested page", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, [alertRuleRevisionFixture()]));

    await listAlertRules("token", "workspace-1", 2);

    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/rules?page=2");
  });

  it("listAlertRuleRevisions builds the nested revisions route with ?page=", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, [alertRuleRevisionFixture()]));

    await listAlertRuleRevisions("token", "workspace-1", "rule-1", 1);

    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe("/api/workspaces/workspace-1/alerts/rules/rule-1/revisions?page=1");
  });

  it("forwards an AbortSignal through to fetch", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, alertRuleRevisionFixture()));
    const controller = new AbortController();

    await createAlertRule(
      "token",
      "workspace-1",
      { name: "x", metricDefinitionId: "m1", operator: "IsTrue", enabled: false },
      controller.signal
    );

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.signal).toBe(controller.signal);
  });

  it("hasPossibleNextAlertsPage is true only for a full page", () => {
    const fullPage = Array.from({ length: ALERTS_PAGE_SIZE }, () => alertRuleRevisionFixture());
    expect(hasPossibleNextAlertsPage(fullPage)).toBe(true);
    expect(hasPossibleNextAlertsPage(fullPage.slice(0, 3))).toBe(false);
    expect(hasPossibleNextAlertsPage([])).toBe(false);
  });

  it("maps a 409 version conflict to ApiError with status 409, without relying on detail text", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(409, { status: 409, title: "Conflict", detail: "Validation failed" })
    );

    const error = await updateAlertRule("token", "workspace-1", "rule-1", {
      name: "x",
      metricDefinitionId: "m1",
      operator: "IsTrue",
      enabled: true,
      expectedVersion: 1
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    // Intentionally not asserting on problem.detail content: production detail text is a generic
    // safe sentence, never the real conflict reason, so callers must branch on status alone.
  });

  it("maps a 400 validation error to ApiError with status 400", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(400, { status: 400, title: "Validation failed", detail: "Validation failed" })
    );

    const error = await createAlertRule("token", "workspace-1", {
      name: "",
      metricDefinitionId: "m1",
      operator: "IsTrue",
      enabled: false
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
  });

  it("maps a 404 not-found error to ApiError with status 404", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(404, { status: 404, title: "Resource not found", detail: "Resource not found." })
    );

    const error = await listAlertRuleRevisions("token", "workspace-1", "missing-rule", 1).catch(
      (e: unknown) => e
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
  });
});
