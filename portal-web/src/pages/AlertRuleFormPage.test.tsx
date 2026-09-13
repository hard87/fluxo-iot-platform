import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as alertRulesService from "../services/api/alertRulesService";
import * as deviceService from "../services/api/deviceService";
import { ApiError } from "../services/api/httpClient";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertRuleRevision, DeviceResponse, MetricDefinitionResponse } from "../types";
import { AlertRuleFormPage } from "./AlertRuleFormPage";

const navigateMock = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));

vi.mock("../services/api/alertRulesService", async () => {
  const actual = await vi.importActual<typeof import("../services/api/alertRulesService")>(
    "../services/api/alertRulesService"
  );
  return {
    ...actual,
    listAlertRules: vi.fn(),
    createAlertRule: vi.fn(),
    updateAlertRule: vi.fn()
  };
});

vi.mock("../services/api/telemetryService", () => ({ listMetricDefinitions: vi.fn() }));
vi.mock("../services/api/deviceService", () => ({ listDevices: vi.fn() }));

const listRulesMock = vi.mocked(alertRulesService.listAlertRules);
const createMock = vi.mocked(alertRulesService.createAlertRule);
const updateMock = vi.mocked(alertRulesService.updateAlertRule);
const metricsMock = vi.mocked(telemetryService.listMetricDefinitions);
const devicesMock = vi.mocked(deviceService.listDevices);

