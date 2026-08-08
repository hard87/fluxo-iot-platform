import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type {
  MetricDefinitionResponse,
  TelemetryAggregation,
  TelemetrySeriesResponse
} from "../../types";
import { EmptyState } from "../feedback/FeedbackStates";
import {
  formatTelemetryNumber,
  formatTelemetryTimestamp,
  humanizeMetricKey,
  metricDisplayName,
  stableSeriesColor,
  valueTypeLabels
} from "./metricPresentation";

export interface TelemetrySeriesGroup {
  key: string;
  series: TelemetrySeriesResponse[];
}

export function groupTelemetrySeries(series: TelemetrySeriesResponse[]): TelemetrySeriesGroup[] {
  const groups = new Map<string, TelemetrySeriesResponse[]>();

  for (const item of series) {
    const unit = item.canonicalUnit?.trim();
    const semanticType = item.semanticType?.trim();
    const metadataKey = unit && semanticType
      ? `metadata:${item.valueType}:${semanticType}:${unit}`
      : `metric:${item.valueType}:${item.metricKey}:${unit ?? "missing"}`;
    groups.set(metadataKey, [...(groups.get(metadataKey) ?? []), item]);
  }

  return [...groups.entries()].map(([key, groupedSeries]) => ({ key, series: groupedSeries }));
}

interface TelemetrySeriesPanelProps {
  group: TelemetrySeriesGroup;
  aggregation: TelemetryAggregation;
  devices: Map<string, string>;
  definitions: Map<string, MetricDefinitionResponse>;
}

interface SeriesModel {
  dataKey: string;
  color: string;
  displayName: string;
  deviceName: string;
  fullLabel: string;
  legendLabel: string;
  series: TelemetrySeriesResponse;
}

interface TooltipPayloadItem {
  dataKey?: string | number;
  value?: string | number;
  color?: string;
}

function chartTickFormatter(timestamp: string, rangeMs: number) {
  const date = new Date(timestamp);
  const options: Intl.DateTimeFormatOptions = rangeMs > 7 * 86400000
    ? { day: "2-digit", month: "2-digit" }
    : rangeMs > 24 * 3600000
      ? { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }
      : { hour: "2-digit", minute: "2-digit" };
  return new Intl.DateTimeFormat("pt-BR", options).format(date);
}

