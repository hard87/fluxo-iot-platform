interface Props {
  enabled: boolean;
}

export function AlertRuleStateBadge({ enabled }: Props) {
  return <span className={`status-badge ${enabled ? "success" : "neutral"}`}>{enabled ? "Ativa" : "Desativada"}</span>;
}
