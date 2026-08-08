import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { DashboardResponse, Workspace } from "../types";
import * as deviceService from "../services/api/deviceService";
import * as workspaceService from "../services/api/workspaceService";
import { getWorkspaceHealthSummary } from "../components/dashboard/WorkspaceHealthStatus";
import { DashboardPage } from "./DashboardPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));
vi.mock("../services/api/deviceService", () => ({ getDashboard: vi.fn() }));
vi.mock("../services/api/workspaceService", () => ({ listWorkspaces: vi.fn() }));

const workspace: Workspace = {
  id: "workspace-1",
  tenantId: "tenant-01",
  name: "Fábrica Norte",
  role: "Owner",
  createdAtUtc: "2026-01-01T00:00:00Z"
};

function createDashboard(overrides: Partial<DashboardResponse> = {}): DashboardResponse {
  return {
    workspaceId: "workspace-1",
    tenantId: "tenant-01",
    devicesTotal: 10,
    devicesOnline: 8,
    devicesOffline: 1,
    devicesUnknown: 1,
    lastTelemetryReceivedAtUtc: new Date(Date.now() - 2 * 60_000).toISOString(),
    messagesProcessed: 4511,
    messagesRejected: 27,
    ...overrides
  };
}

function renderDashboard() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/dashboard"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/dashboard" element={<DashboardPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("DashboardPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(workspaceService.listWorkspaces).mockResolvedValue([workspace]);
    vi.mocked(deviceService.getDashboard).mockResolvedValue(createDashboard());
  });

  it("apresenta contexto, KPIs, atividade relativa, saúde e CTAs", async () => {
    renderDashboard();

    expect(await screen.findByText("Fábrica Norte · Tenant tenant-01")).toBeInTheDocument();
    expect(screen.getByLabelText("Dispositivos: 10")).toBeInTheDocument();
    expect(screen.getByLabelText("Online: 8")).toBeInTheDocument();
    expect(screen.getByLabelText("Offline: 1")).toBeInTheDocument();
    expect(screen.getByLabelText("Unknown: 1")).toBeInTheDocument();
    expect(screen.getByLabelText("Mensagens processadas: 4.511")).toBeInTheDocument();
    expect(screen.getByLabelText("Mensagens rejeitadas: 27")).toBeInTheDocument();
    expect(screen.getByText("Há 2 minutos")).toBeInTheDocument();
    expect(screen.getByText("Offline detectado")).toBeInTheDocument();

    expect(screen.getByRole("link", { name: "Ver dispositivos" })).toHaveAttribute(
      "href",
      "/workspaces/workspace-1/devices"
    );
    expect(screen.getByRole("link", { name: "Explorar telemetria" })).toHaveAttribute(
      "href",
      "/workspaces/workspace-1/explorer"
    );

    const activity = screen.getByRole("heading", { name: "Última telemetria" }).closest("article");
    expect(activity).not.toBeNull();
    expect(within(activity!).getByText(/Horário local/)).toBeInTheDocument();
    expect(activity!.querySelector("time")).toHaveAttribute("dateTime", expect.stringContaining("T"));
  });

  it("trata ausência de telemetria como estado desconhecido", async () => {
    vi.mocked(deviceService.getDashboard).mockResolvedValue(
      createDashboard({
        devicesOffline: 0,
        devicesUnknown: 0,
        messagesRejected: 0,
        lastTelemetryReceivedAtUtc: null
      })
    );

    renderDashboard();

    expect(await screen.findByText("Sem telemetria recebida")).toBeInTheDocument();
    expect(screen.getByText("Sem telemetria")).toBeInTheDocument();
  });

  it("apresenta estado de erro quando os dados não podem ser carregados", async () => {
    vi.mocked(deviceService.getDashboard).mockRejectedValue(new Error("network"));

    renderDashboard();

    expect(await screen.findByText("Dados indisponíveis")).toBeInTheDocument();
    expect(screen.getByText("Não foi possível avaliar a situação do workspace com os dados atuais.")).toBeInTheDocument();
  });
});

describe("getWorkspaceHealthSummary", () => {
  it("mantém estados healthy, warning, offline e unknown determinísticos", () => {
    expect(getWorkspaceHealthSummary(createDashboard({ devicesOffline: 1 })).state).toBe("offline");
    expect(
      getWorkspaceHealthSummary(createDashboard({ devicesOffline: 0, devicesUnknown: 0, messagesRejected: 1 })).state
    ).toBe("warning");
    expect(
      getWorkspaceHealthSummary(
        createDashboard({ devicesOffline: 0, devicesUnknown: 1, messagesRejected: 0 })
      ).state
    ).toBe("unknown");
    expect(
      getWorkspaceHealthSummary(
        createDashboard({ devicesOffline: 0, devicesUnknown: 0, messagesRejected: 0 })
      ).state
    ).toBe("healthy");
  });
});
