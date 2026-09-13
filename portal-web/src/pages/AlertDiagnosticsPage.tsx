import { useCallback, useEffect, useRef, useState } from "react";
import { useParams } from "react-router-dom";
import {
  alertDiagnosticReasonLabel,
  alertDiagnosticStatusBadgeClass,
  alertDiagnosticStatusLabel
} from "../components/alerts/alertDiagnosticPresentation";
import { formatTimestampWithZone } from "../components/alerts/alertEventPresentation";
import { AlertsSectionTabs } from "../components/alerts/AlertsSectionTabs";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertEventsService from "../services/api/alertEventsService";
import * as alertRulesService from "../services/api/alertRulesService";
import { ApiError } from "../services/api/httpClient";
import type { AlertAttemptDiagnostic, AlertRuleRevision } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

export function AlertDiagnosticsPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();

  const [diagnostics, setDiagnostics] = useState<AlertAttemptDiagnostic[]>([]);
  const [rulesById, setRulesById] = useState<Record<string, AlertRuleRevision>>({});
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

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
      const response = await alertEventsService.listAlertDiagnostics(authToken, currentWorkspaceId, currentPage);
      if (isMountedRef.current) {
        setDiagnostics(response);
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
    alertRulesService
      .listAllAlertRules(token, workspaceId)
      .then((rules) => {
        if (cancelled) {
          return;
        }
        const byRuleId: Record<string, AlertRuleRevision> = {};
        for (const rule of rules) {
          byRuleId[rule.ruleId] = rule;
        }
        setRulesById(byRuleId);
      })
      .catch(() => {
        // Rule-name enrichment is presentation-only: on failure, rows fall back to the raw ruleId.
      });
    return () => {
      cancelled = true;
    };
  }, [token, workspaceId]);

  const hasNextPage = alertRulesService.hasPossibleNextAlertsPage(diagnostics);
  const isForbidden = error instanceof ApiError && error.status === 403;

  return (
    <section>
      <PageHeader
        title="Alertas"
        description="Falhas na avaliação de regras registradas pelo motor de alertas neste workspace."
      />

      {workspaceId ? <AlertsSectionTabs workspaceId={workspaceId} /> : null}

      <p className="muted">
        Esta tela mostra apenas tentativas de avaliação com falha ou esgotadas (dead letter). Ela não
        reflete o estado de entrega por canal de notificação (e-mail, webhook), que ainda não existe
        neste contrato.
      </p>

      {loading ? <LoadingState compact title="Carregando diagnóstico de alertas" /> : null}

      {!loading && error ? (
        <ErrorState
          title={isForbidden ? "Acesso restrito a administradores" : "Não foi possível carregar o diagnóstico"}
          description={
            isForbidden
              ? "Você precisa ter o papel de Admin neste workspace para ver o diagnóstico de entrega."
              : getApiErrorMessage(error, "Ocorreu um erro inesperado ao carregar o diagnóstico de alertas.")
          }
          action={
            !isForbidden && token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId, page)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && diagnostics.length === 0 ? (
        <EmptyState
          title="Nenhuma falha registrada"
          description="Nenhuma tentativa de avaliação falhou ou esgotou as tentativas nesta página."
        />
      ) : null}

      {!loading && !error && diagnostics.length > 0 ? (
        <div className="panel">
          <div className="table-scroll">
            <table className="alert-rule-table">
              <caption className="visually-hidden">Falhas de avaliação de alertas neste workspace</caption>
              <thead>
                <tr>
                  <th scope="col">Regra</th>
                  <th scope="col">Dispositivo</th>
                  <th scope="col">Estado</th>
                  <th scope="col">Tentativas</th>
                  <th scope="col">Próxima tentativa</th>
                  <th scope="col">Motivo</th>
                </tr>
              </thead>
              <tbody>
                {diagnostics.map((diagnostic) => {
                  const rule = rulesById[diagnostic.ruleId];
                  return (
                    <tr key={diagnostic.id}>
                      <td>{rule?.name ?? diagnostic.ruleId}</td>
                      <td>{diagnostic.deviceIdentifier}</td>
                      <td>
                        <span className={`status-badge ${alertDiagnosticStatusBadgeClass(diagnostic.status)}`}>
                          {alertDiagnosticStatusLabel(diagnostic.status)}
                        </span>
                      </td>
                      <td>{diagnostic.attemptCount}</td>
                      <td>
                        {diagnostic.status === "Failed" && diagnostic.nextAttemptAtUtc
                          ? formatTimestampWithZone(diagnostic.nextAttemptAtUtc)
                          : "Não haverá nova tentativa"}
                      </td>
                      <td>{alertDiagnosticReasonLabel(diagnostic.reason)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label="Paginação do diagnóstico de alertas">
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
