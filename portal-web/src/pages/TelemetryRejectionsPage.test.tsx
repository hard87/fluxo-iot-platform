import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as service from "../services/api/rejectionService";
import type { TelemetryRejectionPage } from "../types";
import { TelemetryRejectionsPage } from "./TelemetryRejectionsPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));
vi.mock("../services/api/rejectionService", () => ({ listTelemetryRejections: vi.fn() }));

const listMock = vi.mocked(service.listTelemetryRejections);

function pageWith(overrides: Partial<TelemetryRejectionPage> = {}): TelemetryRejectionPage {
  return {
    page: 1,
    pageSize: 20,
    totalCount: 1,
    items: [
      {
        id: "r1",
        receivedAtUtc: "2026-09-08T12:00:00Z",
        topic: "fluxo/device/telemetry",
        payloadPreview: '{"bad":true}',
        errorType: "Validation",
        reason: "schema inválido",
        deviceId: "edgewarden",
        messageType: "telemetry",
        sequence: 42,
        reprocessed: false,
        reprocessAttempts: 0
      }
    ],
    ...overrides
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/rejections"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/rejections" element={<TelemetryRejectionsPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("TelemetryRejectionsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    listMock.mockResolvedValue(pageWith());
  });

  it("lista rejeições usando o workspace da rota", async () => {
    renderPage();
    expect(await screen.findByText("schema inválido")).toBeInTheDocument();
    expect(screen.getByText("edgewarden")).toBeInTheDocument();
    expect(listMock).toHaveBeenCalledWith("token", "workspace-1", 1, 20, "");
  });

  it("expõe uma tabela acessível com legenda e cabeçalhos de coluna", async () => {
    renderPage();
    const table = await screen.findByRole("table");
    expect(within(table).getByText(/da mais recente para a mais antiga/i)).toBeInTheDocument();
    for (const header of ["Recebida", "Dispositivo", "Tópico", "Motivo", "Prévia do payload", "Reprocessamento"]) {
      expect(within(table).getByRole("columnheader", { name: header })).toBeInTheDocument();
    }
  });

  it("mostra o estado de carregamento antes da resposta", () => {
    let resolve: (value: TelemetryRejectionPage) => void = () => {};
    listMock.mockReturnValue(new Promise<TelemetryRejectionPage>(r => { resolve = r; }));
    renderPage();
    expect(screen.getByText("Carregando mensagens rejeitadas")).toBeInTheDocument();
    resolve(pageWith());
  });

  it("apresenta o estado de erro com ação de repetir e refaz a chamada", async () => {
    const user = userEvent.setup();
    listMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();
    expect(await screen.findByText("Não foi possível carregar as rejeições")).toBeInTheDocument();

    listMock.mockResolvedValueOnce(pageWith());
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByText("schema inválido")).toBeInTheDocument();
  });

  it("apresenta estado vazio", async () => {
    listMock.mockResolvedValue(pageWith({ totalCount: 0, items: [] }));
    renderPage();
    expect(await screen.findByText("Nenhuma rejeição encontrada")).toBeInTheDocument();
  });

  it("pagina pelos controles Anterior/Próxima", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue(pageWith({ totalCount: 60, pageSize: 20 }));
    renderPage();
    await screen.findByText("schema inválido");

    expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(listMock).toHaveBeenLastCalledWith("token", "workspace-1", 2, 20, "");
  });

  it("aplica a busca por teclado e volta para a primeira página", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue(pageWith({ totalCount: 60, pageSize: 20 }));
    renderPage();
    await screen.findByText("schema inválido");

    await user.tab();
    expect(screen.getByRole("searchbox", { name: "Buscar" })).toHaveFocus();
    await user.keyboard("edgewarden{Enter}");

    expect(listMock).toHaveBeenLastCalledWith("token", "workspace-1", 1, 20, "edgewarden");
  });

  it("renderiza payloads e motivos longos sem truncar no cliente", async () => {
    const longReason = "motivo ".repeat(40).trim();
    const longPreview = `{"blob":"${"y".repeat(220)}"}`;
    listMock.mockResolvedValue(
      pageWith({ items: [{ ...pageWith().items[0], reason: longReason, payloadPreview: longPreview }] })
    );
    renderPage();
    expect(await screen.findByText(longReason)).toBeInTheDocument();
    expect(screen.getByText(longPreview)).toBeInTheDocument();
  });
});
