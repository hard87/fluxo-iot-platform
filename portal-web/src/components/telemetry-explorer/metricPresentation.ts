import { formatHumanTimestamp } from "../../utils/formatTimestamp";
import type { MetricDefinitionResponse, MetricValueType, TelemetryAggregation, TelemetryBucket } from "../../types";

const acronyms: Record<string, string> = {
  cpu: "CPU",
  mqtt: "MQTT",
  rx: "RX",
  tx: "TX",
  ram: "RAM",
  io: "I/O",
  id: "ID"
};

/**
 * Curated pt-BR display names for the metric keys currently in real use.
 * Presentation only — does not touch canonicalUnit, valueType, or any API contract.
 * Unknown/future metric keys intentionally fall back to humanizeMetricKey() below
 * instead of guessing a translation.
 */
const curatedMetricNames: Record<string, string> = {
  temperature_c: "Temperatura",
  cpu_temperature_c: "Temperatura da CPU",
  humidity_percent: "Umidade",
  disk_used_percent: "Disco utilizado",
  memory_used_percent: "Memória utilizada",
  uptime_sec: "Tempo ativo",
  mqtt_connected: "MQTT conectado",
  time_synchronized: "Horário sincronizado",
  replayed_messages: "Mensagens reenviadas",
  dropped_messages: "Mensagens descartadas",
  load_1m: "Carga (1 min)"
};

const chartColors = [
  "#0b5c4b",
  "#c75d20",
  "#315e9e",
  "#8a3f7d",
  "#6d6b20",
  "#007c91",
  "#7c4d20",
  "#4b5f9e",
  "#a23b72",
  "#3f7d20"
];

export const aggregationLabels: Record<TelemetryAggregation, string> = {
  raw: "Dados brutos (raw)",
  avg: "Média (avg)",
  min: "Mínimo (min)",
  max: "Máximo (max)",
  sum: "Soma (sum)",
  count: "Contagem (count)",
  last: "Último valor (last)"
};

export const bucketLabels: Record<TelemetryBucket, string> = {
  "1m": "1 minuto",
  "5m": "5 minutos",
  "15m": "15 minutos",
  "1h": "1 hora",
  "6h": "6 horas",
  "1d": "1 dia"
};

export const valueTypeLabels: Record<MetricValueType, string> = {
  Numeric: "Numérica",
  Boolean: "Booleana",
  Text: "Texto"
};

function humanizeToken(token: string) {
  return acronyms[token.toLocaleLowerCase()] ?? token;
}

export function humanizeMetricKey(metricKey: string) {
  const segments = metricKey.split(".");
  const leaf = segments[segments.length - 1] || metricKey;
  const words = leaf.split(/[_-]+/).filter(Boolean).map(humanizeToken);
  if (words.length === 0) {
    return metricKey;
  }

  const label = words.join(" ");
  return label.charAt(0).toLocaleUpperCase("pt-BR") + label.slice(1);
}

export function metricDisplayName(metricKey: string, definition?: MetricDefinitionResponse) {
  const configuredName = definition?.displayName?.trim();
  if (configuredName && configuredName.toLocaleLowerCase() !== metricKey.toLocaleLowerCase()) {
    return configuredName;
  }

  const leaf = metricKey.split(".").pop() ?? metricKey;
  const curated = curatedMetricNames[leaf.toLocaleLowerCase()];
  if (curated) {
    return curated;
  }

  return humanizeMetricKey(metricKey);
}

export function stableSeriesColor(identity: string) {
  let hash = 2166136261;
  for (let index = 0; index < identity.length; index += 1) {
    hash ^= identity.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return chartColors[(hash >>> 0) % chartColors.length];
}

export function formatTelemetryNumber(value: number) {
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 }).format(value);
}

export function formatTelemetryTimestamp(timestamp: string) {
  return formatHumanTimestamp(timestamp);
}
