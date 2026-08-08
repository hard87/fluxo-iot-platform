import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { DeviceStatusBadge } from "../components/DeviceStatusBadge";
import { PageHeader } from "../components/PageHeader";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import type { DeviceResponse } from "../types";

export function DevicesPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();
  const [devices, setDevices] = useState<DeviceResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

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
        const response = await deviceService.listDevices(authToken, currentWorkspaceId);
        if (isMounted) {
          setDevices(response);
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
      <PageHeader title="Dispositivos" />
      <ApiErrorMessage error={error} />
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
      {loading ? <p>Carregando dispositivos...</p> : null}
      {!loading && devices.length === 0 ? <p>Nenhum dispositivo cadastrado.</p> : null}
      <ul className="list">
        {devices.map((device) => (
          <li key={device.id} className="list-item">
            <div>
              <strong>{device.name}</strong>
              <p className="muted">{device.identifier}</p>
              <DeviceStatusBadge status={device.operationalStatus} />
            </div>
            {workspaceId ? (
              <Link className="button-secondary" to={`/workspaces/${workspaceId}/devices/${device.id}`}>
                Detalhes
              </Link>
            ) : null}
          </li>
        ))}
      </ul>
    </section>
  );
}
