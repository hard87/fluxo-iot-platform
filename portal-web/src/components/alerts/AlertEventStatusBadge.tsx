import type { AlertEventStatus } from "../../types";
import { alertEventStatusBadgeClass, alertEventStatusLabels } from "./alertEventPresentation";

interface Props {
  status: AlertEventStatus;
}

export function AlertEventStatusBadge({ status }: Props) {
  return (
    <span className={`status-badge ${alertEventStatusBadgeClass[status]}`}>{alertEventStatusLabels[status]}</span>
  );
}
