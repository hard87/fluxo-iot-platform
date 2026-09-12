import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as alertRulesService from "../services/api/alertRulesService";
import { ApiError } from "../services/api/httpClient";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertRuleRevision, MetricDefinitionResponse } from "../types";
import { AlertRulesPage } from "./AlertRulesPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));

vi.mock("../services/api/alertRulesService", async () => {
  const actual = await vi.importActual<typeof import("../services/api/alertRulesService")>(
    "../services/api/alertRulesService"
  );
  return {
    ...actual,
    listAlertRules: vi.fn(),
    updateAlertRule: vi.fn()
  };
});

vi.mock("../services/api/telemetryService", () => ({
  listMetricDefinitions: vi.fn()
}));

const listMock = vi.mocked(alertRulesService.listAlertRules);
const updateMock = vi.mocked(alertRulesService.updateAlertRule);
const metricsMock = vi.mocked(telemetryService.listMetricDefinitions);

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

function metricDefinitionFixture(overrides: Partial<MetricDefinitionResponse> = {}): MetricDefinitionResponse {
  return {
    id: "metric-1",
    metricKey: "temperature_c",
    displayName: "Temperatura",
    valueType: "Numeric",
    semanticType: null,
    canonicalUnit: "°C",
    status: "Active",
    isQueryable: true,
    ...overrides
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts" element={<AlertRulesPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertRulesPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    metricsMock.mockResolvedValue([metricDefinitionFixture()]);
  });

  it("lista regras usando o workspace da rota e resolve o nome da métrica", async () => {
    listMock.mockResolvedValue([alertRuleFixture()]);
    renderPage();

    expect(await screen.findByText("Temperatura alta")).toBeInTheDocument();
    expect(screen.getByText("Temperatura")).toBeInTheDocument();
    expect(listMock).toHaveBeenCalledWith("token", "workspace-1", 1);
  });

  it("mostra o escopo como 'todos os dispositivos' quando deviceIdentifier é nulo, e o identificador quando definido", async () => {
    listMock.mockResolvedValue([
      alertRuleFixture({ deviceIdentifier: null }),
      alertRuleFixture({ ruleId: "rule-2", id: "rule-2", name: "Regra 2", deviceIdentifier: "device-9" })
    ]);
    renderPage();

    await screen.findByText("Temperatura alta");
    expect(screen.getByText("Todos os dispositivos compatíveis")).toBeInTheDocument();
    expect(screen.getByText("device-9")).toBeInTheDocument();
  });

  it("expõe uma tabela acessível com cabeçalhos de coluna", async () => {
    listMock.mockResolvedValue([alertRuleFixture()]);
    renderPage();

    const table = await screen.findByRole("table");
    for (const header of ["Nome", "Métrica", "Escopo", "Condição", "Severidade", "Estado"]) {
      expect(within(table).getByRole("columnheader", { name: header })).toBeInTheDocument();
    }
  });

  it("mostra o estado de carregamento antes da resposta", () => {
    listMock.mockReturnValue(new Promise<AlertRuleRevision[]>(() => {}));
    renderPage();

    expect(screen.getByText("Carregando regras de alerta")).toBeInTheDocument();
  });

  it("apresenta o estado de erro com ação de repetir e refaz a chamada", async () => {
    const user = userEvent.setup();
    listMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();

    expect(await screen.findByText("Não foi possível carregar as regras de alerta")).toBeInTheDocument();

    listMock.mockResolvedValueOnce([alertRuleFixture()]);
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByText("Temperatura alta")).toBeInTheDocument();
  });

  it("apresenta estado vazio quando não há regras", async () => {
    listMock.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("Nenhuma regra de alerta cadastrada")).toBeInTheDocument();
  });

  it("mantém Anterior e Próxima desabilitados quando a página não está cheia", async () => {
    listMock.mockResolvedValue([
      alertRuleFixture(),
      alertRuleFixture({ ruleId: "rule-2", id: "rule-2", name: "Regra 2" }),
      alertRuleFixture({ ruleId: "rule-3", id: "rule-3", name: "Regra 3" })
    ]);
    renderPage();

    await screen.findByText("Temperatura alta");
    expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Próxima" })).toBeDisabled();
  });

  it("habilita Próxima quando a página vem cheia e pagina corretamente", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue(
      Array.from({ length: 100 }, (_, index) =>
        alertRuleFixture({ ruleId: `rule-${index}`, id: `rule-${index}` })
      )
    );
    renderPage();

    await screen.findAllByText("Temperatura alta");
    expect(screen.getByRole("button", { name: "Próxima" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(listMock).toHaveBeenLastCalledWith("token", "workspace-1", 2);
  });

  it("ativa/desativa uma regra pelo botão de ação e atualiza a linha com a revisão retornada", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([alertRuleFixture({ enabled: true })]);
    updateMock.mockResolvedValue(alertRuleFixture({ enabled: false, version: 2 }));
    renderPage();

    await screen.findByText("Temperatura alta");
    await user.click(screen.getByRole("button", { name: "Desativar" }));

    expect(updateMock).toHaveBeenCalledWith(
      "token",
      "workspace-1",
      "rule-1",
      expect.objectContaining({ enabled: false, expectedVersion: 1 })
    );
    expect(await screen.findByRole("button", { name: "Ativar" })).toBeInTheDocument();
  });

  it("mostra um erro seguro e preserva o botão anterior quando o toggle falha", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([alertRuleFixture({ enabled: true })]);
    updateMock.mockRejectedValue(new ApiError(409, "Conflict"));
    renderPage();

    await screen.findByText("Temperatura alta");
    await user.click(screen.getByRole("button", { name: "Desativar" }));

    expect(await screen.findByText("Não foi possível atualizar a regra")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Desativar" })).toBeInTheDocument();
  });
});
