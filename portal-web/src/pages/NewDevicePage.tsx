import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import type { DeviceResponse, ProvisionedDeviceResponse } from "../types";
import { sanitizeText } from "../utils/sanitize";
import { validateRequired } from "../utils/validators";

type Category = "Sensor" | "Actuator" | "Gateway";

export function NewDevicePage() {
  const navigate = useNavigate();
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();

  const [name, setName] = useState("");
  const [identifier, setIdentifier] = useState("");
  const [category, setCategory] = useState<Category>("Sensor");
  const [metadataJson, setMetadataJson] = useState("");
  const [provisionNow, setProvisionNow] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [createdDevice, setCreatedDevice] = useState<DeviceResponse | null>(null);
  const [provisioned, setProvisioned] = useState<ProvisionedDeviceResponse | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setCreatedDevice(null);
    setProvisioned(null);

    const nameError = validateRequired(name, "Nome");
    const identifierError = validateRequired(identifier, "Identificador");

    if (nameError || identifierError) {
      setError(new Error(nameError ?? identifierError ?? "Dados invalidos."));
      return;
    }

    if (metadataJson.trim()) {
      try {
        JSON.parse(metadataJson);
      } catch {
        setError(new Error("Metadata JSON invalido."));
        return;
      }
    }

    if (!token || !workspaceId) {
      setError(new Error("Sessao invalida."));
      return;
    }

    setLoading(true);

    const payload = {
      name: sanitizeText(name),
      identifier: sanitizeText(identifier),
      category,
      metadataJson: metadataJson.trim() ? metadataJson.trim() : undefined
    };

    try {
      if (provisionNow) {
        const response = await deviceService.provisionDevice(token, workspaceId, payload);
        setProvisioned(response);
      } else {
        const response = await deviceService.createDevice(token, workspaceId, payload);
        setCreatedDevice(response);
      }
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section>
      <h1>Novo dispositivo</h1>
      <p>Cadastre e opcionalmente provisione com credencial MQTT inicial.</p>
      <ApiErrorMessage error={error} />
      <form onSubmit={handleSubmit} className="form-grid panel">
        <label>
          Nome
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} maxLength={120} required />
        </label>
        <label>
          Identificador
          <input
            type="text"
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            maxLength={120}
            required
          />
        </label>
        <label>
          Categoria
          <select value={category} onChange={(e) => setCategory(e.target.value as Category)}>
            <option value="Sensor">Sensor</option>
            <option value="Actuator">Actuator</option>
            <option value="Gateway">Gateway</option>
          </select>
        </label>
        <label>
          Metadata JSON (opcional)
          <textarea value={metadataJson} onChange={(e) => setMetadataJson(e.target.value)} rows={4} />
        </label>
        <label className="checkbox-field">
          <input type="checkbox" checked={provisionNow} onChange={(e) => setProvisionNow(e.target.checked)} />
          Provisonar agora e gerar credencial inicial
        </label>
        <button type="submit" disabled={loading}>
          {loading ? "Salvando..." : "Salvar dispositivo"}
        </button>
      </form>

      {createdDevice ? (
        <article className="panel success-box">
          <h2>Dispositivo criado</h2>
          <p>ID: {createdDevice.id}</p>
          {workspaceId ? (
            <Link className="button-link" to={`/workspaces/${workspaceId}/devices/${createdDevice.id}`}>
              Abrir detalhes
            </Link>
          ) : null}
        </article>
      ) : null}

      {provisioned ? (
        <article className="panel success-box">
          <h2>Credencial gerada (mostrar uma unica vez)</h2>
          <p>
            <strong>Username:</strong> {provisioned.credentialUsername}
          </p>
          <p>
            <strong>Secret:</strong> <code>{provisioned.provisioningSecret}</code>
          </p>
          <p>
            <strong>Topico:</strong> <code>{provisioned.mqttPublishTopic}</code>
          </p>
          {workspaceId ? (
            <Link className="button-link" to={`/workspaces/${workspaceId}/devices/${provisioned.deviceId}`}>
              Abrir detalhes do dispositivo
            </Link>
          ) : null}
        </article>
      ) : null}

      {workspaceId ? (
        <p>
          <Link to={`/workspaces/${workspaceId}/devices`}>Voltar para lista de dispositivos</Link>
        </p>
      ) : null}

      <p className="muted">
        O segredo nao fica salvo no navegador e so aparece no momento da criacao/rotacao.
      </p>
      <button
        type="button"
        className="button-secondary"
        onClick={() => navigate(workspaceId ? `/workspaces/${workspaceId}/devices` : "/workspaces")}
      >
        Cancelar
      </button>
    </section>
  );
}
