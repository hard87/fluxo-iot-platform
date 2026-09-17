import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AlertRuleStateBadge } from "../components/alerts/AlertRuleStateBadge";
import { AlertSeverityBadge } from "../components/alerts/AlertSeverityBadge";
import { AlertsSectionTabs } from "../components/alerts/AlertsSectionTabs";
import { alertConditionLabel, alertScopeLabel } from "../components/alerts/alertRulePresentation";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import "../styles/alerts.css";
import { Timestamp } from "../components/Timestamp";
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
  const [status, setStatus] = useState<"current" | "archived">("current");
  const [archiveCandidate, setArchiveCandidate] = useState<AlertRuleRevision | null>(null);
  const [archiving, setArchiving] = useState(false);
  const [notice, setNotice] = useState("");
  const [historyRule, setHistoryRule] = useState<AlertRuleRevision | null>(null);
  const [revisions, setRevisions] = useState<AlertRuleRevision[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<unknown>(null);
  const historyRequestRef = useRef(0);
  const loadRequestRef = useRef(0);
  const [historyPage, setHistoryPage] = useState(1);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const load = useCallback(async (authToken: string, currentWorkspaceId: string, currentPage: number) => {
    const requestId = ++loadRequestRef.current;
    setLoading(true);
    setError(null);
    try {
      const response = status === "current"
        ? await alertRulesService.listAlertRules(authToken, currentWorkspaceId, currentPage)
        : await alertRulesService.listAlertRules(authToken, currentWorkspaceId, currentPage, undefined, status);
      if (isMountedRef.current && requestId === loadRequestRef.current) {
        setRules(response);
      }
    } catch (err) {
      if (isMountedRef.current && requestId === loadRequestRef.current) {
        setError(err);
      }
    } finally {
      if (isMountedRef.current && requestId === loadRequestRef.current) {
        setLoading(false);
      }
    }
  }, [status]);

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
      // Recipients live in a separate table (NotificationSubscription), not on
      // AlertRuleRevision, and a save with no portalRecipientUserIds replaces the current
      // subscriptions with none. Fetch and resend the current list so toggling enabled/disabled
      // never silently wipes portal notification recipients.
      const portalRecipientUserIds = await alertRulesService.getPortalRecipients(token, workspaceId, rule.ruleId);
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
        expectedVersion: rule.version,
        portalRecipientUserIds
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

  async function handleArchive() {
    if (!token || !workspaceId || !archiveCandidate || archiving) return;
    setArchiving(true);
    setToggleError(null);
    try {
      await alertRulesService.archiveAlertRule(token, workspaceId, archiveCandidate.ruleId, archiveCandidate.version);
      if (!isMountedRef.current) return;
      setArchiveCandidate(null);
      setNotice(`Regra “${archiveCandidate.name}” arquivada. O histórico foi preservado.`);
      await load(token, workspaceId, page);
    } catch (err) {
      if (isMountedRef.current) setToggleError(err);
    } finally {
      if (isMountedRef.current) setArchiving(false);
    }
  }

  async function showHistory(rule: AlertRuleRevision, revisionPage = 1) {
    if (!token || !workspaceId) return;
    const requestId = ++historyRequestRef.current;
    setHistoryPage(revisionPage);
    setHistoryRule(rule); setRevisions([]); setHistoryLoading(true); setHistoryError(null);
    try {
      const result = await alertRulesService.listAlertRuleRevisions(token, workspaceId, rule.ruleId, revisionPage);
      if (isMountedRef.current && requestId === historyRequestRef.current) setRevisions(result);
    } catch (err) {
      if (isMountedRef.current && requestId === historyRequestRef.current) setHistoryError(err);
    } finally {
      if (isMountedRef.current && requestId === historyRequestRef.current) setHistoryLoading(false);
    }
  }

  return (
    <section className="alert-rules-page">
      <PageHeader
        title="Alertas"
        description="Regras configuradas para avaliar telemetria e abrir eventos neste workspace."
      />

      {workspaceId ? (
        <>
          <AlertsSectionTabs workspaceId={workspaceId} />
          <div className="inline-actions">
            <Link className="button-link" to={`/workspaces/${workspaceId}/alerts/new`}>
              Criar regra
            </Link>
          </div>
        </>
      ) : null}

      <div className="inline-actions" aria-label="Estado das regras">
        <button type="button" className="button-secondary" disabled={archiving || togglingRuleId !== null} aria-pressed={status === "current"} onClick={() => { setStatus("current"); setPage(1); setArchiveCandidate(null); setToggleError(null); }}>Em uso</button>
        <button type="button" className="button-secondary" disabled={archiving || togglingRuleId !== null} aria-pressed={status === "archived"} onClick={() => { setStatus("archived"); setPage(1); setArchiveCandidate(null); setToggleError(null); }}>Arquivadas</button>
      </div>
      {notice ? <p role="status">{notice}</p> : null}
      {archiveCandidate ? (
        <div className="panel" role="alertdialog" aria-labelledby="archive-title" aria-describedby="archive-description">
          <h2 id="archive-title">Arquivar “{archiveCandidate.name}”?</h2>
          <p id="archive-description">A regra deixará de avaliar medições e seus eventos ativos serão encerrados. As revisões e o histórico serão preservados. A regra arquivada não poderá ser reativada.</p>
          <div className="inline-actions">
            <button type="button" disabled={archiving} onClick={() => void handleArchive()}>{archiving ? "Arquivando…" : "Confirmar arquivamento"}</button>
            <button type="button" className="button-secondary" disabled={archiving} onClick={() => setArchiveCandidate(null)}>Cancelar</button>
          </div>
        </div>
      ) : null}
      {toggleError ? <ErrorState compact title="Não foi possível atualizar a regra" description={getApiErrorMessage(toggleError, "Recarregue a lista e tente novamente.")} action={token && workspaceId ? <button type="button" className="button-secondary" disabled={archiving} onClick={() => { setArchiveCandidate(null); setToggleError(null); void load(token, workspaceId, page); }}>Recarregar lista</button> : undefined} /> : null}
      {historyRule ? (
        <div className="panel alert-rule-history" aria-label="Histórico de revisões">
          <h2>Revisões de “{historyRule.name}”</h2>
          <button type="button" className="button-secondary" onClick={() => { ++historyRequestRef.current; setHistoryRule(null); }}>Fechar histórico</button>
          {historyLoading ? <LoadingState compact title="Carregando revisões" /> : null}
          {historyError ? <ErrorState compact title="Não foi possível carregar as revisões" description={getApiErrorMessage(historyError, "Tente novamente.")} action={<button type="button" onClick={() => void showHistory(historyRule, historyPage)}>Tentar novamente</button>} /> : null}
          {!historyLoading && !historyError ? <ol>{revisions.map(revision => <li key={revision.id}>Versão {revision.version} · {revision.archivedAtUtc ? "Arquivada" : revision.enabled ? "Ativa" : "Desativada"} · <Timestamp value={revision.createdAtUtc} /> · Autor: {revision.authorId} · {alertConditionLabel(revision)}</li>)}</ol> : null}
          {!historyLoading && !historyError ? <nav className="pagination" aria-label="Paginação das revisões"><button type="button" disabled={historyPage <= 1} onClick={() => void showHistory(historyRule, historyPage - 1)}>Revisões anteriores</button><span>Página {historyPage}</span><button type="button" disabled={!alertRulesService.hasPossibleNextAlertsPage(revisions)} onClick={() => void showHistory(historyRule, historyPage + 1)}>Próximas revisões</button></nav> : null}
        </div>
      ) : null}

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
          title={status === "archived" ? "Nenhuma regra arquivada" : "Nenhuma regra de alerta cadastrada"}
          description={status === "archived" ? "As regras arquivadas aparecerão aqui com seu histórico preservado." : "Nenhuma regra foi configurada ainda neste workspace."}
        />
      ) : null}

      {!loading && !error && rules.length > 0 ? (
        <div className="panel">
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
                      {rule.archivedAtUtc ? <span>Arquivada em <Timestamp value={rule.archivedAtUtc} /> · Autor: {rule.authorId}</span> : <AlertRuleStateBadge enabled={rule.enabled} />}
                    </td>
                    <td>
                      <div className="inline-actions">
                        {workspaceId && !rule.archivedAtUtc ? (
                          <Link
                            className="button-secondary"
                            to={`/workspaces/${workspaceId}/alerts/${rule.ruleId}/edit`}
                            state={{ rule }}
                          >
                            Editar
                          </Link>
                        ) : null}
                        {!rule.archivedAtUtc ? <><button
                          type="button"
                          className="button-secondary"
                          disabled={togglingRuleId !== null || archiving || archiveCandidate !== null}
                          onClick={() => void handleToggle(rule)}
                        >
                          {rule.enabled ? "Desativar" : "Ativar"}
                        </button>
                        <button type="button" className="button-secondary" disabled={togglingRuleId !== null || archiving || archiveCandidate !== null} onClick={() => { setArchiveCandidate(rule); setToggleError(null); setNotice(""); }}>Arquivar</button></> : null}
                        <button type="button" className="button-secondary" onClick={() => void showHistory(rule)}>Ver revisões</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label="Paginação das regras de alerta">
            <button type="button" disabled={page <= 1 || archiving || archiveCandidate !== null} onClick={() => setPage((current) => current - 1)}>
              Anterior
            </button>
            <span>Página {page}</span>
            <button type="button" disabled={!hasNextPage || archiving || archiveCandidate !== null} onClick={() => setPage((current) => current + 1)}>
              Próxima
            </button>
          </nav>
        </div>
      ) : null}
    </section>
  );
}
