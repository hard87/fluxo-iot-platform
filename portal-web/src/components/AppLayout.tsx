import { Link, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <p className="brand">Fluxo Portal MVP</p>
          <small className="subtitle">IoT workspace controlado</small>
        </div>
        <div className="topbar-right">
          <span className="user-pill">{user?.email}</span>
          <button type="button" onClick={handleLogout} className="button-secondary">
            Sair
          </button>
        </div>
      </header>
      <nav className="nav-links">
        <Link to="/workspaces">Workspaces</Link>
        <Link to="/status">Saude da plataforma</Link>
      </nav>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
