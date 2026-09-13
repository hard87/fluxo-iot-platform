import { describe, expect, it } from "vitest";
import {
  alertConditionLabel,
  alertScopeLabel,
  alertSeverityBadgeClass,
  alertSeverityLabels
} from "./alertRulePresentation";

describe("alertScopeLabel", () => {
  it("returns the device identifier when the rule targets one device", () => {
    expect(alertScopeLabel("device-1")).toBe("device-1");
  });

  it("returns a readable label when the rule applies to all compatible devices", () => {
    expect(alertScopeLabel(null)).toBe("Todos os dispositivos compatíveis");
  });
});

describe("alertConditionLabel", () => {
  it("formats a numeric threshold operator with the unit", () => {
    expect(alertConditionLabel({ operator: "GreaterThan", threshold: 30, thresholdHigh: null, unit: "°C" })).toBe(
      "> 30 °C"
    );
    expect(alertConditionLabel({ operator: "LessOrEqual", threshold: 5, thresholdHigh: null, unit: null })).toBe(
      "≤ 5"
    );
  });

  it("formats range operators with both bounds", () => {
    expect(
      alertConditionLabel({ operator: "InsideRange", threshold: 10, thresholdHigh: 20, unit: "%" })
    ).toBe("Entre 10 e 20 %");
    expect(
      alertConditionLabel({ operator: "OutsideRange", threshold: 0, thresholdHigh: 5, unit: "bar" })
    ).toBe("Fora de 0 a 5 bar");
  });

  it("formats boolean operators without a threshold", () => {
    expect(alertConditionLabel({ operator: "IsTrue", threshold: null, thresholdHigh: null, unit: null })).toBe(
      "É verdadeiro"
    );
    expect(alertConditionLabel({ operator: "IsFalse", threshold: null, thresholdHigh: null, unit: null })).toBe(
      "É falso"
    );
  });
});

describe("alertSeverityLabels / alertSeverityBadgeClass", () => {
  it("has a text label and a badge class for every severity, not color alone", () => {
    expect(alertSeverityLabels.Info).toBe("Informativo");
    expect(alertSeverityLabels.Warning).toBe("Aviso");
    expect(alertSeverityLabels.Critical).toBe("Crítico");
    expect(alertSeverityBadgeClass.Critical).toBe("danger");
  });
});
