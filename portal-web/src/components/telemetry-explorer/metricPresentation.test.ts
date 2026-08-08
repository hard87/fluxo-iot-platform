import { describe, expect, it } from "vitest";
import type { TelemetrySeriesResponse } from "../../types";
import { groupTelemetrySeries } from "./TelemetrySeriesPanel";
import { humanizeMetricKey, stableSeriesColor } from "./metricPresentation";

function series(metricKey: string, canonicalUnit: string | null, semanticType: string | null): TelemetrySeriesResponse {
  return {
    deviceId: "edgewarden", metricKey, valueType: "Numeric", canonicalUnit, semanticType,
    points: [], truncated: false
  };
}

describe("telemetry presentation", () => {
  it("mantém a cor estável para a identidade da série", () => {
    expect(stableSeriesColor("edgewarden|gateway.cpu_temperature_c"))
      .toBe(stableSeriesColor("edgewarden|gateway.cpu_temperature_c"));
  });

  it("não mistura métricas sem metadata de unidade e grandeza", () => {
    const groups = groupTelemetrySeries([
      series("gateway.cpu_temperature_c", null, null),
      series("gateway.disk_used_percent", null, null)
    ]);
    expect(groups).toHaveLength(2);
  });

  it("agrupa grandezas equivalentes quando unidade e tipo semântico são explícitos", () => {
    const groups = groupTelemetrySeries([
      series("environment.temperature_c", "°C", "temperature"),
      series("gateway.cpu_temperature_c", "°C", "temperature")
    ]);
    expect(groups).toHaveLength(1);
  });

  it("gera um nome legível sem inventar tradução", () => {
    expect(humanizeMetricKey("gateway.cpu_temperature_c")).toBe("CPU temperature c");
  });
});
