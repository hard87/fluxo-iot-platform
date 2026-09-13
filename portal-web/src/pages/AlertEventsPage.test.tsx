import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as alertRulesService from "../services/api/alertRulesService";
import * as alertEventsService from "../services/api/alertEventsService";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertEvent, AlertRuleRevision, MetricDefinitionResponse } from "../types";
import { AlertEventsPage } from "./AlertEventsPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token", user: { userId: "user-1", email: "a@b.com" } }) }));

vi.mock("../services/api/alertEventsService", () => ({ listAlertEvents: vi.fn() }));

vi.mock("../services/api/alertRulesService", async () => {
  const actual = await vi.importActual<typeof import("../services/api/alertRulesService")>(
    "../services/api/alertRulesService"
  );
  return { ...actual, listAllAlertRules: vi.fn() };
});

vi.mock("../services/api/telemetryService", () => ({ listMetricDefinitions: vi.fn() }));

const listEventsMock = vi.mocked(alertEventsService.listAlertEvents);
const listAllRulesMock = vi.mocked(alertRulesService.listAllAlertRules);
const metricsMock = vi.mocked(telemetryService.listMetricDefinitions);

function alertEventFixture(overrides: Partial<AlertEvent> = {}): AlertEvent {
  return {
    id: "event-1",
    workspaceId: "workspace-1",
    ruleId: "rule-1",
    revisionId: "revision-1",
    deviceIdentifier: "sensor-1",
    triggeredAtUtc: "2026-09-12T12:00:00Z",
    status: "Firing",
    ordinal: 1,
    ...overrides
  };
}

function alertRuleFixture(overrides: Partial<AlertRuleRevision> = {}): AlertRuleRevision {
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
    hysteresis: 0,
    durationSeconds: 0,
    cooldownSeconds: 0,
    expectedIntervalSeconds: 300,
    severity: "Critical",
    enabled: true,
    activatedAtUtc: "2026-09-12T12:00:00Z",
    createdAtUtc: "2026-09-12T12:00:00Z",
    authorId: "user-1",
    ...overrides
  };
}

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

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts/events"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/events" element={<AlertEventsPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertEventsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listAllRulesMock.mockResolvedValue([alertRuleFixture()]);
    metricsMock.mockResolvedValue([metricDefinitionFixture()]);
  });

  it("lista eventos e resolve regra, métrica e severidade", async () => {
    listEventsMock.mockResolvedValue([alertEventFixture()]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(await within(table).findByText("Temperatura alta")).toBeInTheDocument();
    expect(within(table).getByText("Temperatura")).toBeInTheDocument();
    expect(within(table).getByText("Crítico")).toBeInTheDocument();
    expect(within(table).getByText("sensor-1")).toBeInTheDocument();
    expect(listEventsMock).toHaveBeenCalledWith("token", "workspace-1", 1);
  });

  it("expõe uma tabela acessível com cabeçalhos de coluna", async () => {
    listEventsMock.mockResolvedValue([alertEventFixture()]);
    renderPage();

    const table = await screen.findByRole("table");
    for (const header of ["Dispositivo", "Regra", "Métrica", "Severidade", "Estado", "Ocorrido em"]) {
      expect(within(table).getByRole("columnheader", { name: header })).toBeInTheDocument();
    }
  });

  it("filtra por estado sem novo fetch", async () => {
    const user = userEvent.setup();
    listEventsMock.mockResolvedValue([
      alertEventFixture({ id: "event-1", status: "Firing" }),
      alertEventFixture({ id: "event-2", status: "Resolved" })
    ]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Ativo")).toBeInTheDocument();
    expect(within(table).getByText("Resolvido")).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Estado"), "Resolved");

    expect(within(table).queryByText("Ativo")).not.toBeInTheDocument();
    expect(within(table).getByText("Resolvido")).toBeInTheDocument();
    expect(listEventsMock).toHaveBeenCalledTimes(1);
  });

  it("filtra por severidade usando a severidade da regra atual", async () => {
    const user = userEvent.setup();
    listEventsMock.mockResolvedValue([
      alertEventFixture({ id: "event-1", ruleId: "rule-1" }),
      alertEventFixture({ id: "event-2", ruleId: "rule-2" })
    ]);
    listAllRulesMock.mockResolvedValue([
      alertRuleFixture({ ruleId: "rule-1", severity: "Critical" }),
      alertRuleFixture({ ruleId: "rule-2", id: "rule-2", severity: "Info" })
    ]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(await within(table).findByText("Crítico")).toBeInTheDocument();
    expect(within(table).getByText("Informativo")).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Severidade"), "Critical");

    expect(within(table).queryByText("Informativo")).not.toBeInTheDocument();
    expect(within(table).getByText("Crítico")).toBeInTheDocument();
  });

  it("mostra o estado de carregamento antes da resposta", () => {
    listEventsMock.mockReturnValue(new Promise<AlertEvent[]>(() => {}));
    renderPage();

    expect(screen.getByText("Carregando eventos de alerta")).toBeInTheDocument();
  });

  it("apresenta o estado de erro com ação de repetir e refaz a chamada", async () => {
    const user = userEvent.setup();
    listEventsMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();

    expect(await screen.findByText("Não foi possível carregar os eventos de alerta")).toBeInTheDocument();

    listEventsMock.mockResolvedValueOnce([alertEventFixture()]);
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByText("Temperatura alta")).toBeInTheDocument();
  });

  it("apresenta estado vazio quando não há eventos", async () => {
    listEventsMock.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("Nenhum evento de alerta")).toBeInTheDocument();
  });

  it("pagina pelos controles Anterior/Próxima quando a página está cheia", async () => {
    const user = userEvent.setup();
    listEventsMock.mockResolvedValue(
      Array.from({ length: 100 }, (_, index) => alertEventFixture({ id: `event-${index}` }))
    );
    renderPage();

    await screen.findAllByText("Temperatura alta");
    expect(screen.getByRole("button", { name: "Próxima" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(listEventsMock).toHaveBeenLastCalledWith("token", "workspace-1", 2);
  });

  it("linka para o histórico do evento", async () => {
    listEventsMock.mockResolvedValue([alertEventFixture()]);
    renderPage();

    const link = await screen.findByRole("link", { name: "Ver histórico" });
    expect(link).toHaveAttribute("href", "/workspaces/workspace-1/alerts/events/event-1");
  });
});
