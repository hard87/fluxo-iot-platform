import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as statusService from "../services/api/statusService";
import type { PlatformStatusResponse } from "../types";
import { HealthPage } from "./HealthPage";

vi.mock("../services/api/statusService", () => ({ getPlatformStatus: vi.fn() }));

function response(overrides: Partial<PlatformStatusResponse> = {}): PlatformStatusResponse {
  return {
    status: "Healthy",
    checkedAtUtc: "2026-08-08T12:27:22.000Z",
    components: [
      {
        name: "postgresql",
        status: "Healthy",
        durationMs: 12.5,
        description: "Conexão com o banco de dados",
        lastCheckedUtc: "2026-08-08T12:27:22.000Z"
      }
    ],
    ...overrides
  };
}

describe("HealthPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mostra o estado de carregamento e depois os componentes reais", async () => {
    vi.mocked(statusService.getPlatformStatus).mockResolvedValue(response());

    render(<HealthPage />);

    expect(screen.getByText("Consultando status")).toBeInTheDocument();
    expect(await screen.findByText("postgresql")).toBeInTheDocument();
    expect(screen.getByText("Conexão com o banco de dados")).toBeInTheDocument();
    expect(screen.getAllByText("Healthy").length).toBeGreaterThan(0);
    expect(screen.queryByText(/2026-08-08T/)).not.toBeInTheDocument();
  });

  it("nunca mostra Healthy quando nenhum componente é monitorado", async () => {
    vi.mocked(statusService.getPlatformStatus).mockResolvedValue(response({ status: "Healthy", components: [] }));

    render(<HealthPage />);

    expect(await screen.findByText("Nenhum componente monitorado")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("mostra estado de erro com opção de tentar novamente", async () => {
    vi.mocked(statusService.getPlatformStatus).mockRejectedValue(new Error("network"));

    render(<HealthPage />);

    expect(await screen.findByText("Não foi possível consultar a saúde da plataforma")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Tentar novamente" })).toBeInTheDocument();
  });
});
