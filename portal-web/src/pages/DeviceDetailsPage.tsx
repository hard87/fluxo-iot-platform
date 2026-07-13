import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { DeviceStatusBadge } from "../components/DeviceStatusBadge";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import type {
  DeviceProvisioningDetails,
  DeviceResponse,
  RotateCredentialResponse,
  TelemetryResponse
} from "../types";
import { safeJsonPreview } from "../utils/sanitize";

export function DeviceDetailsPage() {
  const { workspaceId, deviceId } = useParams<{ workspaceId: string; deviceId: string }>();
  const { token } = useAuth();

  const [device, setDevice] = useState<DeviceResponse | null>(null);
  const [provisioning, setProvisioning] = useState<DeviceProvisioningDetails | null>(null);
  const [telemetry, setTelemetry] = useState<TelemetryResponse[]>([]);
  const [rotatedCredential, setRotatedCredential] = useState<RotateCredentialResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [rotating, setRotating] = useState(false);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    if (!token || !workspaceId || !deviceId) {
      return;
    }

    const authToken: string = token;
    const currentWorkspaceId: string = workspaceId;
    const currentDeviceId: string = deviceId;

    let isMounted = true;

    async function load() {
      setLoading(true);
      setError(null);
      try {
        const [deviceResult, provisioningResult, telemetryResult] = await Promise.all([
          deviceService.getDevice(authToken, currentWorkspaceId, currentDeviceId),
          deviceService.getProvisioningDetails(authToken, currentWorkspaceId, currentDeviceId),
          deviceService.getTelemetry(authToken, currentWorkspaceId, currentDeviceId)
        ]);

        if (!isMounted) {
          return;
        }

        setDevice(deviceResult);
        setProvisioning(provisioningResult);
        setTelemetry(telemetryResult);
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
  }, [token, workspaceId, deviceId]);

  async function handleRotateCredential() {
    if (!token || !workspaceId || !deviceId) {
      return;
    }

    setRotating(true);
    setError(null);
    setRotatedCredential(null);

    try {
      const result = await deviceService.rotateCredential(token, workspaceId, deviceId);
      setRotatedCredential(result);
      const details = await deviceService.getProvisioningDetails(token, workspaceId, deviceId);
      setProvisioning(details);
    } catch (err) {
      setError(err);
    } finally {
      setRotating(false);
    }
  }

  return (
    <section>
      <h1>Detalhes do dispositivo</h1>
      <ApiErrorMessage error={error} />
      {loading ? <p>Carregando...</p> : null}
      {device ? (
        <article className="panel">
          <h2>{device.name}</h2>
          <p>
            <strong>Identifier:</strong> {device.identifier}
          </p>
          <p>
            <strong>Status:</strong> <DeviceStatusBadge status={device.operationalStatus} />
          </p>
          <p>
            <strong>Ultimo contato:</strong> {device.lastContactAtUtc ?? "Sem contato"}
          </p>
          <p>
            <strong>Ultima telemetria:</strong> {device.lastTelemetryReceivedAtUtc ?? "Sem telemetria"}
          </p>
          <p>
            <strong>Ultimo payload:</strong>
          </p>
          <pre>{safeJsonPreview(device.lastTelemetryPayloadJson)}</pre>
        </article>
      ) : null}

      {provisioning ? (
        <article className="panel">
          <h2>Provisionamento</h2>
          <p>
            <strong>Username ativo:</strong> {provisioning.activeCredentialUsername ?? "Nao disponivel"}
          </p>
          <p>
            <strong>Topico MQTT:</strong> <code>{provisioning.mqttPublishTopic}</code>
          </p>
          <button type="button" onClick={handleRotateCredential} disabled={rotating}>
            {rotating ? "Rotacionando..." : "Rotacionar credencial"}
          </button>
        </article>
      ) : null}

      {rotatedCredential ? (
        <article className="panel success-box">
          <h2>Nova credencial gerada (mostrar uma unica vez)</h2>
          <p>
            <strong>Username:</strong> {rotatedCredential.credentialUsername}
          </p>
          <p>
            <strong>Secret:</strong> <code>{rotatedCredential.provisioningSecret}</code>
          </p>
        </article>
      ) : null}

      <article className="panel">
        <h2>Ultimas telemetrias</h2>
        {telemetry.length === 0 ? <p>Sem telemetria registrada.</p> : null}
        <ul className="list">
          {telemetry.map((item) => (
            <li key={item.id} className="list-item telemetry-item">
              <div>
                <strong>{item.occurredAtUtc}</strong>
                <p className="muted">Ingerido em {item.ingestedAtUtc}</p>
                <pre>{safeJsonPreview(item.payloadJson)}</pre>
              </div>
            </li>
          ))}
        </ul>
      </article>

      {workspaceId ? (
        <p>
          <Link to={`/workspaces/${workspaceId}/devices`}>Voltar para dispositivos</Link>
        </p>
      ) : null}
    </section>
  );
}
