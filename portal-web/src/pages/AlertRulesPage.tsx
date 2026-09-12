import { useCallback, useEffect, useRef, useState } from "react";
import { useParams } from "react-router-dom";
import { AlertRuleStateBadge } from "../components/alerts/AlertRuleStateBadge";
import { AlertSeverityBadge } from "../components/alerts/AlertSeverityBadge";
import { alertConditionLabel, alertScopeLabel } from "../components/alerts/alertRulePresentation";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertRulesService from "../services/api/alertRulesService";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertRuleRevision } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

export function AlertRulesPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();

  const [rules, setRules] = useState<AlertRuleRevision[]>([]);
  const [metricNames, setMetricNames] = useState<Record<string, string>>({});
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [togglingRuleId, setTogglingRuleId] = useState<string | null>(null);
  const [toggleError, setToggleError] = useState<unknown>(null);

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
      const response = await alertRulesService.listAlertRules(authToken, currentWorkspaceId, currentPage);
      if (isMountedRef.current) {
        setRules(response);
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
    telemetryService
      .listMetricDefinitions(token, workspaceId)
      .then((definitions) => {
        if (cancelled) {
          return;
        }
        const names: Record<string, string> = {};
        for (const definition of definitions) {
          names[definition.id] = definition.displayName;
        }
        setMetricNames(names);
      })
      .catch(() => {
        // The metric catalog is presentation-only here: on failure, rows just fall back to the raw id.
      });
    return () => {
      cancelled = true;
    };
  }, [token, workspaceId]);

  async function handleToggle(rule: AlertRuleRevision) {
    if (!token || !workspaceId) {
      return;
    }
    setTogglingRuleId(rule.ruleId);
    setToggleError(null);
    try {
      const updated = await alertRulesService.updateAlertRule(token, workspaceId, rule.ruleId, {
        name: rule.name,
        metricDefinitionId: rule.metricDefinitionId,
        deviceIdentifier: rule.deviceIdentifier,
        operator: rule.operator,
        threshold: rule.threshold,
        thresholdHigh: rule.thresholdHigh,
        hysteresis: rule.hysteresis,
        durationSeconds: rule.durationSeconds,
        cooldownSeconds: rule.cooldownSeconds,
        severity: rule.severity,
        enabled: !rule.enabled,
        expectedVersion: rule.version
      });
      if (isMountedRef.current) {
        setRules((current) => current.map((item) => (item.ruleId === updated.ruleId ? updated : item)));
      }
    } catch (err) {
      if (isMountedRef.current) {
        setToggleError(err);
      }
    } finally {
      if (isMountedRef.current) {
        setTogglingRuleId(null);
      }
    }
  }

  const hasNextPage = alertRulesService.hasPossibleNextAlertsPage(rules);

  return (
    <section>
      <PageHeader
        title="Alertas"
        description="Regras configuradas para avaliar telemetria e abrir eventos neste workspace."
      />

      {loading ? <LoadingState compact title="Carregando regras de alerta" /> : null}

      {!loading && error ? (
        <ErrorState
          title="Não foi possível carregar as regras de alerta"
          description={getApiErrorMessage(error, "Ocorreu um erro inesperado ao carregar as regras de alerta.")}
          action={
            token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId, page)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && rules.length === 0 ? (
        <EmptyState
          title="Nenhuma regra de alerta cadastrada"
          description="Nenhuma regra foi configurada ainda neste workspace."
        />
      ) : null}

      {!loading && !error && rules.length > 0 ? (
        <div className="panel">
          {toggleError ? (
            <ErrorState
              compact
              title="Não foi possível atualizar a regra"
              description={getApiErrorMessage(
                toggleError,
                "Ocorreu um erro inesperado ao atualizar a regra. Recarregue a página e tente novamente."
              )}
            />
          ) : null}
          <div className="table-scroll">
            <table className="alert-rule-table">
              <caption className="visually-hidden">Regras de alerta configuradas neste workspace</caption>
              <thead>
                <tr>
                  <th scope="col">Nome</th>
                  <th scope="col">Métrica</th>
                  <th scope="col">Escopo</th>
                  <th scope="col">Condição</th>
                  <th scope="col">Severidade</th>
                  <th scope="col">Estado</th>
                  <th scope="col">
                    <span className="visually-hidden">Ações</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {rules.map((rule) => (
                  <tr key={rule.id}>
                    <td>{rule.name}</td>
                    <td>{metricNames[rule.metricDefinitionId] ?? rule.metricDefinitionId}</td>
                    <td>{alertScopeLabel(rule.deviceIdentifier)}</td>
                    <td>{alertConditionLabel(rule)}</td>
                    <td>
                      <AlertSeverityBadge severity={rule.severity} />
                    </td>
                    <td>
                      <AlertRuleStateBadge enabled={rule.enabled} />
                    </td>
                    <td>
                      <button
                        type="button"
                        className="button-secondary"
                        disabled={togglingRuleId === rule.ruleId}
                        onClick={() => void handleToggle(rule)}
                      >
                        {rule.enabled ? "Desativar" : "Ativar"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label="Paginação das regras de alerta">
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
