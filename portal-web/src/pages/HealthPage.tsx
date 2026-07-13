import { useEffect, useState } from "react";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import * as statusService from "../services/api/statusService";
import type { PlatformStatusResponse } from "../types";

export function HealthPage() {
  const [status, setStatus] = useState<PlatformStatusResponse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      setLoading(true);
      try {
        const response = await statusService.getPlatformStatus();
        if (isMounted) {
          setStatus(response);
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
  }, []);

  return (
    <section>
      <h1>Saude da plataforma</h1>
      <ApiErrorMessage error={error} />
      {loading ? <p>Consultando status...</p> : null}
      {status ? (
        <article className="panel">
          <p>
            <strong>Status geral:</strong> {status.status}
          </p>
          <p>
            <strong>Verificado em:</strong> {status.checkedAtUtc}
          </p>
          <ul className="list">
            {status.components.map((component) => (
              <li key={component.name} className="list-item">
                <div>
                  <strong>{component.name}</strong>
                  <p className="muted">{component.description ?? "Sem descricao"}</p>
                </div>
                <span className="badge">{component.status}</span>
              </li>
            ))}
          </ul>
        </article>
      ) : null}
    </section>
  );
}
