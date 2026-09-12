import type { AlertOperator, AlertSeverity } from "../../types";

export function alertScopeLabel(deviceIdentifier: string | null): string {
  return deviceIdentifier ?? "Todos os dispositivos compatíveis";
}

const numericOperatorSymbols: Partial<Record<AlertOperator, string>> = {
  GreaterThan: ">",
  GreaterOrEqual: "≥",
  LessThan: "<",
  LessOrEqual: "≤"
};

interface ConditionFields {
  operator: AlertOperator;
  threshold: number | null;
  thresholdHigh: number | null;
  unit: string | null;
}

export function alertConditionLabel({ operator, threshold, thresholdHigh, unit }: ConditionFields): string {
  const unitSuffix = unit ? ` ${unit}` : "";

  switch (operator) {
    case "IsTrue":
      return "É verdadeiro";
    case "IsFalse":
      return "É falso";
    case "InsideRange":
      return `Entre ${threshold ?? "?"} e ${thresholdHigh ?? "?"}${unitSuffix}`;
    case "OutsideRange":
      return `Fora de ${threshold ?? "?"} a ${thresholdHigh ?? "?"}${unitSuffix}`;
    default:
      return `${numericOperatorSymbols[operator]} ${threshold ?? "?"}${unitSuffix}`;
  }
}

export const alertSeverityLabels: Record<AlertSeverity, string> = {
  Info: "Informativo",
  Warning: "Aviso",
  Critical: "Crítico"
};

export const alertSeverityBadgeClass: Record<AlertSeverity, string> = {
  Info: "info",
  Warning: "warning",
  Critical: "danger"
};
