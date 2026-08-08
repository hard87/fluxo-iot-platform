import type { DashboardResponse } from "../../types";

export type WorkspaceHealthState = "healthy" | "warning" | "offline" | "unknown" | "error";

export interface WorkspaceHealthSummary {
  state: WorkspaceHealthState;
  label: string;
  detail: string;
}

export const dashboardErrorSummary: WorkspaceHealthSummary = {
  state: "error",
  label: "Dados indisponíveis",
  detail: "Não foi possível avaliar a situação do workspace com os dados atuais."
};

export function getWorkspaceHealthSummary(dashboard: DashboardResponse): WorkspaceHealthSummary {
  if (dashboard.devicesOffline > 0) {
    return {
      state: "offline",
      label: "Offline detectado",
      detail: `${dashboard.devicesOffline} dispositivo${dashboard.devicesOffline === 1 ? " está" : "s estão"} offline e requer atenção.`
    };
  }

  if (dashboard.devicesTotal === 0) {
    return {
      state: "unknown",
      label: "Sem dispositivos",
      detail: "Ainda não há dispositivos para avaliar neste workspace."
    };
  }

  if (dashboard.devicesUnknown > 0) {
    return {
      state: "unknown",
      label: "Estado incompleto",
      detail: `${dashboard.devicesUnknown} dispositivo${dashboard.devicesUnknown === 1 ? " está" : "s estão"} com estado desconhecido.`
    };
  }

  if (dashboard.messagesRejected > 0) {
    return {
      state: "warning",
      label: "Atenção necessária",
      detail: `${dashboard.messagesRejected} mensage${dashboard.messagesRejected === 1 ? "m foi rejeitada" : "ns foram rejeitadas"} no total informado.`
    };
  }

  if (!dashboard.lastTelemetryReceivedAtUtc) {
    return {
      state: "unknown",
      label: "Sem telemetria",
      detail: "Não há telemetria recente disponível para completar a avaliação."
    };
  }

  return {
    state: "healthy",
    label: "Operação normal",
    detail: "Os indicadores disponíveis não mostram dispositivos offline, estados desconhecidos ou mensagens rejeitadas."
  };
}

export function WorkspaceHealthStatus({ summary }: { summary: WorkspaceHealthSummary }) {
  return (
    <span className="workspace-health-status" data-state={summary.state}>
      <span className="workspace-health-dot" aria-hidden="true" />
      {summary.label}
    </span>
  );
}
