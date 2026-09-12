import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { ExplorerSelectors } from "../components/telemetry-explorer/ExplorerSelectors";
import { ExplorerToolbar } from "../components/telemetry-explorer/ExplorerToolbar";
import type { PeriodPreset } from "../components/telemetry-explorer/ExplorerToolbar";
import {
  groupTelemetrySeries,
  TelemetrySeriesPanel
} from "../components/telemetry-explorer/TelemetrySeriesPanel";
import { aggregationLabels, bucketLabels, formatTelemetryTimestamp } from "../components/telemetry-explorer/metricPresentation";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import { ApiError } from "../services/api/httpClient";
import * as telemetryService from "../services/api/telemetryService";
import type {
  DeviceResponse,
  MetricDefinitionResponse,
  MetricValueType,
  TelemetryAggregation,
  TelemetryBucket,
  TelemetryQueryResponse
} from "../types";
import { getApiErrorMessage } from "../utils/apiErrorMessage";

const compatibility: Record<MetricValueType, TelemetryAggregation[]> = {
  Numeric: ["raw", "avg", "min", "max", "sum", "count", "last"],
  Boolean: ["raw", "count", "last"],
  Text: ["raw", "count", "last"]
};
const buckets: TelemetryBucket[] = ["1m", "5m", "15m", "1h", "6h", "1d"];

function localInput(date: Date) {
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return localDate.toISOString().slice(0, 16);
}

function errorMessage(error: unknown) {
  return getApiErrorMessage(error, "Ocorreu um erro inesperado ao consultar a telemetria.");
}

