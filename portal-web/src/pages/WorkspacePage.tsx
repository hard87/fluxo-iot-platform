import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { PageHeader } from "../components/PageHeader";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { useAuth } from "../hooks/useAuth";
import { useWorkspaceSelection } from "../hooks/useWorkspaceSelection";
import * as workspaceService from "../services/api/workspaceService";
import { ApiError } from "../services/api/httpClient";
import type { Workspace } from "../types";
import { sanitizeText } from "../utils/sanitize";
import { validateRequired } from "../utils/validators";

function errorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : "Ocorreu um erro inesperado.";
}

const roleLabels = { 1: "Owner", 2: "Admin", 3: "Viewer" } as const;

function formatWorkspaceRole(role: Workspace["role"]): string {
  return typeof role === "number" ? roleLabels[role] : role;
}

export function WorkspacePage() {
  const navigate = useNavigate();
  const { token } = useAuth();
  const { workspaceId: selectedWorkspaceId, setWorkspaceId } = useWorkspaceSelection();
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [name, setName] = useState("");
  const [tenantId, setTenantId] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [formError, setFormError] = useState<unknown>(null);

  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const load = useCallback(
    async (authToken: string) => {
      setLoading(true);
      setLoadError(null);
      try {
        const items = await workspaceService.listWorkspaces(authToken);
        if (!isMountedRef.current) {
          return;
        }

        setWorkspaces(items);

        if (!selectedWorkspaceId && items.length > 0) {
          setWorkspaceId(items[0].id);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setLoadError(err);
        }
      } finally {
        if (isMountedRef.current) {
          setLoading(false);
        }
      }
    },
    [selectedWorkspaceId, setWorkspaceId]
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    void load(token);
  }, [token, load]);

  async function handleCreateWorkspace(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const nameError = validateRequired(name, "Nome do workspace");
    if (nameError) {
      setFormError(new Error(nameError));
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
      setFormError(err);
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
      <PageHeader
        title="Workspaces"
        description="Crie ou selecione um workspace para gerenciar dispositivos."
      />
      <div className="panel-grid">
        <article className="panel">
          <h2>Meus workspaces</h2>

          {loading ? <LoadingState compact title="Carregando workspaces" /> : null}

          {!loading && loadError ? (
            <ErrorState
              title="Não foi possível carregar seus workspaces"
              description={errorMessage(loadError)}
              action={
                token ? (
                  <button type="button" className="button-secondary" onClick={() => void load(token)}>
                    Tentar novamente
                  </button>
                ) : undefined
              }
            />
          ) : null}

          {!loading && !loadError && workspaces.length === 0 ? (
            <EmptyState
              title="Nenhum workspace encontrado"
              description="Crie o primeiro workspace ao lado para começar a gerenciar dispositivos."
              action={<a href="#create-workspace">Ir para criação de workspace</a>}
            />
          ) : null}

          {!loading && !loadError && workspaces.length > 0 ? (
            <ul className="list">
              {workspaces.map((workspace) => {
                const isSelected = selectedWorkspaceId === workspace.id;
                return (
                  <li key={workspace.id} className="list-item">
                    <div>
                      <strong>{workspace.name}</strong>
                      <p className="muted">
                        tenant: {workspace.tenantId} ·{" "}
                        <span className="badge">{formatWorkspaceRole(workspace.role)}</span>
                      </p>
                    </div>
                    <div className="inline-actions">
                      <button
                        type="button"
                        className={isSelected ? "button-secondary active" : "button-secondary"}
                        aria-current={isSelected ? "true" : undefined}
                        onClick={() => handleOpenWorkspace(workspace.id)}
                      >
                        {isSelected ? "Selecionado — Abrir" : "Abrir"}
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          ) : null}

          {selectedWorkspaceId ? (
            <p>
              Workspace selecionado. <Link to={`/workspaces/${selectedWorkspaceId}/devices`}>Ver dispositivos</Link>
            </p>
          ) : null}
        </article>
        <article className="panel" id="create-workspace">
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
            <ApiErrorMessage error={formError} />
          </form>
        </article>
      </div>
    </section>
  );
}