function TelemetryTooltip({
  active,
  payload,
  label,
  models
}: {
  active?: boolean;
  payload?: TooltipPayloadItem[];
  label?: string;
  models: SeriesModel[];
}) {
  if (!active || !payload?.length || !label) return null;

  return (
    <div className="telemetry-tooltip">
      <time dateTime={label}>{formatTelemetryTimestamp(label)}</time>
      <ul>
        {payload.map((item) => {
          const model = models.find((candidate) => candidate.dataKey === String(item.dataKey));
          if (!model || item.value === undefined) return null;
          const isBoolean = model.series.valueType === "Boolean";
          const value = isBoolean
            ? Number(item.value) === 1 ? "true" : "false"
            : typeof item.value === "number" ? formatTelemetryNumber(item.value) : String(item.value);

          return (
            <li key={model.dataKey}>
              <span className="telemetry-tooltip-marker" style={{ backgroundColor: item.color ?? model.color }} aria-hidden="true" />
              <span>
                <strong>{model.deviceName} · {model.displayName}</strong>
                <code>{model.series.metricKey}</code>
              </span>
              <b>{value}{model.series.canonicalUnit ? ` ${model.series.canonicalUnit}` : ""}</b>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

export function TelemetrySeriesPanel({ group, aggregation, devices, definitions }: TelemetrySeriesPanelProps) {
  const nonEmpty = group.series.filter((item) => item.points.length > 0);
  const empty = group.series.filter((item) => item.points.length === 0);
  const uniqueMetricKeys = [...new Set(group.series.map((item) => item.metricKey))];
  const firstSeries = group.series[0];
  const firstDefinition = definitions.get(firstSeries.metricKey);
  const displayName = uniqueMetricKeys.length === 1
    ? metricDisplayName(firstSeries.metricKey, firstDefinition)
    : humanizeMetricKey(firstSeries.semanticType ?? "séries relacionadas");
  const technicalLabel = uniqueMetricKeys.length === 1
    ? firstSeries.metricKey
    : `${uniqueMetricKeys.length} métricas relacionadas`;
  const unit = firstSeries.canonicalUnit?.trim() || null;

  if (nonEmpty.length === 0) {
    return (
      <article className="panel telemetry-series-panel">
        <SeriesPanelHeader title={displayName} technicalLabel={technicalLabel} series={firstSeries} unit={unit} />
        <EmptyState
          title="Nenhuma telemetria encontrada para esta métrica"
          description="A métrica não possui pontos no período selecionado."
          compact
        />
      </article>
    );
  }

  const models: SeriesModel[] = nonEmpty.map((series, index) => {
    const seriesDisplayName = metricDisplayName(series.metricKey, definitions.get(series.metricKey));
    const deviceName = devices.get(series.deviceId) ?? series.deviceId;
    const fullLabel = `${deviceName} · ${seriesDisplayName} · ${series.metricKey}`;
    return {
      dataKey: `series-${index}`,
      color: stableSeriesColor(`${series.deviceId}|${series.metricKey}`),
      displayName: seriesDisplayName,
      deviceName,
      fullLabel,
      legendLabel: uniqueMetricKeys.length === 1 ? deviceName : `${deviceName} · ${seriesDisplayName}`,
      series
    };
  });

  if (firstSeries.valueType === "Text" || aggregation === "count") {
    return (
      <article className="panel telemetry-series-panel">
        <SeriesPanelHeader title={displayName} technicalLabel={technicalLabel} series={firstSeries} unit={unit} />
        <EmptySeriesNote empty={empty} devices={devices} definitions={definitions} />
        <div className="table-scroll">
          <table className="telemetry-table">
            <caption className="visually-hidden">Valores de {displayName} no período consultado</caption>
            <thead><tr><th scope="col">Série</th><th scope="col">Data e hora</th><th scope="col">Valor</th></tr></thead>
            <tbody>
              {nonEmpty.flatMap((series) => series.points.map((point) => (
                <tr key={`${series.deviceId}-${series.metricKey}-${point.timestampUtc}`}>
                  <td>
                    <strong>{devices.get(series.deviceId) ?? series.deviceId}</strong>
                    <code>{series.metricKey}</code>
                  </td>
                  <td><time dateTime={point.timestampUtc}>{formatTelemetryTimestamp(point.timestampUtc)}</time></td>
                  <td>{aggregation === "count" ? point.sampleCount ?? "—" : point.textValue ?? "—"}</td>
                </tr>
              )))}
            </tbody>
          </table>
        </div>
      </article>
    );
  }

  const rows = new Map<string, Record<string, string | number>>();
  models.forEach((model) => model.series.points.forEach((point) => {
    const row = rows.get(point.timestampUtc) ?? { timestamp: point.timestampUtc };
    if (model.series.valueType === "Boolean" && point.booleanValue !== null) {
      row[model.dataKey] = point.booleanValue ? 1 : 0;
    } else if (point.numericValue !== null) {
      row[model.dataKey] = point.numericValue;
    }
    rows.set(point.timestampUtc, row);
  }));
  const chartData = [...rows.values()];
  const timestamps = chartData.map((row) => new Date(String(row.timestamp)).getTime());
  const chartRangeMs = Math.max(...timestamps) - Math.min(...timestamps);

  return (
    <article className="panel telemetry-series-panel chart-panel">
      <SeriesPanelHeader title={displayName} technicalLabel={technicalLabel} series={firstSeries} unit={unit} />
      <EmptySeriesNote empty={empty} devices={devices} definitions={definitions} />
      <div className="telemetry-chart" role="img" aria-label={`Gráfico de ${displayName}`}>
        <ResponsiveContainer width="100%" height={360}>
          <LineChart data={chartData} margin={{ top: 12, right: 20, bottom: 8, left: unit ? 12 : 0 }}>
            <CartesianGrid stroke="#d9e2de" strokeDasharray="3 3" vertical={false} />
            <XAxis
              dataKey="timestamp"
              tickFormatter={(value) => chartTickFormatter(String(value), chartRangeMs)}
              minTickGap={32}
              tick={{ fontSize: 12, fill: "#52615b" }}
            />
            <YAxis
              width={unit ? 68 : 56}
              domain={firstSeries.valueType === "Boolean" ? [0, 1] : ["auto", "auto"]}
              ticks={firstSeries.valueType === "Boolean" ? [0, 1] : undefined}
              tickFormatter={(value) => firstSeries.valueType === "Boolean" ? (value ? "true" : "false") : formatTelemetryNumber(value)}
              tick={{ fontSize: 12, fill: "#52615b" }}
              label={unit ? { value: unit, angle: -90, position: "insideLeft", fill: "#52615b" } : undefined}
            />
            <Tooltip
              content={<TelemetryTooltip models={models} />}
              cursor={{ stroke: "#94a39d", strokeDasharray: "4 4" }}
            />
            {models.map((model) => (
              <Line
                key={`${model.series.deviceId}-${model.series.metricKey}`}
                dataKey={model.dataKey}
                name={model.legendLabel}
                type={firstSeries.valueType === "Boolean" ? "stepAfter" : "monotone"}
                stroke={model.color}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 4, strokeWidth: 2 }}
                connectNulls={false}
                isAnimationActive={false}
              />
            ))}
          </LineChart>
        </ResponsiveContainer>
      </div>
      <ul className="telemetry-series-legend" aria-label="Séries exibidas">
        {models.map((model) => (
          <li key={model.dataKey} title={model.fullLabel}>
            <span style={{ backgroundColor: model.color }} aria-hidden="true" />
            <span>{model.legendLabel}</span>
          </li>
        ))}
      </ul>
    </article>
  );
}

function SeriesPanelHeader({
  title,
  technicalLabel,
  series,
  unit
}: {
  title: string;
  technicalLabel: string;
  series: TelemetrySeriesResponse;
  unit: string | null;
}) {
  return (
    <header className="telemetry-panel-header">
      <div>
        <h2>{title}</h2>
        <code>{technicalLabel}</code>
      </div>
      <div className="telemetry-panel-metadata">
        <span>{valueTypeLabels[series.valueType]}</span>
        <span className={unit ? "" : "is-missing"}>{unit ?? "Unidade não informada"}</span>
      </div>
    </header>
  );
}

function EmptySeriesNote({
  empty,
  devices,
  definitions
}: {
  empty: TelemetrySeriesResponse[];
  devices: Map<string, string>;
  definitions: Map<string, MetricDefinitionResponse>;
}) {
  if (empty.length === 0) return null;
  const labels = empty.map((series) =>
    `${devices.get(series.deviceId) ?? series.deviceId} · ${metricDisplayName(series.metricKey, definitions.get(series.metricKey))}`
  );
  return <p className="telemetry-empty-series">Sem pontos nesta consulta: {labels.join(", ")}.</p>;
}
