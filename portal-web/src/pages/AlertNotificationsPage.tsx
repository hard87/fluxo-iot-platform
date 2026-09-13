import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AlertEventStatusBadge } from "../components/alerts/AlertEventStatusBadge";
import { formatTimestampWithZone } from "../components/alerts/alertEventPresentation";
import { AlertsSectionTabs } from "../components/alerts/AlertsSectionTabs";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertNotificationsService from "../services/api/alertNotificationsService";
import { hasPossibleNextAlertsPage } from "../services/api/alertRulesService";
import type { PortalNotification } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

type ReadFilter = "all" | "unread" | "read";

export function AlertNotificationsPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();

  const [notifications, setNotifications] = useState<PortalNotification[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [readFilter, setReadFilter] = useState<ReadFilter>("all");
  const [markingId, setMarkingId] = useState<string | null>(null);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const load = useCallback(async (authToken: string, currentWorkspaceId: string, currentPage: number) => {
    setLoading(true);
    setError(null);
    try {
      const response = await alertNotificationsService.listMyNotifications(authToken, currentWorkspaceId, currentPage);
      if (isMountedRef.current) {
        setNotifications(response);
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(err);
      }
    } finally {
      if (isMountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    if (!token || !workspaceId) {
      return;
    }
    void load(token, workspaceId, page);
  }, [token, workspaceId, page, load]);

  async function handleMarkRead(notificationId: string) {
    if (!token || !workspaceId) {
      return;
    }
    setMarkingId(notificationId);
    try {
      await alertNotificationsService.markNotificationRead(token, workspaceId, notificationId);
      if (isMountedRef.current) {
        const readAtUtc = new Date().toISOString();
        setNotifications((current) =>
          current.map((item) => (item.id === notificationId ? { ...item, readAtUtc } : item))
        );
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(err);
      }
    } finally {
      if (isMountedRef.current) {
        setMarkingId(null);
      }
    }
  }

  const hasNextPage = hasPossibleNextAlertsPage(notifications);

  const visibleNotifications = notifications.filter((notification) => {
    if (readFilter === "unread") {
      return notification.readAtUtc === null;
    }
    if (readFilter === "read") {
      return notification.readAtUtc !== null;
    }
    return true;
  });

  return (
    <section>
      <PageHeader
        title="Notificações"
        description="Notificações de disparo e resolução para as regras em que você é destinatário no canal de portal."
      />

      {workspaceId ? <AlertsSectionTabs workspaceId={workspaceId} /> : null}

      {loading ? <LoadingState compact title="Carregando notificações" /> : null}

      {!loading && error ? (
        <ErrorState
          title="Não foi possível carregar as notificações"
          description={getApiErrorMessage(error, "Ocorreu um erro inesperado ao carregar as notificações.")}
          action={
            token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId, page)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && notifications.length === 0 ? (
        <EmptyState
          title="Nenhuma notificação"
          description="Você não é destinatário de nenhuma regra com o canal de portal habilitado, ou nenhuma disparou ainda."
        />
      ) : null}

      {!loading && !error && notifications.length > 0 ? (
        <div className="panel">
          <div className="device-registry-filters">
            <label>
              Estado
              <select value={readFilter} onChange={(event) => setReadFilter(event.target.value as ReadFilter)}>
                <option value="all">Todas</option>
                <option value="unread">Não lidas</option>
                <option value="read">Lidas</option>
              </select>
            </label>
          </div>
          <p className="muted">Filtro aplicado à página atual. Use Próxima para ver mais notificações.</p>

          {visibleNotifications.length ? (
            <div className="table-scroll">
              <table className="alert-rule-table">
                <caption className="visually-hidden">Notificações de portal deste usuário</caption>
                <thead>
                  <tr>
                    <th scope="col">Regra</th>
                    <th scope="col">Dispositivo</th>
                    <th scope="col">Transição</th>
                    <th scope="col">Criada em</th>
                    <th scope="col">Lida em</th>
                    <th scope="col">
                      <span className="visually-hidden">Ações</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visibleNotifications.map((notification) => (
                    <tr key={notification.id}>
                      <td>{notification.ruleName}</td>
                      <td>{notification.deviceIdentifier}</td>
                      <td>
                        <AlertEventStatusBadge status={notification.transitionKind} />
                      </td>
                      <td>{formatTimestampWithZone(notification.createdAtUtc)}</td>
                      <td>{notification.readAtUtc ? formatTimestampWithZone(notification.readAtUtc) : "—"}</td>
                      <td>
                        {notification.readAtUtc ? null : (
                          <button
                            type="button"
                            className="button-secondary"
                            disabled={markingId === notification.id}
                            onClick={() => void handleMarkRead(notification.id)}
                          >
                            {markingId === notification.id ? "Marcando…" : "Marcar como lida"}
                          </button>
                        )}
                        {workspaceId ? (
                          <Link className="button-secondary" to={`/workspaces/${workspaceId}/alerts/events/${notification.eventId}`}>
                            Ver histórico
                          </Link>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyState compact title="Nenhuma notificação corresponde ao filtro" description="Ajuste o estado." />
          )}

          <nav className="pagination" aria-label="Paginação das notificações">
            <button type="button" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              Anterior
            </button>
            <span>Página {page}</span>
            <button type="button" disabled={!hasNextPage} onClick={() => setPage((current) => current + 1)}>
              Próxima
            </button>
          </nav>
        </div>
      ) : null}
    </section>
  );
}