export function TelemetryExplorerPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { token } = useAuth();
  const [devices, setDevices] = useState<DeviceResponse[]>([]);
  const [metrics, setMetrics] = useState<MetricDefinitionResponse[]>([]);
  const [deviceIds, setDeviceIds] = useState<string[]>([]);
  const [metricKeys, setMetricKeys] = useState<string[]>([]);
  const [from, setFrom] = useState(localInput(new Date(Date.now() - 6 * 3600000)));
  const [to, setTo] = useState(localInput(new Date()));
  const [activePreset, setActivePreset] = useState<PeriodPreset | null>("6h");
  const [aggregation, setAggregation] = useState<TelemetryAggregation>("raw");
  const [bucket, setBucket] = useState<TelemetryBucket>("1m");
  const [loadingDevices, setLoadingDevices] = useState(true);
  const [loadingMetrics, setLoadingMetrics] = useState(false);
  const [loadingQuery, setLoadingQuery] = useState(false);
  const [loadError, setLoadError] = useState<unknown>();
  const [queryError, setQueryError] = useState<unknown>();
  const [result, setResult] = useState<TelemetryQueryResponse>();

  useEffect(() => {
    if (!token || !workspaceId) return;
    let isMounted = true;
    setLoadingDevices(true);
    setLoadError(undefined);
    deviceService.listDevices(token, workspaceId)
      .then((items) => {
        if (!isMounted) return;
        setDevices(items);
        setLoadingDevices(false);
      })
      .catch((error) => {
        if (!isMounted) return;
        setLoadError(error);
        setLoadingDevices(false);
      });
    return () => { isMounted = false; };
  }, [token, workspaceId]);

  useEffect(() => {
    if (!token || !workspaceId || deviceIds.length === 0 || metrics.length > 0) return;
    let isMounted = true;
    setLoadingMetrics(true);
    setLoadError(undefined);
    telemetryService.listMetricDefinitions(token, workspaceId)
      .then((items) => {
        if (!isMounted) return;
        setMetrics(items);
        setLoadingMetrics(false);
      })
      .catch((error) => {
        if (!isMounted) return;
        setLoadError(error);
        setLoadingMetrics(false);
      });
    return () => { isMounted = false; };
  }, [token, workspaceId, deviceIds.length, metrics.length]);

  const selectedMetrics = metrics.filter((metric) => metricKeys.includes(metric.metricKey));
  const aggregations = useMemo(() => selectedMetrics.length === 0
    ? compatibility.Numeric
    : compatibility[selectedMetrics[0].valueType].filter((candidate) =>
      selectedMetrics.every((metric) => compatibility[metric.valueType].includes(candidate))
    ), [selectedMetrics]);

  useEffect(() => {
    if (!aggregations.includes(aggregation)) setAggregation(aggregations[0]);
  }, [aggregation, aggregations]);

  const rangeMs = new Date(to).getTime() - new Date(from).getTime();
  const minimumBucket: TelemetryBucket = rangeMs <= 6 * 3600000 ? "1m"
    : rangeMs <= 24 * 3600000 ? "5m"
      : rangeMs <= 7 * 86400000 ? "15m"
        : rangeMs <= 30 * 86400000 ? "1h" : "6h";
  const availableBuckets = buckets.slice(buckets.indexOf(minimumBucket));

  useEffect(() => {
    if (!availableBuckets.includes(bucket)) setBucket(availableBuckets[0]);
  }, [bucket, minimumBucket]);

  const rangeValid = rangeMs > 0 && (aggregation === "raw" ? rangeMs <= 24 * 3600000 : rangeMs <= 90 * 86400000);
  const selectedSeriesCount = deviceIds.length * metricKeys.length;
  const canQuery = Boolean(
    token
    && workspaceId
    && deviceIds.length > 0
    && metricKeys.length > 0
    && selectedSeriesCount <= 25
    && rangeValid
    && !loadingQuery
  );
  const bucketError = queryError instanceof ApiError && queryError.problem?.errorCode === "BUCKET_BELOW_MINIMUM";

  function toggle(value: string, values: string[], setter: (next: string[]) => void, max = 10) {
    setter(values.includes(value) ? values.filter((item) => item !== value) : values.length < max ? [...values, value] : values);
  }

  function applyPreset(preset: PeriodPreset) {
    const hours = preset === "1h" ? 1 : preset === "6h" ? 6 : 24;
    const end = new Date();
    setFrom(localInput(new Date(end.getTime() - hours * 3600000)));
    setTo(localInput(end));
    setActivePreset(preset);
  }

  async function execute() {
    if (!canQuery || !token || !workspaceId) return;
    setLoadingQuery(true);
    setQueryError(undefined);
    try {
      setResult(await telemetryService.queryTelemetry(token, workspaceId, {
        deviceIds,
        metricKeys,
        fromUtc: new Date(from).toISOString(),
        toUtc: new Date(to).toISOString(),
        aggregation,
        bucket: aggregation === "raw" ? null : bucket
      }));
    } catch (error) {
      setQueryError(error);
    } finally {
      setLoadingQuery(false);
    }
  }

  const groups = useMemo(() => groupTelemetrySeries(result?.series ?? []), [result]);
  const hasAnyPoints = result?.series.some((series) => series.points.length > 0) ?? false;
  const deviceNames = useMemo(() => new Map(devices.map((device) => [device.id, device.name])), [devices]);
  const metricDefinitions = useMemo(() => new Map(metrics.map((metric) => [metric.metricKey, metric])), [metrics]);
  const resultPeriod = result
    ? `${formatTelemetryTimestamp(result.fromUtc)} — ${formatTelemetryTimestamp(result.toUtc)}`
    : null;

  return (
    <section className="explorer-page">
      <PageHeader
        title="Telemetry Explorer"
        description="Analise séries reais por dispositivo, métrica e período sem alterar a origem dos dados."
      />

      {loadingDevices ? (
        <LoadingState title="Carregando dispositivos" description="Preparando os controles da consulta." />
      ) : null}

      {loadError ? (
        <ErrorState title="Não foi possível preparar o Explorer" description={errorMessage(loadError)} />
      ) : null}

      {!loadingDevices && !loadError && devices.length === 0 ? (
        <EmptyState
          title="Nenhum dispositivo cadastrado"
          description="Cadastre um dispositivo e envie telemetria antes de usar o Explorer."
          action={<Link className="button-link" to={`/workspaces/${workspaceId}/devices/new`}>Cadastrar dispositivo</Link>}
        />
      ) : null}

      {devices.length > 0 ? (
        <>
          <section className="panel explorer-query-builder" aria-labelledby="explorer-query-title">
            <header className="explorer-query-heading">
              <div>
                <h2 id="explorer-query-title">Configurar consulta</h2>
                <p>Selecione as séries e defina como o período será consultado.</p>
              </div>
              <span>{selectedSeriesCount}/25 séries</span>
            </header>

            <ExplorerSelectors
              devices={devices}
              metrics={metrics}
              deviceIds={deviceIds}
              metricKeys={metricKeys}
              loadingMetrics={loadingMetrics}
              onToggleDevice={(deviceId) => toggle(deviceId, deviceIds, setDeviceIds)}
              onToggleMetric={(metricKey) => toggle(metricKey, metricKeys, setMetricKeys)}
            />

            <ExplorerToolbar
              from={from}
              to={to}
              aggregation={aggregation}
              aggregations={aggregations}
              bucket={bucket}
              availableBuckets={availableBuckets}
              activePreset={activePreset}
              rangeValid={rangeValid}
              loading={loadingQuery}
              canQuery={canQuery}
              bucketError={bucketError}
              selectedSeriesCount={selectedSeriesCount}
              onFromChange={(value) => { setFrom(value); setActivePreset(null); }}
              onToChange={(value) => { setTo(value); setActivePreset(null); }}
              onPreset={applyPreset}
              onAggregationChange={setAggregation}
              onBucketChange={setBucket}
              onExecute={() => void execute()}
            />
          </section>

          {queryError ? (
            <ErrorState
              title="A consulta não pôde ser concluída"
              description={errorMessage(queryError)}
              action={canQuery ? <button type="button" onClick={() => void execute()}>Tentar novamente</button> : undefined}
            />
          ) : null}

          {queryError instanceof ApiError && queryError.status === 504 ? (
            <p className="explorer-error-hint">Reduza o período ou a quantidade de séries selecionadas.</p>
          ) : null}

          {loadingQuery ? (
            <LoadingState
              title={result ? "Atualizando resultados" : "Consultando telemetria"}
              description="A consulta pode levar alguns segundos em períodos maiores."
              compact
            />
          ) : null}

          {!result && !loadingQuery && !queryError ? (
            <EmptyState
              title="Configure e execute uma consulta"
              description="Escolha pelo menos um dispositivo e uma métrica para visualizar a telemetria."
            />
          ) : null}

          {result ? (
            <section className="explorer-results" aria-labelledby="explorer-results-title">
              <header className="explorer-results-header">
                <div>
                  <h2 id="explorer-results-title">Resultados</h2>
                  <p>{resultPeriod} · horário local</p>
                </div>
                <dl>
                  <div><dt>Agregação</dt><dd>{aggregationLabels[result.aggregation]}</dd></div>
                  <div><dt>Bucket</dt><dd>{result.bucket ? bucketLabels[result.bucket] : "Não se aplica"}</dd></div>
                  <div><dt>Pontos</dt><dd>{new Intl.NumberFormat("pt-BR").format(result.meta.totalPoints)}</dd></div>
                  <div><dt>Execução</dt><dd>{result.meta.executionTimeMs} ms</dd></div>
                </dl>
              </header>

              {result.series.some((series) => series.truncated) ? (
                <div className="truncate-banner">Resultado truncado — refine o período ou reduza a seleção.</div>
              ) : null}

              {!hasAnyPoints ? (
                <EmptyState
                  title="Nenhuma telemetria encontrada para o período selecionado"
                  description="Tente ampliar o período ou confirme se as métricas escolhidas possuem dados."
                />
              ) : groups.map((group) => (
                <TelemetrySeriesPanel
                  key={group.key}
                  group={group}
                  aggregation={result.aggregation}
                  devices={deviceNames}
                  definitions={metricDefinitions}
                />
              ))}
            </section>
          ) : null}
        </>
      ) : null}
    </section>
  );
}
