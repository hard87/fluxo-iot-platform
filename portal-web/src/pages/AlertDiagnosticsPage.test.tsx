import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as alertEventsService from "../services/api/alertEventsService";
import * as alertRulesService from "../services/api/alertRulesService";
import { ApiError } from "../services/api/httpClient";
import type { AlertAttemptDiagnostic, AlertRuleRevision } from "../types";
import { AlertDiagnosticsPage } from "./AlertDiagnosticsPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));

vi.mock("../services/api/alertEventsService", () => ({ listAlertDiagnostics: vi.fn() }));

vi.mock("../services/api/alertRulesService", async () => {
  const actual = await vi.importActual<typeof import("../services/api/alertRulesService")>(
    "../services/api/alertRulesService"
  );
  return { ...actual, listAllAlertRules: vi.fn() };
});

const listDiagnosticsMock = vi.mocked(alertEventsService.listAlertDiagnostics);
const listAllRulesMock = vi.mocked(alertRulesService.listAllAlertRules);

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

function diagnosticFixture(overrides: Partial<AlertAttemptDiagnostic> = {}): AlertAttemptDiagnostic {
  return {
    id: "attempt-1",
    workItemId: "work-1",
    ruleId: "rule-1",
    deviceIdentifier: "sensor-1",
    status: "Failed",
    attemptCount: 2,
    nextAttemptAtUtc: "2026-09-12T12:05:00Z",
    reason: "EvaluationFailed",
    ...overrides
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts/diagnostics"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/diagnostics" element={<AlertDiagnosticsPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertDiagnosticsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listAllRulesMock.mockResolvedValue([alertRuleFixture()]);
  });

  it("lista falhas e resolve o nome da regra", async () => {
    listDiagnosticsMock.mockResolvedValue([diagnosticFixture()]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(await within(table).findByText("Temperatura alta")).toBeInTheDocument();
    expect(within(table).getByText("sensor-1")).toBeInTheDocument();
    expect(listDiagnosticsMock).toHaveBeenCalledWith("token", "workspace-1", 1);
  });

  it("diferencia Failed (com próxima tentativa) de DeadLetter (sem nova tentativa), por texto", async () => {
    listDiagnosticsMock.mockResolvedValue([
      diagnosticFixture({ id: "attempt-1", status: "Failed", nextAttemptAtUtc: "2026-09-12T12:05:00Z" }),
      diagnosticFixture({ id: "attempt-2", status: "DeadLetter", reason: "LeaseRecoveryExhausted" })
    ]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(await within(table).findByText("Falha — nova tentativa agendada")).toBeInTheDocument();
    expect(within(table).getByText("Falha permanente (dead letter)")).toBeInTheDocument();
    expect(within(table).getByText("Não haverá nova tentativa")).toBeInTheDocument();
    expect(within(table).getByText("Falha ao avaliar a regra")).toBeInTheDocument();
    expect(within(table).getByText("Concessão de processamento expirada; tentativas esgotadas")).toBeInTheDocument();
  });

  it("mostra o estado de carregamento antes da resposta", () => {
    listDiagnosticsMock.mockReturnValue(new Promise<AlertAttemptDiagnostic[]>(() => {}));
    renderPage();

    expect(screen.getByText("Carregando diagnóstico de alertas")).toBeInTheDocument();
  });

  it("apresenta o estado de erro com ação de repetir e refaz a chamada", async () => {
    const user = userEvent.setup();
    listDiagnosticsMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();

    expect(await screen.findByText("Não foi possível carregar o diagnóstico")).toBeInTheDocument();

    listDiagnosticsMock.mockResolvedValueOnce([diagnosticFixture()]);
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByRole("table")).toBeInTheDocument();
  });

  it("mostra uma mensagem específica de acesso restrito para 403, sem ação de repetir", async () => {
    listDiagnosticsMock.mockRejectedValueOnce(new ApiError(403, "Forbidden"));
    renderPage();

    expect(await screen.findByText("Acesso restrito a administradores")).toBeInTheDocument();
    expect(
      screen.getByText("Você precisa ter o papel de Admin neste workspace para ver o diagnóstico de entrega.")
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Tentar novamente" })).not.toBeInTheDocument();
  });

  it("apresenta estado vazio quando não há falhas", async () => {
    listDiagnosticsMock.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("Nenhuma falha registrada")).toBeInTheDocument();
  });

  it("pagina pelos controles Anterior/Próxima quando a página está cheia", async () => {
    const user = userEvent.setup();
    listDiagnosticsMock.mockResolvedValue(
      Array.from({ length: 100 }, (_, index) => diagnosticFixture({ id: `attempt-${index}` }))
    );
    renderPage();

    await screen.findByRole("table");
    expect(screen.getByRole("button", { name: "Próxima" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(listDiagnosticsMock).toHaveBeenLastCalledWith("token", "workspace-1", 2);
  });
});
