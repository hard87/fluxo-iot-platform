import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import type { DashboardResponse } from "../types";

export function DashboardPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token || !workspaceId) {
      return;
    }

    const authToken: string = token;
    const currentWorkspaceId: string = workspaceId;

    let isMounted = true;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        const response = await deviceService.getDashboard(authToken, currentWorkspaceId);
        if (isMounted) {
          setDashboard(response);
        }
      } catch (err) {
        if (isMounted) {
          setError(err);
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      isMounted = false;
    };
  }, [token, workspaceId]);

  return (
    <section>
      <h1>Dashboard do workspace</h1>
      <ApiErrorMessage error={error} />
      {loading ? <p>Carregando dashboard...</p> : null}
      {dashboard ? (
        <div className="panel-grid">
          <article className="panel metric-card">
            <p>Dispositivos</p>
            <strong>{dashboard.devicesTotal}</strong>
          </article>
          <article className="panel metric-card">
            <p>Online</p>
            <strong>{dashboard.devicesOnline}</strong>
          </article>
          <article className="panel metric-card">
            <p>Offline</p>
            <strong>{dashboard.devicesOffline}</strong>
          </article>
          <article className="panel metric-card">
            <p>Unknown</p>
            <strong>{dashboard.devicesUnknown}</strong>
          </article>
          <article className="panel metric-card">
            <p>Mensagens processadas</p>
            <strong>{dashboard.messagesProcessed}</strong>
          </article>
          <article className="panel metric-card">
            <p>Mensagens rejeitadas</p>
            <strong>{dashboard.messagesRejected}</strong>
          </article>
          <article className="panel metric-card wide">
            <p>Ultima telemetria</p>
            <strong>{dashboard.lastTelemetryReceivedAtUtc ?? "Sem dados"}</strong>
          </article>
        </div>
      ) : null}
      {workspaceId ? (
        <p>
          <Link to={`/workspaces/${workspaceId}/devices`}>Ir para dispositivos</Link>
        </p>
      ) : null}
    </section>
  );
}
