import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TelemetryExplorerPage } from "./TelemetryExplorerPage";
import * as deviceService from "../services/api/deviceService";
import * as telemetryService from "../services/api/telemetryService";
import type { MetricDefinitionResponse } from "../types";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));
vi.mock("../services/api/deviceService", () => ({ listDevices: vi.fn() }));
vi.mock("../services/api/telemetryService", () => ({ listMetricDefinitions: vi.fn(), queryTelemetry: vi.fn() }));

const device = {
  id: "d1", tenantId: "tenant", workspaceId: "w1", name: "Motor 1", identifier: "motor-1", category: "Sensor",
  isActive: true, createdAtUtc: "2026-01-01T00:00:00Z", operationalStatus: "Online" as const
};
const numeric: MetricDefinitionResponse = {
  id: "m1", metricKey: "temperature_c", displayName: "Temperatura", valueType: "Numeric",
  semanticType: "temperature", canonicalUnit: "°C", status: "Active", isQueryable: true
};
const textMetric: MetricDefinitionResponse = {
  ...numeric, id: "m2", metricKey: "machine_state", displayName: "Estado", valueType: "Text",
  semanticType: "state", canonicalUnit: null
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/w1/explorer"]}>
      <Routes><Route path="/workspaces/:workspaceId/explorer" element={<TelemetryExplorerPage />} /></Routes>
    </MemoryRouter>
  );
}

async function selectBase(metrics = [numeric]) {
  vi.mocked(telemetryService.listMetricDefinitions).mockImplementation(async () => metrics);
  renderPage();
  await userEvent.click(await screen.findByLabelText("Selecionar dispositivo Motor 1"));
  await waitFor(() => expect(telemetryService.listMetricDefinitions).toHaveBeenCalledTimes(1));
  await userEvent.click(await screen.findByLabelText("Selecionar métrica Temperatura"));
}

describe("TelemetryExplorerPage", () => {
  beforeEach(() => {
    vi.mocked(deviceService.listDevices).mockReset();
    vi.mocked(telemetryService.listMetricDefinitions).mockReset();
    vi.mocked(telemetryService.queryTelemetry).mockReset();
    vi.mocked(deviceService.listDevices).mockResolvedValue([device]);
  });

  it("desabilita Executar consulta sem dispositivo e métrica", async () => {
    renderPage();
    expect(await screen.findByRole("button", { name: "Executar consulta" })).toBeDisabled();
  });

  it("apresenta nome, identificador, estado e metadata das opções", async () => {
    vi.mocked(telemetryService.listMetricDefinitions).mockResolvedValue([{ ...numeric, canonicalUnit: null }]);
    renderPage();
    expect(await screen.findByText("motor-1")).toBeInTheDocument();
    expect(screen.getByText("online")).toBeInTheDocument();
    await userEvent.click(screen.getByLabelText("Selecionar dispositivo Motor 1"));
    expect(await screen.findByText("temperature_c")).toBeInTheDocument();
    expect(screen.getByText("Unidade não informada")).toBeInTheDocument();
  });

  it("reduz agregações à interseção dos tipos selecionados", async () => {
    await selectBase([numeric, textMetric]);
    await userEvent.click(screen.getByLabelText("Selecionar métrica Estado"));
    const aggregationSelect = screen.getByRole("combobox", { name: /Agregação/ });
    const options = [...aggregationSelect.querySelectorAll("option")].map((option) => option.value);
    expect(options).toEqual(["raw", "count", "last"]);
    expect(options).not.toContain("avg");
  });

  it("preserva o contrato da consulta raw", async () => {
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({
      workspaceId: "w1", fromUtc: "2026-08-08T10:00:00Z", toUtc: "2026-08-08T11:00:00Z", aggregation: "raw", bucket: null,
      series: [], meta: { totalPoints: 0, maxPointsAllowed: 20000, executionTimeMs: 1 }
    });
    await selectBase();
    await userEvent.click(screen.getByRole("button", { name: "Executar consulta" }));
    await waitFor(() => expect(telemetryService.queryTelemetry).toHaveBeenCalledTimes(1));
    expect(vi.mocked(telemetryService.queryTelemetry).mock.calls[0][2]).toMatchObject({
      deviceIds: ["d1"], metricKeys: ["temperature_c"], aggregation: "raw", bucket: null
    });
  });

  it("renderiza estado vazio útil", async () => {
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({
      workspaceId: "w1", fromUtc: "2026-08-08T10:00:00Z", toUtc: "2026-08-08T11:00:00Z", aggregation: "raw", bucket: null,
      series: [{ deviceId: "d1", metricKey: "temperature_c", valueType: "Numeric", canonicalUnit: "°C", semanticType: null, points: [], truncated: false }],
      meta: { totalPoints: 0, maxPointsAllowed: 20000, executionTimeMs: 1 }
    });
    await selectBase();
    await userEvent.click(screen.getByRole("button", { name: "Executar consulta" }));
    expect(await screen.findByText("Nenhuma telemetria encontrada para o período selecionado")).toBeInTheDocument();
  });

  it("separa unidades diferentes em painéis", async () => {
    const amp: MetricDefinitionResponse = { ...numeric, id: "m3", metricKey: "current_a", displayName: "Corrente", canonicalUnit: "A" };
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({
      workspaceId: "w1", fromUtc: "2026-08-08T10:00:00Z", toUtc: "2026-08-08T11:00:00Z", aggregation: "raw", bucket: null,
      series: [numeric, amp].map((metric, index) => ({
        deviceId: "d1", metricKey: metric.metricKey, valueType: "Numeric", canonicalUnit: metric.canonicalUnit,
        semanticType: metric.semanticType, points: [{ timestampUtc: `2026-07-12T12:00:0${index}Z`, numericValue: index, booleanValue: null, textValue: null, sampleCount: null }], truncated: false
      })),
      meta: { totalPoints: 2, maxPointsAllowed: 20000, executionTimeMs: 1 }
    });
    await selectBase([numeric, amp]);
    await userEvent.click(screen.getByLabelText("Selecionar métrica Corrente"));
    await userEvent.click(screen.getByRole("button", { name: "Executar consulta" }));
    expect(await screen.findByRole("heading", { name: "Temperatura" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Corrente" })).toBeInTheDocument();
  });

  it("renderiza aviso quando o resultado é truncado", async () => {
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({
      workspaceId: "w1", fromUtc: "2026-08-08T10:00:00Z", toUtc: "2026-08-08T11:00:00Z", aggregation: "raw", bucket: null,
      series: [{ deviceId: "d1", metricKey: "temperature_c", valueType: "Numeric", canonicalUnit: "°C", semanticType: null, points: [], truncated: true }],
      meta: { totalPoints: 0, maxPointsAllowed: 20000, executionTimeMs: 1 }
    });
    await selectBase();
    await userEvent.click(screen.getByRole("button", { name: "Executar consulta" }));
    expect(await screen.findByText(/Resultado truncado/)).toBeInTheDocument();
  });
});
