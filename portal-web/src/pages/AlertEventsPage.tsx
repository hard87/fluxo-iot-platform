import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AlertEventStatusBadge } from "../components/alerts/AlertEventStatusBadge";
import { AlertSeverityBadge } from "../components/alerts/AlertSeverityBadge";
import { formatTimestampWithZone } from "../components/alerts/alertEventPresentation";
import { AlertsSectionTabs } from "../components/alerts/AlertsSectionTabs";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertEventsService from "../services/api/alertEventsService";
import * as alertRulesService from "../services/api/alertRulesService";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertEvent, AlertEventStatus, AlertRuleRevision, AlertSeverity } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

type StatusFilter = AlertEventStatus | "all";
type SeverityFilter = AlertSeverity | "all";

export function AlertEventsPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();

  const [events, setEvents] = useState<AlertEvent[]>([]);
  const [rulesById, setRulesById] = useState<Record<string, AlertRuleRevision>>({});
  const [metricNames, setMetricNames] = useState<Record<string, string>>({});
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [severityFilter, setSeverityFilter] = useState<SeverityFilter>("all");

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
      const response = await alertEventsService.listAlertEvents(authToken, currentWorkspaceId, currentPage);
      if (isMountedRef.current) {
        setEvents(response);
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

  useEffect(() => {
    if (!token || !workspaceId) {
      return;
    }
    let cancelled = false;
    Promise.all([
      alertRulesService.listAllAlertRules(token, workspaceId),
      telemetryService.listMetricDefinitions(token, workspaceId)
    ])
      .then(([rules, metrics]) => {
        if (cancelled) {
          return;
        }
        const byRuleId: Record<string, AlertRuleRevision> = {};
        for (const rule of rules) {
          byRuleId[rule.ruleId] = rule;
        }
        const namesByMetricId: Record<string, string> = {};
        for (const metric of metrics) {
          namesByMetricId[metric.id] = metric.displayName;
        }
        setRulesById(byRuleId);
        setMetricNames(namesByMetricId);
      })
      .catch(() => {
        // Enrichment (rule name/metric/severity) is presentation-only: on failure, rows fall back
        // to raw ruleId and omit severity rather than blocking the events list from rendering.
      });
    return () => {
      cancelled = true;
    };
  }, [token, workspaceId]);

  const hasNextPage = alertRulesService.hasPossibleNextAlertsPage(events);

  const visibleEvents = events.filter((event) => {
    if (statusFilter !== "all" && event.status !== statusFilter) {
      return false;
    }
    if (severityFilter !== "all" && rulesById[event.ruleId]?.severity !== severityFilter) {
      return false;
    }
    return true;
  });

  return (
    <section>
      <PageHeader title="Alertas" description="Eventos abertos e resolvidos a partir das regras deste workspace." />

      {workspaceId ? <AlertsSectionTabs workspaceId={workspaceId} /> : null}

      {loading ? <LoadingState compact title="Carregando eventos de alerta" /> : null}

      {!loading && error ? (
        <ErrorState
          title="Não foi possível carregar os eventos de alerta"
          description={getApiErrorMessage(error, "Ocorreu um erro inesperado ao carregar os eventos de alerta.")}
          action={
            token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId, page)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && events.length === 0 ? (
        <EmptyState title="Nenhum evento de alerta" description="Nenhuma regra disparou um evento nesta página." />
      ) : null}

      {!loading && !error && events.length > 0 ? (
        <div className="panel">
          <div className="device-registry-filters">
            <label>
              Estado
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}>
                <option value="all">Todos</option>
                <option value="Firing">Ativo</option>
                <option value="Resolved">Resolvido</option>
                <option value="Closed">Encerrado</option>
              </select>
            </label>
            <label>
              Severidade
              <select
                value={severityFilter}
                onChange={(event) => setSeverityFilter(event.target.value as SeverityFilter)}
              >
                <option value="all">Todas</option>
                <option value="Info">Informativo</option>
                <option value="Warning">Aviso</option>
                <option value="Critical">Crítico</option>
              </select>
            </label>
          </div>
          <p className="muted">Filtro aplicado à página atual. Use Próxima para ver mais eventos.</p>

          {visibleEvents.length ? (
            <div className="table-scroll">
              <table className="alert-rule-table">
                <caption className="visually-hidden">Eventos de alerta neste workspace</caption>
                <thead>
                  <tr>
                    <th scope="col">Dispositivo</th>
                    <th scope="col">Regra</th>
                    <th scope="col">Métrica</th>
                    <th scope="col">Severidade</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Ocorrido em</th>
                    <th scope="col">
                      <span className="visually-hidden">Ações</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visibleEvents.map((event) => {
                    const rule = rulesById[event.ruleId];
                    return (
                      <tr key={event.id}>
                        <td>{event.deviceIdentifier}</td>
                        <td>{rule?.name ?? event.ruleId}</td>
                        <td>{rule ? (metricNames[rule.metricDefinitionId] ?? rule.metricDefinitionId) : "—"}</td>
                        <td>{rule ? <AlertSeverityBadge severity={rule.severity} /> : "—"}</td>
                        <td>
                          <AlertEventStatusBadge status={event.status} />
                        </td>
                        <td>{formatTimestampWithZone(event.triggeredAtUtc)}</td>
                        <td>
                          {workspaceId ? (
                            <Link className="button-secondary" to={`/workspaces/${workspaceId}/alerts/events/${event.id}`}>
                              Ver histórico
                            </Link>
                          ) : null}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyState compact title="Nenhum evento corresponde ao filtro" description="Ajuste o estado ou a severidade." />
          )}

          <nav className="pagination" aria-label="Paginação dos eventos de alerta">
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
