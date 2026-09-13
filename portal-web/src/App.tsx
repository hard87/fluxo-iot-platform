import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { useAuth } from "./hooks/useAuth";
import { AlertRuleFormPage } from "./pages/AlertRuleFormPage";
import { AlertRulesPage } from "./pages/AlertRulesPage";
import { DashboardPage } from "./pages/DashboardPage";
import { DeviceDetailsPage } from "./pages/DeviceDetailsPage";
import { DevicesPage } from "./pages/DevicesPage";
import { HealthPage } from "./pages/HealthPage";
import { LoginPage } from "./pages/LoginPage";
import { NewDevicePage } from "./pages/NewDevicePage";
import { NotFoundPage } from "./pages/NotFoundPage";
import { RegisterPage } from "./pages/RegisterPage";
import { WorkspacePage } from "./pages/WorkspacePage";
import { TelemetryExplorerPage } from "./pages/TelemetryExplorerPage";
import { TelemetryRejectionsPage } from "./pages/TelemetryRejectionsPage";

function HomeRedirect() {
  const { isAuthenticated } = useAuth();
  return <Navigate to={isAuthenticated ? "/workspaces" : "/login"} replace />;
}

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeRedirect />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/workspaces" element={<WorkspacePage />} />
          <Route path="/workspaces/:workspaceId/dashboard" element={<DashboardPage />} />
          <Route path="/workspaces/:workspaceId/devices" element={<DevicesPage />} />
          <Route path="/workspaces/:workspaceId/devices/new" element={<NewDevicePage />} />
          <Route path="/workspaces/:workspaceId/devices/:deviceId" element={<DeviceDetailsPage />} />
          <Route path="/workspaces/:workspaceId/alerts" element={<AlertRulesPage />} />
          <Route path="/workspaces/:workspaceId/alerts/new" element={<AlertRuleFormPage />} />
          <Route path="/workspaces/:workspaceId/alerts/:ruleId/edit" element={<AlertRuleFormPage />} />
          <Route path="/workspaces/:workspaceId/explorer" element={<TelemetryExplorerPage />} />
          <Route path="/workspaces/:workspaceId/rejections" element={<TelemetryRejectionsPage />} />
          <Route path="/status" element={<HealthPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
