import type { AlertSeverity } from "../../types";
import { alertSeverityBadgeClass, alertSeverityLabels } from "./alertRulePresentation";

interface Props {
  severity: AlertSeverity;
}

export function AlertSeverityBadge({ severity }: Props) {
  return <span className={`status-badge ${alertSeverityBadgeClass[severity]}`}>{alertSeverityLabels[severity]}</span>;
}
