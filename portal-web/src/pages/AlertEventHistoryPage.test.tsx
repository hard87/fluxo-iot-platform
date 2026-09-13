import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as alertEventsService from "../services/api/alertEventsService";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertHistory, MetricDefinitionResponse } from "../types";
import { AlertEventHistoryPage } from "./AlertEventHistoryPage";

vi.mock("../hooks/useAuth", () => ({
  useAuth: () => ({ token: "token", user: { userId: "user-1", email: "a@b.com" } })
}));

vi.mock("../services/api/alertEventsService", () => ({
  getAlertHistory: vi.fn(),
  acknowledgeAlertEvent: vi.fn()
}));

vi.mock("../services/api/telemetryService", () => ({ listMetricDefinitions: vi.fn() }));

const getHistoryMock = vi.mocked(alertEventsService.getAlertHistory);
const acknowledgeMock = vi.mocked(alertEventsService.acknowledgeAlertEvent);
const metricsMock = vi.mocked(telemetryService.listMetricDefinitions);

function metricDefinitionFixture(overrides: Partial<MetricDefinitionResponse> = {}): MetricDefinitionResponse {
  return {
    id: "metric-1",
    metricKey: "temperature_c",
    displayName: "Temperatura",
    valueType: "Numeric",
    semanticType: null,
    canonicalUnit: "°C",
    status: "Discovered",
    isQueryable: true,
    ...overrides
  };
}

function historyFixture(overrides: Partial<AlertHistory> = {}): AlertHistory {
  return {
    event: {
      id: "event-1",
      workspaceId: "workspace-1",
      ruleId: "rule-1",
      revisionId: "revision-1",
      deviceIdentifier: "sensor-1",
      triggeredAtUtc: "2026-09-12T12:00:00Z",
      status: "Firing",
      ordinal: 1
    },
    revision: {
      id: "revision-1",
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
      hysteresis: 0,
      durationSeconds: 0,
      cooldownSeconds: 0,
      expectedIntervalSeconds: 300,
      severity: "Critical",
      enabled: true,
      activatedAtUtc: "2026-09-12T12:00:00Z",
      createdAtUtc: "2026-09-12T12:00:00Z",
      authorId: "user-1"
    },
    transitions: [
      {
        id: "transition-1",
        workspaceId: "workspace-1",
        eventId: "event-1",
        ordinal: 1,
        kind: "Firing",
        occurredAtUtc: "2026-09-12T12:00:00Z",
        recordedAtUtc: "2026-09-12T12:00:05Z",
        receivedAtUtc: "2026-09-12T12:00:00Z",
        ingestionRecordId: "ingestion-1",
        numericValue: 35,
        booleanValue: null,
        reason: "ConditionSatisfied",
        evaluatorVersion: "alerts-v1"
      }
    ],
    acknowledgements: [],
    deliveryIntents: [],
    ...overrides
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts/events/event-1"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/events/:eventId" element={<AlertEventHistoryPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertEventHistoryPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    metricsMock.mockResolvedValue([metricDefinitionFixture()]);
  });

  it("mostra o resumo do evento com métrica resolvida e badges de severidade/estado", async () => {
    getHistoryMock.mockResolvedValue(historyFixture());
    renderPage();

    expect(await screen.findByRole("heading", { name: "Temperatura alta" })).toBeInTheDocument();
    expect(screen.getByText(/Dispositivo sensor-1/)).toBeInTheDocument();
    expect(screen.getByText(/Métrica Temperatura/)).toBeInTheDocument();
    expect(screen.getByText("Crítico")).toBeInTheDocument();
    expect(screen.getAllByText("Ativo").length).toBeGreaterThanOrEqual(1);
  });

  it("lista as transições com valor e motivo traduzido", async () => {
    getHistoryMock.mockResolvedValue(historyFixture());
    renderPage();

    await screen.findByRole("table");
    expect(screen.getByText("35")).toBeInTheDocument();
    expect(screen.getByText("Condição satisfeita")).toBeInTheDocument();
  });

  it("mostra 'Sem dado de ingestão' quando receivedAtUtc é nulo, em vez de um atraso zero", async () => {
    getHistoryMock.mockResolvedValue(
      historyFixture({
        transitions: [
          {
            id: "transition-1",
            workspaceId: "workspace-1",
            eventId: "event-1",
            ordinal: 1,
            kind: "Firing",
            occurredAtUtc: "2026-09-12T12:00:00Z",
            recordedAtUtc: "2026-09-12T12:00:00Z",
            receivedAtUtc: null,
            ingestionRecordId: null,
            numericValue: 35,
            booleanValue: null,
            reason: "Historical",
            evaluatorVersion: "alerts-v1"
          }
        ]
      })
    );
    renderPage();

    expect(await screen.findByText("Sem dado de ingestão")).toBeInTheDocument();
  });

  it("reconhece o evento e mostra o reconhecimento com autoria própria", async () => {
    const user = userEvent.setup();
    getHistoryMock.mockResolvedValue(historyFixture());
    acknowledgeMock.mockResolvedValue({
      id: "ack-1",
      workspaceId: "workspace-1",
      eventId: "event-1",
      authorId: "user-1",
      createdAtUtc: "2026-09-12T12:05:00Z"
    });
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Reconhecer evento" }));

    expect(acknowledgeMock).toHaveBeenCalledWith("token", "workspace-1", "event-1");
    expect(await screen.findByText(/Você em/)).toBeInTheDocument();
    expect(screen.getByText("Você já reconheceu este evento.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reconhecer evento" })).not.toBeInTheDocument();
  });

  it("nao mostra o botao de reconhecer quando o usuario atual ja reconheceu ao carregar", async () => {
    getHistoryMock.mockResolvedValue(
      historyFixture({
        acknowledgements: [
          { id: "ack-1", workspaceId: "workspace-1", eventId: "event-1", authorId: "user-1", createdAtUtc: "2026-09-12T12:05:00Z" }
        ]
      })
    );
    renderPage();

    expect(await screen.findByText("Você já reconheceu este evento.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reconhecer evento" })).not.toBeInTheDocument();
  });

  it("mostra erro seguro quando o reconhecimento falha, sem travar a página", async () => {
    const user = userEvent.setup();
    getHistoryMock.mockResolvedValue(historyFixture());
    acknowledgeMock.mockRejectedValue(new Error("falhou"));
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Reconhecer evento" }));

    expect(await screen.findByText("Não foi possível reconhecer o evento")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reconhecer evento" })).toBeInTheDocument();
  });

  it("apresenta o estado de erro com ação de repetir e refaz a chamada", async () => {
    const user = userEvent.setup();
    getHistoryMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();

    expect(await screen.findByText("Não foi possível carregar o histórico")).toBeInTheDocument();

    getHistoryMock.mockResolvedValueOnce(historyFixture());
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByRole("heading", { name: "Temperatura alta" })).toBeInTheDocument();
  });
});
