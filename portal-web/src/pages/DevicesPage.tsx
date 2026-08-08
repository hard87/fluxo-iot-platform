import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { DeviceStatusBadge } from "../components/DeviceStatusBadge";
import { PageHeader } from "../components/PageHeader";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import { ApiError } from "../services/api/httpClient";
import type { DeviceResponse } from "../types";

function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "Ocorreu um erro inesperado.";
}

export function DevicesPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();
  const [devices, setDevices] = useState<DeviceResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const load = useCallback(async (authToken: string, currentWorkspaceId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await deviceService.listDevices(authToken, currentWorkspaceId);
      if (isMountedRef.current) {
        setDevices(response);
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

    void load(token, workspaceId);
  }, [token, workspaceId, load]);

  return (
    <section>
      <PageHeader title="Dispositivos" />
      {workspaceId ? (
        <div className="inline-actions">
          <Link className="button-link" to={`/workspaces/${workspaceId}/devices/new`}>
            Cadastrar novo dispositivo
          </Link>
          <Link className="button-link secondary" to={`/workspaces/${workspaceId}/dashboard`}>
            Voltar para dashboard
          </Link>
        </div>
      ) : null}

      {loading ? <LoadingState compact title="Carregando dispositivos" /> : null}

      {!loading && error ? (
        <ErrorState
          title="Não foi possível carregar os dispositivos"
          description={errorMessage(error)}
          action={
            token && workspaceId ? (
              <button type="button" className="button-secondary" onClick={() => void load(token, workspaceId)}>
                Tentar novamente
              </button>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && devices.length === 0 ? (
        <EmptyState
          title="Nenhum dispositivo cadastrado"
          description="Cadastre um dispositivo para começar a receber telemetria neste workspace."
          action={
            workspaceId ? (
              <Link className="button-link" to={`/workspaces/${workspaceId}/devices/new`}>
                Cadastrar dispositivo
              </Link>
            ) : undefined
          }
        />
      ) : null}

      {!loading && !error && devices.length > 0 ? (
        <ul className="list">
          {devices.map((device) => (
            <li key={device.id} className="list-item">
              <div>
                <strong>{device.name}</strong>
                <p className="muted">{device.identifier}</p>
                <DeviceStatusBadge status={device.operationalStatus} />
              </div>
              {workspaceId ? (
                <Link className="button-secondary" to={`/workspaces/${workspaceId}/devices/${device.id}`}>
                  Detalhes
                </Link>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
