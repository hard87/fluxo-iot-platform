import { useEffect } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import { useWorkspaceSelection } from "../hooks/useWorkspaceSelection";
import { AppHeader } from "./AppHeader";
import { AppNavigation } from "./AppNavigation";
import { MainContent } from "./MainContent";

export function AppShell() {
  const { user, logout } = useAuth();
  const { workspaceId: selectedWorkspaceId, setWorkspaceId } = useWorkspaceSelection();
  const location = useLocation();
  const navigate = useNavigate();
  const routeWorkspaceId = location.pathname.match(/^\/workspaces\/([^/]+)/)?.[1] ?? null;
  const workspaceId = routeWorkspaceId ?? selectedWorkspaceId;

  useEffect(() => {
    if (routeWorkspaceId && routeWorkspaceId !== selectedWorkspaceId) {
      setWorkspaceId(routeWorkspaceId);
    }
  }, [routeWorkspaceId, selectedWorkspaceId, setWorkspaceId]);

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Pular para o conteúdo
      </a>
      <header className="app-header">
        <div className="app-shell-container">
          <AppHeader userEmail={user?.email} onLogout={handleLogout} />
          <AppNavigation workspaceId={workspaceId} />
        </div>
      </header>
      <MainContent>
        <Outlet />
      </MainContent>
    </div>
  );
}
