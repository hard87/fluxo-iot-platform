import { Link } from "react-router-dom";

interface AppHeaderProps {
  userEmail?: string;
  onLogout: () => void;
}

export function AppHeader({ userEmail, onLogout }: AppHeaderProps) {
  return (
    <div className="app-header-bar">
      <Link className="app-brand" to="/workspaces" aria-label="Fluxo Portal — ir para workspaces">
        <span className="app-brand-name">Fluxo</span>
        <span className="app-product-name">Portal IoT</span>
      </Link>

      <div className="app-header-account">
        {userEmail ? (
          <span className="user-pill" title={userEmail}>
            <span className="user-pill-label">Usuário</span>
            <span className="user-pill-email">{userEmail}</span>
          </span>
        ) : null}
        <button type="button" onClick={onLogout} className="header-logout-button">
          Sair
        </button>
      </div>
    </div>
  );
}
