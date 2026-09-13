import type { AlertEventStatus } from "../../types";

export const alertEventStatusLabels: Record<AlertEventStatus, string> = {
  Firing: "Ativo",
  Resolved: "Resolvido",
  Closed: "Encerrado"
};

export const alertEventStatusBadgeClass: Record<AlertEventStatus, string> = {
  Firing: "danger",
  Resolved: "success",
  Closed: "neutral"
};

const KNOWN_TRANSITION_REASONS: Record<string, string> = {
  ConditionSatisfied: "Condição satisfeita",
  RecoveryObserved: "Recuperação observada",
  RuleRevised: "Regra revisada",
  RuleDisabled: "Regra desativada",
  BeforeActivation: "Antes da ativação da regra",
  Historical: "Amostra histórica"
};

/** `reason` is free text on the backend, not a closed union; unknown values are shown as-is. */
export function alertTransitionReasonLabel(reason: string): string {
  return KNOWN_TRANSITION_REASONS[reason] ?? reason;
}

const timestampWithZoneFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false,
  timeZoneName: "short"
});

/** Same absolute format as Timestamp/formatHumanTimestamp, but with an explicit timezone name. */
export function formatTimestampWithZone(value: string): string {
  return timestampWithZoneFormatter.format(new Date(value)).replace(",", "");
}

/** Delay between when a transition was recorded and when its ingestion was received.
 * A missing receivedAtUtc is a real absence of evidence, not a healthy zero — say so explicitly. */
export function formatFreshness(recordedAtUtc: string, receivedAtUtc: string | null): string {
  if (!receivedAtUtc) {
    return "Sem dado de ingestão";
  }

  const deltaMs = Math.max(0, new Date(recordedAtUtc).getTime() - new Date(receivedAtUtc).getTime());
  if (deltaMs < 1000) {
    return "< 1s";
  }

  const totalSeconds = Math.floor(deltaMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return minutes > 0 ? `${minutes}min ${seconds}s` : `${seconds}s`;
}

export function alertTransitionValueLabel(numericValue: number | null, booleanValue: boolean | null): string {
  if (numericValue !== null) {
    return String(numericValue);
  }
  if (booleanValue !== null) {
    return booleanValue ? "Verdadeiro" : "Falso";
  }
  return "—";
}
