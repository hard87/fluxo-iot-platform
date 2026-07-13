import { ApiError } from "../services/api/httpClient";

interface Props {
  error: unknown;
}

export function ApiErrorMessage({ error }: Props) {
  if (!error) {
    return null;
  }

  const message = error instanceof ApiError ? error.message : "Ocorreu um erro inesperado.";

  return <p className="error-message">{message}</p>;
}
