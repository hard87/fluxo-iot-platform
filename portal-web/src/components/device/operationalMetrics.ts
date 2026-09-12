import type { TelemetrySeriesResponse } from "../../types";
export type OperationalPeriod = "1h" | "6h" | "24h";
export const periodHours: Record<OperationalPeriod, number> = { "1h": 1, "6h": 6, "24h": 24 };
export function timeWindow(period: OperationalPeriod, now = new Date()) {
  return { fromUtc: new Date(now.getTime() - periodHours[period] * 3600000).toISOString(), toUtc: now.toISOString() };
}
export function numericValues(series: TelemetrySeriesResponse) {
  return series.points.map(p => p.numericValue).filter((v): v is number => v !== null);
}
export function summarizeSeries(series: TelemetrySeriesResponse) {
  const values = numericValues(series);
  if (!values.length) return null;
  const current = values[values.length - 1], first = values[0], min = Math.min(...values), max = Math.max(...values);
  const average = values.reduce((sum, value) => sum + value, 0) / values.length;
  const tolerance = Math.max(Math.abs(first) * .02, .01);
  const trend = Math.abs(current - first) <= tolerance ? "estável" : current > first ? "subindo" : "caindo";
  return { current, min, max, average, trend, samples: values.length };
}
