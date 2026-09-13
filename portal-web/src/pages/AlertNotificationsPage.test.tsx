import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as alertNotificationsService from "../services/api/alertNotificationsService";
import type { PortalNotification } from "../types";
import { AlertNotificationsPage } from "./AlertNotificationsPage";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));

vi.mock("../services/api/alertNotificationsService", () => ({
  listMyNotifications: vi.fn(),
  markNotificationRead: vi.fn()
}));

const listMock = vi.mocked(alertNotificationsService.listMyNotifications);
const markReadMock = vi.mocked(alertNotificationsService.markNotificationRead);

function notificationFixture(overrides: Partial<PortalNotification> = {}): PortalNotification {
  return {
    id: "notification-1",
    eventId: "event-1",
    ruleId: "rule-1",
    ruleName: "Temperatura alta",
    deviceIdentifier: "sensor-1",
    eventStatus: "Firing",
    transitionKind: "Firing",
    createdAtUtc: "2026-09-13T12:00:00Z",
    readAtUtc: null,
    ...overrides
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/workspaces/workspace-1/alerts/notifications"]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/alerts/notifications" element={<AlertNotificationsPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("AlertNotificationsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("lista notificações da própria conta", async () => {
    listMock.mockResolvedValue([notificationFixture()]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(within(table).getByText("Temperatura alta")).toBeInTheDocument();
    expect(within(table).getByText("sensor-1")).toBeInTheDocument();
    expect(listMock).toHaveBeenCalledWith("token", "workspace-1", 1);
  });

  it("mostra estado vazio quando não há notificações", async () => {
    listMock.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText("Nenhuma notificação")).toBeInTheDocument();
  });

  it("filtra por lida/não lida sem novo fetch", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([
      notificationFixture({ id: "notification-1", readAtUtc: null }),
      notificationFixture({ id: "notification-2", readAtUtc: "2026-09-13T13:00:00Z" })
    ]);
    renderPage();

    const table = await screen.findByRole("table");
    expect(within(table).getAllByRole("row")).toHaveLength(3); // header + 2 rows

    await user.selectOptions(screen.getByLabelText("Estado"), "unread");
    expect(within(table).getAllByRole("row")).toHaveLength(2); // header + 1 row
    expect(listMock).toHaveBeenCalledTimes(1);
  });

  it("marca uma notificação como lida e remove o botão de ação", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue([notificationFixture()]);
    markReadMock.mockResolvedValue();
    renderPage();

    const button = await screen.findByRole("button", { name: "Marcar como lida" });
    await user.click(button);

    expect(markReadMock).toHaveBeenCalledWith("token", "workspace-1", "notification-1");
    expect(await screen.findByRole("table")).not.toHaveTextContent("Marcar como lida");
  });

  it("apresenta o estado de erro com ação de repetir", async () => {
    const user = userEvent.setup();
    listMock.mockRejectedValueOnce(new Error("falhou"));
    renderPage();

    expect(await screen.findByText("Não foi possível carregar as notificações")).toBeInTheDocument();

    listMock.mockResolvedValueOnce([notificationFixture()]);
    await user.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(await screen.findByText("Temperatura alta")).toBeInTheDocument();
  });

  it("pagina pelos controles Anterior/Próxima quando a página está cheia", async () => {
    const user = userEvent.setup();
    listMock.mockResolvedValue(
      Array.from({ length: 100 }, (_, index) => notificationFixture({ id: `notification-${index}` }))
    );
    renderPage();

    await screen.findAllByText("Temperatura alta");
    expect(screen.getByRole("button", { name: "Próxima" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(listMock).toHaveBeenLastCalledWith("token", "workspace-1", 2);
  });

  it("linka para o histórico do evento associado", async () => {
    listMock.mockResolvedValue([notificationFixture()]);
    renderPage();

    const link = await screen.findByRole("link", { name: "Ver histórico" });
    expect(link).toHaveAttribute("href", "/workspaces/workspace-1/alerts/events/event-1");
  });
});
