import { NavLink } from "react-router-dom";

interface Props {
  workspaceId: string;
}

export function AlertsSectionTabs({ workspaceId }: Props) {
  return (
    <div className="view-tabs" role="tablist" aria-label="Seção de alertas">
      <NavLink to={`/workspaces/${workspaceId}/alerts`} end role="tab">
        Regras
      </NavLink>
      <NavLink to={`/workspaces/${workspaceId}/alerts/events`} role="tab">
        Eventos
      </NavLink>
      <NavLink to={`/workspaces/${workspaceId}/alerts/diagnostics`} role="tab">
        Diagnóstico
      </NavLink>
    </div>
  );
}
