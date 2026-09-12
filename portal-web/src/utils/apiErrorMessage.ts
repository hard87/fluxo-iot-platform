import { ApiError } from "../services/api/httpClient";

const DEFAULT_MESSAGE = "Ocorreu um erro inesperado.";

export function getApiErrorMessage(error: unknown, fallback: string = DEFAULT_MESSAGE): string {
  return error instanceof ApiError ? error.message : fallback;
}