function numericMetric(overrides: Partial<MetricDefinitionResponse> = {}): MetricDefinitionResponse {
  return {
    id: "metric-numeric",
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

function booleanMetric(overrides: Partial<MetricDefinitionResponse> = {}): MetricDefinitionResponse {
  return {
    id: "metric-boolean",
    metricKey: "door_open",
    displayName: "Porta aberta",
    valueType: "Boolean",
    semanticType: null,
    canonicalUnit: null,
    status: "Discovered",
    isQueryable: true,
    ...overrides
  };
}

function deviceFixture(overrides: Partial<DeviceResponse> = {}): DeviceResponse {
  return {
    id: "device-1",
    tenantId: "tenant-1",
    workspaceId: "workspace-1",
    name: "Sensor 1",
    identifier: "sensor-1",
    category: "Sensor",
    isActive: true,
    createdAtUtc: "2026-09-12T12:00:00Z",
    operationalStatus: "Online",
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
    metricDefinitionId: "metric-numeric",
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

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts/new"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/new" element={<AlertRuleFormPage />} />
      </Routes>
    </MemoryRouter>
  );
}

function renderEdit(rule?: AlertRuleRevision) {
  return render(
    <MemoryRouter
      initialEntries={[
        {
          pathname: "/workspaces/workspace-1/alerts/rule-1/edit",
          state: rule ? { rule } : undefined
        }
      ]}
    >
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/:ruleId/edit" element={<AlertRuleFormPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertRuleFormPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    metricsMock.mockResolvedValue([numericMetric(), booleanMetric()]);
    devicesMock.mockResolvedValue([deviceFixture()]);
  });

  afterEach(() => {
    navigateMock.mockReset();
  });

  it("cria uma regra numérica válida com enabled forçado para false", async () => {
    const user = userEvent.setup();
    createMock.mockResolvedValue(alertRuleFixture({ enabled: false }));
    renderCreate();

    await user.type(await screen.findByLabelText("Nome"), "Temperatura alta");
    await user.selectOptions(screen.getByLabelText("Métrica"), "metric-numeric");
    await user.selectOptions(screen.getByLabelText("Condição"), "GreaterThan");
    await user.type(screen.getByLabelText("Limite"), "30");

    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(createMock).toHaveBeenCalledWith(
      "token",
      "workspace-1",
      expect.objectContaining({
        name: "Temperatura alta",
        metricDefinitionId: "metric-numeric",
        operator: "GreaterThan",
        threshold: 30,
        thresholdHigh: null,
        hysteresis: 0,
        enabled: false
      })
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith("/workspaces/workspace-1/alerts"));
  });

  it("esconde limite/histerese para métricas booleanas e envia threshold nulo", async () => {
    const user = userEvent.setup();
    createMock.mockResolvedValue(alertRuleFixture());
    renderCreate();

    await user.type(await screen.findByLabelText("Nome"), "Porta aberta");
    await user.selectOptions(screen.getByLabelText("Métrica"), "metric-boolean");

    expect(screen.queryByLabelText("Limite")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Histerese")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(createMock).toHaveBeenCalledWith(
      "token",
      "workspace-1",
      expect.objectContaining({ operator: "IsTrue", threshold: null, thresholdHigh: null, hysteresis: 0 })
    );
  });

  it("mostra o limite superior apenas para operadores de intervalo e valida inferior <= superior", async () => {
    const user = userEvent.setup();
    renderCreate();

    await user.type(await screen.findByLabelText("Nome"), "Faixa de pressão");
    await user.selectOptions(screen.getByLabelText("Métrica"), "metric-numeric");
    await user.selectOptions(screen.getByLabelText("Condição"), "InsideRange");

    expect(screen.getByLabelText("Limite superior")).toBeInTheDocument();

    await user.type(screen.getByLabelText("Limite inferior"), "20");
    await user.type(screen.getByLabelText("Limite superior"), "10");
    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(await screen.findByText("O limite superior deve ser maior ou igual ao inferior.")).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("não envia a requisição quando a validação do cliente falha", async () => {
    const user = userEvent.setup();
    renderCreate();

    await screen.findByLabelText("Nome");
    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(await screen.findByText("Nome é obrigatório.")).toBeInTheDocument();
    expect(screen.getByText("Selecione uma métrica.")).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it("preenche o formulário a partir da regra recebida via state, sem consultar a lista", async () => {
    renderEdit(alertRuleFixture());

    expect(await screen.findByDisplayValue("Temperatura alta")).toBeInTheDocument();
    expect(screen.getByLabelText("Regra ativa")).toBeChecked();
    expect(listRulesMock).not.toHaveBeenCalled();
  });

  it("exige confirmação explícita ao editar uma regra ativa, e cancelar não salva", async () => {
    const user = userEvent.setup();
    renderEdit(alertRuleFixture({ enabled: true }));

    await screen.findByDisplayValue("Temperatura alta");
    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(await screen.findByText(/Esta regra está ativa/)).toBeInTheDocument();
    expect(updateMock).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(screen.queryByText(/Esta regra está ativa/)).not.toBeInTheDocument();
    expect(updateMock).not.toHaveBeenCalled();
  });

  it("confirma a alteração de uma regra ativa e envia expectedVersion", async () => {
    const user = userEvent.setup();
    updateMock.mockResolvedValue(alertRuleFixture({ version: 2 }));
    renderEdit(alertRuleFixture({ enabled: true, version: 3 }));

    await screen.findByDisplayValue("Temperatura alta");
    await user.click(screen.getByRole("button", { name: "Salvar regra" }));
    await user.click(await screen.findByRole("button", { name: "Confirmar e salvar" }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith(
        "token",
        "workspace-1",
        "rule-1",
        expect.objectContaining({ expectedVersion: 3 })
      )
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith("/workspaces/workspace-1/alerts"));
  });

  it("mostra uma mensagem específica quando o servidor recusa por conflito de versão (409)", async () => {
    const user = userEvent.setup();
    updateMock.mockRejectedValue(new ApiError(409, "Conflict"));
    renderEdit(alertRuleFixture({ enabled: false }));

    await screen.findByDisplayValue("Temperatura alta");
    await user.click(screen.getByRole("button", { name: "Salvar regra" }));

    expect(await screen.findByText(/alterada por outra ação enquanto você editava/)).toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it("mostra 'regra não encontrada' quando a busca pela lista não encontra o id", async () => {
    listRulesMock.mockResolvedValue([]);
    renderEdit();

    expect(await screen.findByText("Regra não encontrada")).toBeInTheDocument();
  });
});
