import type { TelemetryAggregation, TelemetryBucket } from "../../types";
import { aggregationLabels, bucketLabels, formatTelemetryTimestamp } from "./metricPresentation";

export type PeriodPreset = "1h" | "6h" | "24h";

interface ExplorerToolbarProps {
  from: string;
  to: string;
  aggregation: TelemetryAggregation;
  aggregations: TelemetryAggregation[];
  bucket: TelemetryBucket;
  availableBuckets: TelemetryBucket[];
  activePreset: PeriodPreset | null;
  rangeValid: boolean;
  loading: boolean;
  canQuery: boolean;
  bucketError: boolean;
  selectedSeriesCount: number;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  onPreset: (preset: PeriodPreset) => void;
  onAggregationChange: (value: TelemetryAggregation) => void;
  onBucketChange: (value: TelemetryBucket) => void;
  onExecute: () => void;
}

function selectedPeriodLabel(from: string, to: string) {
  const fromDate = new Date(from);
  const toDate = new Date(to);
  if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime())) {
    return "Período incompleto";
  }

  return `${formatTelemetryTimestamp(fromDate.toISOString())} — ${formatTelemetryTimestamp(toDate.toISOString())}`;
}

export function ExplorerToolbar({
  from,
  to,
  aggregation,
  aggregations,
  bucket,
  availableBuckets,
  activePreset,
  rangeValid,
  loading,
  canQuery,
  bucketError,
  selectedSeriesCount,
  onFromChange,
  onToChange,
  onPreset,
  onAggregationChange,
  onBucketChange,
  onExecute
}: ExplorerToolbarProps) {
  return (
    <div className="explorer-toolbar">
      <fieldset className="explorer-period-control">
        <legend>Período</legend>
        <div className="explorer-period-presets" aria-label="Atalhos de período">
          {(["1h", "6h", "24h"] as const).map((preset) => (
            <button
              key={preset}
              type="button"
              className={`button-secondary explorer-preset${activePreset === preset ? " is-active" : ""}`}
              aria-pressed={activePreset === preset}
              onClick={() => onPreset(preset)}
            >
              {preset === "1h" ? "Última hora" : preset === "6h" ? "6 horas" : "24 horas"}
            </button>
          ))}
        </div>
        <div className="explorer-date-range">
          <label>
            De
            <input type="datetime-local" value={from} onChange={(event) => onFromChange(event.target.value)} />
          </label>
          <label>
            Até
            <input type="datetime-local" value={to} onChange={(event) => onToChange(event.target.value)} />
          </label>
        </div>
        <small>{selectedPeriodLabel(from, to)} · horário local</small>
      </fieldset>

      <label className="explorer-toolbar-field">
        Agregação
        <select
          value={aggregation}
          onChange={(event) => onAggregationChange(event.target.value as TelemetryAggregation)}
        >
          {aggregations.map((item) => <option key={item} value={item}>{aggregationLabels[item]}</option>)}
        </select>
        <small>{aggregation === "raw" ? "Pontos originais, limitados a 24 horas." : "Valores consolidados por intervalo."}</small>
      </label>

      <label className={`explorer-toolbar-field${bucketError ? " field-error" : ""}`}>
        Bucket
        <select
          disabled={aggregation === "raw"}
          value={bucket}
          onChange={(event) => onBucketChange(event.target.value as TelemetryBucket)}
        >
          {availableBuckets.map((item) => <option key={item} value={item}>{bucketLabels[item]}</option>)}
        </select>
        <small>{aggregation === "raw" ? "Não se aplica a dados brutos." : "Intervalo mínimo ajustado ao período."}</small>
      </label>

      <div className="explorer-execute-control">
        <span>{selectedSeriesCount} série{selectedSeriesCount === 1 ? "" : "s"} selecionada{selectedSeriesCount === 1 ? "" : "s"}</span>
        {!rangeValid ? (
          <p className="error-message">
            {aggregation === "raw" ? "Consultas raw são limitadas a 24 horas." : "Consultas agregadas são limitadas a 90 dias."}
          </p>
        ) : null}
        {selectedSeriesCount > 25 ? (
          <p className="error-message">Reduza a seleção para no máximo 25 séries por consulta.</p>
        ) : null}
        <button type="button" disabled={!canQuery} onClick={onExecute}>
          {loading ? "Consultando..." : "Executar consulta"}
        </button>
      </div>
    </div>
  );
}
