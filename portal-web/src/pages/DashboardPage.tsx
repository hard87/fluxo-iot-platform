import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { DashboardKpiCard } from "../components/dashboard/DashboardKpiCard";
import { TelemetryActivity } from "../components/dashboard/TelemetryActivity";
import {
  dashboardErrorSummary,
  getWorkspaceHealthSummary,
  WorkspaceHealthStatus
} from "../components/dashboard/WorkspaceHealthStatus";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import * as workspaceService from "../services/api/workspaceService";
import type { DashboardResponse, Workspace } from "../types";

export function DashboardPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
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
      setDashboard(null);
      setWorkspace(null);

      try {
        const [response, workspaces] = await Promise.all([
          deviceService.getDashboard(authToken, currentWorkspaceId),
          workspaceService.listWorkspaces(authToken).catch(() => [])
        ]);
        if (isMounted) {
          setDashboard(response);
          setWorkspace(workspaces.find((item) => item.id === currentWorkspaceId) ?? null);
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

  const workspaceName = workspace?.name ?? (workspaceId ? `Workspace ${workspaceId.slice(0, 8)}` : "Workspace");
  const tenantId = dashboard?.tenantId ?? workspace?.tenantId;
  const healthSummary = dashboard ? getWorkspaceHealthSummary(dashboard) : error ? dashboardErrorSummary : null;

  return (
    <section className="dashboard-page">
      <PageHeader
        title="Dashboard"
        description={tenantId ? `${workspaceName} · Tenant ${tenantId}` : workspaceName}
        actions={workspaceId ? (
          <>
            <Link className="button-link" to={`/workspaces/${workspaceId}/devices`}>
              Ver dispositivos
            </Link>
            <Link className="button-link secondary" to={`/workspaces/${workspaceId}/explorer`}>
              Explorar telemetria
            </Link>
          </>
        ) : undefined}
      />
      <ApiErrorMessage error={error} />
      {loading ? <div className="panel dashboard-loading" role="status">Carregando visão operacional...</div> : null}
      {dashboard ? (
        <div className="dashboard-overview">
          <section className="dashboard-section" aria-labelledby="dashboard-kpis-title">
            <div className="dashboard-section-heading">
              <h2 id="dashboard-kpis-title">Indicadores</h2>
              <p>Resumo atual fornecido pelo workspace.</p>
            </div>
            <div className="dashboard-kpi-grid">
              <DashboardKpiCard label="Dispositivos" value={dashboard.devicesTotal} hint="Total cadastrado" />
              <DashboardKpiCard label="Online" value={dashboard.devicesOnline} hint="Com status online" tone="success" />
              <DashboardKpiCard
                label="Offline"
                value={dashboard.devicesOffline}
                hint={dashboard.devicesOffline > 0 ? "Requer atenção" : "Nenhum offline"}
                tone={dashboard.devicesOffline > 0 ? "danger" : "neutral"}
              />
              <DashboardKpiCard
                label="Unknown"
                value={dashboard.devicesUnknown}
                hint={dashboard.devicesUnknown > 0 ? "Estado não confirmado" : "Nenhum desconhecido"}
                tone={dashboard.devicesUnknown > 0 ? "warning" : "neutral"}
              />
              <DashboardKpiCard
                label="Mensagens processadas"
                value={dashboard.messagesProcessed}
                hint="Total informado"
                tone="info"
              />
              <DashboardKpiCard
                label="Mensagens rejeitadas"
                value={dashboard.messagesRejected}
                hint={dashboard.messagesRejected > 0 ? "Há rejeições" : "Nenhuma rejeição"}
                tone={dashboard.messagesRejected > 0 ? "danger" : "neutral"}
              />
            </div>
          </section>

          <section className="dashboard-section" aria-labelledby="dashboard-activity-title">
            <div className="dashboard-section-heading">
              <h2 id="dashboard-activity-title">Atividade e situação</h2>
              <p>Leitura rápida dos sinais disponíveis.</p>
            </div>
            <div className="dashboard-activity-grid">
              <TelemetryActivity timestamp={dashboard.lastTelemetryReceivedAtUtc} />
              <article className="panel dashboard-health-card">
                <div className="dashboard-health-heading">
                  <div>
                    <p className="dashboard-card-eyebrow">Situação</p>
                    <h2>Visão operacional</h2>
                  </div>
                  <WorkspaceHealthStatus summary={healthSummary!} />
                </div>
                <p>{healthSummary!.detail}</p>
                <small>Síntese visual dos indicadores acima; não substitui a saúde da plataforma.</small>
              </article>
            </div>
          </section>
        </div>
      ) : null}
      {!loading && error ? (
        <article className="panel dashboard-health-card dashboard-health-card-error">
          <div className="dashboard-health-heading">
            <h2>Visão operacional</h2>
            <WorkspaceHealthStatus summary={dashboardErrorSummary} />
          </div>
          <p>{dashboardErrorSummary.detail}</p>
        </article>
      ) : null}
    </section>
  );
}
