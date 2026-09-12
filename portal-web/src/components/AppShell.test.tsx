import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AppShell } from "./AppShell";

const shellState = vi.hoisted(() => ({
  logout: vi.fn(),
  setWorkspaceId: vi.fn(),
  workspaceId: "workspace-1" as string | null
}));

vi.mock("../hooks/useAuth", () => ({
  useAuth: () => ({
    user: { userId: "user-1", email: "operator@fluxo.local" },
    logout: shellState.logout
  })
}));

vi.mock("../hooks/useWorkspaceSelection", () => ({
  useWorkspaceSelection: () => ({
    workspaceId: shellState.workspaceId,
    setWorkspaceId: shellState.setWorkspaceId
  })
}));

function renderShell(initialEntry: string) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/workspaces" element={<h1>Workspaces content</h1>} />
          <Route path="/workspaces/:workspaceId/dashboard" element={<h1>Dashboard content</h1>} />
          <Route path="/workspaces/:workspaceId/devices" element={<h1>Devices content</h1>} />
          <Route path="/workspaces/:workspaceId/explorer" element={<h1>Explorer content</h1>} />
          <Route path="/status" element={<h1>Health content</h1>} />
        </Route>
        <Route path="/login" element={<h1>Login content</h1>} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AppShell", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    shellState.workspaceId = "workspace-1";
  });

  it("mantém a navegação completa e indica a rota atual", async () => {
    renderShell("/workspaces/workspace-1/dashboard");

    expect(screen.getByRole("link", { name: "Pular para o conteúdo" })).toHaveAttribute("href", "#main-content");
    expect(screen.getByText("operator@fluxo.local")).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "Navegação principal" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Workspaces" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Dashboard" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Dispositivos" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Telemetry Explorer" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Saúde da plataforma" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("link", { name: "Telemetry Explorer" }));
    expect(screen.getByRole("heading", { name: "Explorer content" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Telemetry Explorer" })).toHaveAttribute("aria-current", "page");

    await userEvent.click(screen.getByRole("link", { name: "Saúde da plataforma" }));
    expect(screen.getByRole("heading", { name: "Health content" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Saúde da plataforma" })).toHaveAttribute("aria-current", "page");
  });

  it("expõe rotas de workspace como indisponíveis sem seleção", () => {
    shellState.workspaceId = null;
    renderShell("/workspaces");

    expect(screen.getByRole("link", { name: "Workspaces" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("Dashboard")).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText("Dispositivos")).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText("Telemetry Explorer")).toHaveAttribute("aria-disabled", "true");
  });

  it("encerra a sessão e navega para o login", async () => {
    renderShell("/status");

    await userEvent.click(screen.getByRole("button", { name: "Sair" }));

    expect(shellState.logout).toHaveBeenCalledOnce();
    expect(screen.getByRole("heading", { name: "Login content" })).toBeInTheDocument();
  });
});
