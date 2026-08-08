import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { StatusBadge } from "../components/StatusBadge";
import { Timestamp } from "../components/Timestamp";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import * as statusService from "../services/api/statusService";
import type { PlatformStatusResponse } from "../types";
import { deriveOverallStatus, healthStatusTone, mapHealthStatus } from "../utils/healthStatus";
import { ApiError } from "../services/api/httpClient";

function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "Ocorreu um erro inesperado.";
}

export function HealthPage() {
  const [status, setStatus] = useState<PlatformStatusResponse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await statusService.getPlatformStatus();
      setStatus(response);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const overallState = status ? deriveOverallStatus(status.components) : null;

  return (
    <section>
      <PageHeader
        title="Saúde da plataforma"
        description="Estado dos componentes cuja saúde pode ser verificada diretamente pela plataforma."
      />

      {loading ? <LoadingState title="Consultando status" description="Verificando os componentes monitorados." /> : null}

      {!loading && error ? (
        <ErrorState
          title="Não foi possível consultar a saúde da plataforma"
          description={errorMessage(error)}
          action={
            <button type="button" className="button-secondary" onClick={() => void load()}>
              Tentar novamente
            </button>
          }
        />
      ) : null}

      {!loading && !error && status ? (
        <article className="panel">
          <div className="dashboard-health-heading">
            <div>
              <h2>Status geral</h2>
              <p className="muted">
                Derivado dos {status.components.length} componente(s) monitorado(s) nesta verificação.
              </p>
            </div>
            {overallState ? <StatusBadge label={overallState} tone={healthStatusTone(overallState)} /> : null}
          </div>
          <p>
            <strong>Verificado em:</strong> <Timestamp value={status.checkedAtUtc} />
          </p>

          {status.components.length === 0 ? (
            <EmptyState
              compact
              title="Nenhum componente monitorado"
              description="Nenhum health check está registrado nesta plataforma no momento. Nenhum status é exibido para evitar informação fictícia."
            />
          ) : (
            <ul className="list">
              {status.components.map((component) => {
                const state = mapHealthStatus(component.status);
                return (
                  <li key={component.name} className="list-item">
                    <div>
                      <strong>{component.name}</strong>
                      <p className="muted">{component.description ?? "Sem descrição disponível"}</p>
                      <p className="muted">
                        Última verificação: <Timestamp value={component.lastCheckedUtc} />
                      </p>
                    </div>
                    <StatusBadge label={state} tone={healthStatusTone(state)} detail={component.status} />
                  </li>
                );
              })}
            </ul>
          )}
        </article>
      ) : null}
    </section>
  );
}
