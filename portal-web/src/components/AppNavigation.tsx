import { NavLink } from "react-router-dom";

interface AppNavigationProps {
  workspaceId: string | null;
}

interface NavigationItem {
  label: string;
  to: string | null;
  end?: boolean;
  requiresWorkspace?: boolean;
}

export function AppNavigation({ workspaceId }: AppNavigationProps) {
  const items: NavigationItem[] = [
    { label: "Workspaces", to: "/workspaces", end: true },
    {
      label: "Dashboard",
      to: workspaceId ? `/workspaces/${workspaceId}/dashboard` : null,
      end: true,
      requiresWorkspace: true
    },
    {
      label: "Dispositivos",
      to: workspaceId ? `/workspaces/${workspaceId}/devices` : null,
      requiresWorkspace: true
    },
    {
      label: "Alertas",
      to: workspaceId ? `/workspaces/${workspaceId}/alerts` : null,
      end: true,
      requiresWorkspace: true
    },
    {
      label: "Telemetry Explorer",
      to: workspaceId ? `/workspaces/${workspaceId}/explorer` : null,
      end: true,
      requiresWorkspace: true
    },
    {
      label: "Rejeições",
      to: workspaceId ? `/workspaces/${workspaceId}/rejections` : null,
      end: true,
      requiresWorkspace: true
    },
    { label: "Saúde da plataforma", to: "/status", end: true }
  ];

  return (
    <nav className="app-navigation" aria-label="Navegação principal">
      {!workspaceId ? (
        <span id="workspace-navigation-help" className="visually-hidden">
          Selecione um workspace para acessar as páginas deste contexto.
        </span>
      ) : null}
      <div className="app-navigation-list">
        {items.map((item) =>
          item.to ? (
            <NavLink
              key={item.label}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `app-navigation-link${isActive ? " is-active" : ""}`}
            >
              {item.label}
            </NavLink>
          ) : (
            <span
              key={item.label}
              className="app-navigation-link is-disabled"
              aria-disabled="true"
              aria-describedby={item.requiresWorkspace ? "workspace-navigation-help" : undefined}
              title={item.requiresWorkspace ? "Selecione um workspace para acessar" : undefined}
            >
              {item.label}
            </span>
          )
        )}
      </div>
    </nav>
  );
}
