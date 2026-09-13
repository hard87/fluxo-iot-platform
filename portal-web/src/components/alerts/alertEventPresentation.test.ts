import { describe, expect, it } from "vitest";
import {
  alertEventStatusBadgeClass,
  alertEventStatusLabels,
  alertTransitionReasonLabel,
  alertTransitionValueLabel,
  formatFreshness,
  formatTimestampWithZone
} from "./alertEventPresentation";

describe("alertEventStatusLabels / alertEventStatusBadgeClass", () => {
  it("has a text label and a badge class for every status, not color alone", () => {
    expect(alertEventStatusLabels.Firing).toBe("Ativo");
    expect(alertEventStatusLabels.Resolved).toBe("Resolvido");
    expect(alertEventStatusLabels.Closed).toBe("Encerrado");
    expect(alertEventStatusBadgeClass.Firing).toBe("danger");
  });
});

describe("alertTransitionReasonLabel", () => {
  it("translates known backend reason codes", () => {
    expect(alertTransitionReasonLabel("ConditionSatisfied")).toBe("Condição satisfeita");
    expect(alertTransitionReasonLabel("RecoveryObserved")).toBe("Recuperação observada");
    expect(alertTransitionReasonLabel("RuleRevised")).toBe("Regra revisada");
  });

  it("falls back to the raw string for unknown reasons instead of hiding it", () => {
    expect(alertTransitionReasonLabel("SomeFutureReason")).toBe("SomeFutureReason");
  });
});

describe("formatTimestampWithZone", () => {
  it("includes an explicit timezone name in the formatted output", () => {
    const formatted = formatTimestampWithZone("2026-09-12T12:00:00Z");
    // The exact abbreviation depends on the runtime's local zone; only assert it's present at all.
    expect(formatted).not.toBe(formatted.replace(/[A-Za-z]{2,5}$/, ""));
  });
});

describe("formatFreshness", () => {
  it("reports missing ingestion evidence explicitly instead of a healthy zero", () => {
    expect(formatFreshness("2026-09-12T12:00:00Z", null)).toBe("Sem dado de ingestão");
  });

  it("formats sub-second delays", () => {
    expect(formatFreshness("2026-09-12T12:00:00.500Z", "2026-09-12T12:00:00.000Z")).toBe("< 1s");
  });

  it("formats seconds and minutes", () => {
    expect(formatFreshness("2026-09-12T12:00:05Z", "2026-09-12T12:00:00Z")).toBe("5s");
    expect(formatFreshness("2026-09-12T12:01:20Z", "2026-09-12T12:00:00Z")).toBe("1min 20s");
  });

  it("clamps a negative delay (clock skew) to zero instead of showing a negative duration", () => {
    expect(formatFreshness("2026-09-12T12:00:00Z", "2026-09-12T12:00:05Z")).toBe("< 1s");
  });
});

describe("alertTransitionValueLabel", () => {
  it("prefers the numeric value when present", () => {
    expect(alertTransitionValueLabel(42, null)).toBe("42");
  });

  it("renders a boolean value in Portuguese", () => {
    expect(alertTransitionValueLabel(null, true)).toBe("Verdadeiro");
    expect(alertTransitionValueLabel(null, false)).toBe("Falso");
  });

  it("shows a placeholder when neither value is present", () => {
    expect(alertTransitionValueLabel(null, null)).toBe("—");
  });
});
