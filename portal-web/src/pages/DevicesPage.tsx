import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");
  const visibleDevices = useMemo(() => devices.filter((device) => {
    const matchesText = `${device.name} ${device.identifier} ${device.category}`.toLocaleLowerCase("pt-BR").includes(query.trim().toLocaleLowerCase("pt-BR"));
    const normalizedStatus = typeof device.operationalStatus === "string" ? device.operationalStatus.toLowerCase() : String(device.operationalStatus);
    return matchesText && (status === "all" || normalizedStatus === status);
  }), [devices, query, status]);

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
        <div className="device-registry panel">
          <div className="device-registry-filters">
            <label>Buscar dispositivo<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Nome, identificador ou categoria" /></label>
            <label>Estado<select value={status} onChange={(event) => setStatus(event.target.value)}><option value="all">Todos</option><option value="online">Online</option><option value="offline">Offline</option><option value="unknown">Desconhecido</option></select></label>
          </div>
          {visibleDevices.length ? <div className="table-scroll"><table className="device-table"><caption className="visually-hidden">Dispositivos cadastrados neste workspace</caption><thead><tr><th scope="col">Dispositivo</th><th scope="col">Categoria</th><th scope="col">Estado</th><th scope="col">Última telemetria</th><th scope="col"><span className="visually-hidden">Ações</span></th></tr></thead><tbody>{visibleDevices.map((device) => <tr key={device.id}><td><strong>{device.name}</strong><code>{device.identifier}</code></td><td>{device.category}</td><td><DeviceStatusBadge status={device.operationalStatus} /></td><td>{device.lastTelemetryReceivedAtUtc ? <time dateTime={device.lastTelemetryReceivedAtUtc}>{new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(device.lastTelemetryReceivedAtUtc))}</time> : "Sem telemetria"}</td><td>{workspaceId ? <Link className="button-secondary" to={`/workspaces/${workspaceId}/devices/${device.id}`}>Abrir</Link> : null}</td></tr>)}</tbody></table></div> : <EmptyState compact title="Nenhum dispositivo encontrado" description="Revise a busca ou o filtro de estado." />}
        </div>
      ) : null}
    </section>
  );
}
