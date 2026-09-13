import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { AlertEventStatusBadge } from "../components/alerts/AlertEventStatusBadge";
import { AlertSeverityBadge } from "../components/alerts/AlertSeverityBadge";
import {
  alertTransitionReasonLabel,
  alertTransitionValueLabel,
  formatFreshness,
  formatTimestampWithZone
} from "../components/alerts/alertEventPresentation";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as alertEventsService from "../services/api/alertEventsService";
import * as telemetryService from "../services/api/telemetryService";
import type { AlertHistory } from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

export function AlertEventHistoryPage() {
  const { workspaceId, eventId } = useParams<{ workspaceId: string; eventId: string }>();
  const { token, user } = useAuth();

  const [history, setHistory] = useState<AlertHistory | null>(null);
  const [metricName, setMetricName] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [acknowledging, setAcknowledging] = useState(false);
  const [ackError, setAckError] = useState<unknown>(null);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const load = useCallback(async (authToken: string, wsId: string, id: string) => {
    setLoading(true);
    setError(null);
    try {
      const [historyResult, metrics] = await Promise.all([
        alertEventsService.getAlertHistory(authToken, wsId, id),
        telemetryService.listMetricDefinitions(authToken, wsId)
      ]);
      if (isMountedRef.current) {
        setHistory(historyResult);
        const metric = metrics.find((item) => item.id === historyResult.revision.metricDefinitionId);
        setMetricName(metric?.displayName ?? historyResult.revision.metricDefinitionId);
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
    if (!token || !workspaceId || !eventId) {
      return;
    }
    void load(token, workspaceId, eventId);
  }, [token, workspaceId, eventId, load]);

  const alreadyAcknowledged = Boolean(
    user && history?.acknowledgements.some((acknowledgement) => acknowledgement.authorId === user.userId)
  );

  async function handleAcknowledge() {
    if (!token || !workspaceId || !eventId || !history) {
      return;
    }
    setAcknowledging(true);
    setAckError(null);
    try {
      const acknowledgement = await alertEventsService.acknowledgeAlertEvent(token, workspaceId, eventId);
      if (isMountedRef.current) {
        setHistory((current) => {
          if (!current) {
            return current;
          }
          const alreadyPresent = current.acknowledgements.some((item) => item.id === acknowledgement.id);
          return alreadyPresent
            ? current
            : { ...current, acknowledgements: [...current.acknowledgements, acknowledgement] };
        });
      }
    } catch (err) {
      if (isMountedRef.current) {
        setAckError(err);
      }
    } finally {
      if (isMountedRef.current) {
        setAcknowledging(false);
      }
    }
  }

  if (loading) {
    return (
      <section>
        <PageHeader title="Histórico do evento" />
        <LoadingState compact title="Carregando histórico do evento" />
      </section>
    );
  }

  if (error) {
    return (
      <section>
        <PageHeader title="Histórico do evento" />
        <ErrorState
          title="Não foi possível carregar o histórico"
          description={getApiErrorMessage(error, "Ocorreu um erro inesperado ao carregar o histórico do evento.")}
          action={
            token && workspaceId && eventId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId, eventId)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      </section>
    );
  }

  if (!history) {
    return (
      <section>
        <PageHeader title="Histórico do evento" />
        <EmptyState
          title="Evento não encontrado"
          description="O evento pode ter sido removido ou o link está incorreto."
          action={workspaceId ? <Link to={`/workspaces/${workspaceId}/alerts/events`}>Voltar para Eventos</Link> : undefined}
        />
      </section>
    );
  }

  return (
    <section>
      <PageHeader
        title={history.revision.name}
        description={`Dispositivo ${history.event.deviceIdentifier} · Métrica ${metricName ?? history.revision.metricDefinitionId}`}
      />

      <div className="panel">
        <div className="inline-actions">
          <AlertSeverityBadge severity={history.revision.severity} />
          <AlertEventStatusBadge status={history.event.status} />
        </div>
        <p>
          Disparado em <strong>{formatTimestampWithZone(history.event.triggeredAtUtc)}</strong>
        </p>
      </div>

      <div className="panel">
        <h2>Transições</h2>
        <div className="table-scroll">
          <table className="alert-rule-table">
            <caption className="visually-hidden">Histórico de transições deste evento</caption>
            <thead>
              <tr>
                <th scope="col">Ordem</th>
                <th scope="col">Tipo</th>
                <th scope="col">Ocorrido em</th>
                <th scope="col">Registrado em</th>
                <th scope="col">Atraso de ingestão</th>
                <th scope="col">Valor</th>
                <th scope="col">Motivo</th>
              </tr>
            </thead>
            <tbody>
              {history.transitions.map((transition) => (
                <tr key={transition.id}>
                  <td>{transition.ordinal}</td>
                  <td>
                    <AlertEventStatusBadge status={transition.kind} />
                  </td>
                  <td>{formatTimestampWithZone(transition.occurredAtUtc)}</td>
                  <td>{formatTimestampWithZone(transition.recordedAtUtc)}</td>
                  <td>{formatFreshness(transition.recordedAtUtc, transition.receivedAtUtc)}</td>
                  <td>{alertTransitionValueLabel(transition.numericValue, transition.booleanValue)}</td>
                  <td>{alertTransitionReasonLabel(transition.reason)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="panel">
        <h2>Reconhecimento</h2>
        {history.acknowledgements.length ? (
          <ul>
            {history.acknowledgements.map((acknowledgement) => (
              <li key={acknowledgement.id}>
                {user && acknowledgement.authorId === user.userId ? "Você" : `Usuário ${acknowledgement.authorId}`} em{" "}
                {formatTimestampWithZone(acknowledgement.createdAtUtc)}
              </li>
            ))}
          </ul>
        ) : (
          <p className="muted">Nenhum reconhecimento registrado ainda.</p>
        )}

        {ackError ? (
          <ErrorState
            compact
            title="Não foi possível reconhecer o evento"
            description={getApiErrorMessage(ackError, "Ocorreu um erro inesperado ao reconhecer o evento.")}
          />
        ) : null}

        {alreadyAcknowledged ? (
          <p className="muted">Você já reconheceu este evento.</p>
        ) : (
          <button type="button" disabled={acknowledging} onClick={() => void handleAcknowledge()}>
            {acknowledging ? "Reconhecendo…" : "Reconhecer evento"}
          </button>
        )}
      </div>

      {workspaceId ? (
        <p>
          <Link to={`/workspaces/${workspaceId}/alerts/events`}>Voltar para Eventos</Link>
        </p>
      ) : null}
    </section>
  );
}
