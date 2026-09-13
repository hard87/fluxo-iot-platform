import type { AlertAttemptDiagnostic } from "../../types";

type DiagnosticStatus = AlertAttemptDiagnostic["status"];

/**
 * /diagnostics only ever returns Failed or DeadLetter rows (server-filtered) — there is no
 * "pending/processing/delivered" status observable through this endpoint. Failed still has a
 * scheduled retry; DeadLetter does not, regardless of what nextAttemptAtUtc happens to hold.
 */
export function alertDiagnosticStatusLabel(status: DiagnosticStatus): string {
  return status === "DeadLetter" ? "Falha permanente (dead letter)" : "Falha — nova tentativa agendada";
}

export function alertDiagnosticStatusBadgeClass(status: DiagnosticStatus): string {
  return status === "DeadLetter" ? "danger" : "warning";
}

const KNOWN_DIAGNOSTIC_REASONS: Record<string, string> = {
  EvaluationFailed: "Falha ao avaliar a regra",
  LeaseRecoveryExhausted: "Concessão de processamento expirada; tentativas esgotadas"
};

/** `reason` is free text on the backend, not a closed union; unknown values are shown as-is. */
export function alertDiagnosticReasonLabel(reason: string | null): string {
  if (!reason) {
    return "—";
  }
  return KNOWN_DIAGNOSTIC_REASONS[reason] ?? reason;
}
