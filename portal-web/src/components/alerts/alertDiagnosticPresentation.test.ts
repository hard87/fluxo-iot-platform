import { describe, expect, it } from "vitest";
import {
  alertDiagnosticReasonLabel,
  alertDiagnosticStatusBadgeClass,
  alertDiagnosticStatusLabel
} from "./alertDiagnosticPresentation";

describe("alertDiagnosticStatusLabel / alertDiagnosticStatusBadgeClass", () => {
  it("distinguishes a retryable failure from a permanent dead letter with text, not color alone", () => {
    expect(alertDiagnosticStatusLabel("Failed")).toBe("Falha — nova tentativa agendada");
    expect(alertDiagnosticStatusLabel("DeadLetter")).toBe("Falha permanente (dead letter)");
    expect(alertDiagnosticStatusBadgeClass("Failed")).toBe("warning");
    expect(alertDiagnosticStatusBadgeClass("DeadLetter")).toBe("danger");
  });
});

describe("alertDiagnosticReasonLabel", () => {
  it("translates known backend reason codes", () => {
    expect(alertDiagnosticReasonLabel("EvaluationFailed")).toBe("Falha ao avaliar a regra");
    expect(alertDiagnosticReasonLabel("LeaseRecoveryExhausted")).toBe(
      "Concessão de processamento expirada; tentativas esgotadas"
    );
  });

  it("falls back to the raw string for unknown reasons and to a placeholder for null", () => {
    expect(alertDiagnosticReasonLabel("SomeFutureReason")).toBe("SomeFutureReason");
    expect(alertDiagnosticReasonLabel(null)).toBe("—");
  });
});
