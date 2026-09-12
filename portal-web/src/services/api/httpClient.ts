import type { ApiProblem } from "../../types";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export class ApiError extends Error {
  public readonly status: number;
  public readonly problem?: ApiProblem;

  constructor(status: number, message: string, problem?: ApiProblem) {
    super(message);
    this.status = status;
    this.problem = problem;
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  token?: string | null;
  /** When aborted, fetch rejects with DOMException("AbortError"); this is not wrapped as ApiError. */
  signal?: AbortSignal;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {
    Accept: "application/json"
  };

  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  if (options.token) {
    headers.Authorization = `Bearer ${options.token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal
  });

  if (!response.ok) {
    const problem = await tryReadProblem(response);
    const message =
      problem?.detail ??
      problem?.title ??
      `Erro HTTP ${response.status}.`;

    throw new ApiError(response.status, message, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function tryReadProblem(response: Response): Promise<ApiProblem | undefined> {
  try {
    return (await response.json()) as ApiProblem;
  } catch {
    return undefined;
  }
}
