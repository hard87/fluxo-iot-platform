import { getApiErrorMessage } from "../utils/apiErrorMessage";

interface Props {
  error: unknown;
}

export function ApiErrorMessage({ error }: Props) {
  if (!error) {
    return null;
  }

  return <p className="error-message">{getApiErrorMessage(error)}</p>;
}
