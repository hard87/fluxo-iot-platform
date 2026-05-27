import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import { useWorkspaceSelection } from "../hooks/useWorkspaceSelection";
import * as workspaceService from "../services/api/workspaceService";
import type { Workspace } from "../types";
import { sanitizeText } from "../utils/sanitize";
import { validateRequired } from "../utils/validators";

export function WorkspacePage() {
  const navigate = useNavigate();
  const { token } = useAuth();
  const { workspaceId: selectedWorkspaceId, setWorkspaceId } = useWorkspaceSelection();
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [name, setName] = useState("");
  const [tenantId, setTenantId] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    const authToken: string = token;

    let isMounted = true;

    async function load() {
      setLoading(true);
      try {
        const items = await workspaceService.listWorkspaces(authToken);
        if (!isMounted) {
          return;
        }

        setWorkspaces(items);

        if (!selectedWorkspaceId && items.length > 0) {
          setWorkspaceId(items[0].id);
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
  }, [token, selectedWorkspaceId, setWorkspaceId]);

  async function handleCreateWorkspace(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    const nameError = validateRequired(name, "Nome do workspace");
    if (nameError) {
      setError(new Error(nameError));
      return;
    }

    if (!token) {
      return;
    }

    setSubmitting(true);
    try {
      const workspace = await workspaceService.createWorkspace(token, {
        name: sanitizeText(name),
        tenantId: tenantId ? sanitizeText(tenantId) : undefined
      });

      setWorkspaces((prev) => [...prev, workspace].sort((a, b) => a.name.localeCompare(b.name)));
      setWorkspaceId(workspace.id);
      setName("");
      setTenantId("");
    } catch (err) {
      setError(err);
    } finally {
      setSubmitting(false);
    }
  }

  function handleOpenWorkspace(workspaceId: string) {
    setWorkspaceId(workspaceId);
    navigate(`/workspaces/${workspaceId}/dashboard`);
  }

  return (
    <section>
      <h1>Workspaces</h1>
      <p>Crie ou selecione um workspace para gerenciar dispositivos.</p>
      <ApiErrorMessage error={error} />
      <div className="panel-grid">
        <article className="panel">
          <h2>Meus workspaces</h2>
          {loading ? <p>Carregando...</p> : null}
          {!loading && workspaces.length === 0 ? <p>Nenhum workspace encontrado.</p> : null}
          <ul className="list">
            {workspaces.map((workspace) => (
              <li key={workspace.id} className="list-item">
                <div>
                  <strong>{workspace.name}</strong>
                  <p className="muted">tenant: {workspace.tenantId}</p>
                </div>
                <div className="inline-actions">
                  <button
                    type="button"
                    className={selectedWorkspaceId === workspace.id ? "button-secondary active" : "button-secondary"}
                    onClick={() => handleOpenWorkspace(workspace.id)}
                  >
                    Abrir
                  </button>
                </div>
              </li>
            ))}
          </ul>
          {selectedWorkspaceId ? (
            <p>
              Workspace selecionado. <Link to={`/workspaces/${selectedWorkspaceId}/devices`}>Ver dispositivos</Link>
            </p>
          ) : null}
        </article>
        <article className="panel">
          <h2>Criar workspace</h2>
          <form onSubmit={handleCreateWorkspace} className="form-grid">
            <label>
              Nome
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={120}
                required
              />
            </label>
            <label>
              Tenant (opcional)
              <input
                type="text"
                value={tenantId}
                onChange={(e) => setTenantId(e.target.value)}
                maxLength={120}
              />
            </label>
            <button type="submit" disabled={submitting}>
              {submitting ? "Criando..." : "Criar workspace"}
            </button>
          </form>
        </article>
      </div>
    </section>
  );
}
