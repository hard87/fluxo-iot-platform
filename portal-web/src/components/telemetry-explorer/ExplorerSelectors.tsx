import { useMemo, useState } from "react";
import { DeviceStatusBadge } from "../DeviceStatusBadge";
import { LoadingState } from "../feedback/FeedbackStates";
import type { DeviceResponse, MetricDefinitionResponse } from "../../types";
import { metricDisplayName, valueTypeLabels } from "./metricPresentation";

interface ExplorerSelectorsProps {
  devices: DeviceResponse[];
  metrics: MetricDefinitionResponse[];
  deviceIds: string[];
  metricKeys: string[];
  loadingMetrics: boolean;
  onToggleDevice: (deviceId: string) => void;
  onToggleMetric: (metricKey: string) => void;
}

export function ExplorerSelectors({
  devices,
  metrics,
  deviceIds,
  metricKeys,
  loadingMetrics,
  onToggleDevice,
  onToggleMetric
}: ExplorerSelectorsProps) {
  const [deviceFilter, setDeviceFilter] = useState("");
  const [metricFilter, setMetricFilter] = useState("");

  const visibleDevices = useMemo(() => {
    const query = deviceFilter.trim().toLocaleLowerCase();
    if (!query) return devices;
    return devices.filter((device) => `${device.name} ${device.identifier}`.toLocaleLowerCase().includes(query));
  }, [deviceFilter, devices]);

  const visibleMetrics = useMemo(() => {
    const query = metricFilter.trim().toLocaleLowerCase();
    if (!query) return metrics;
    return metrics.filter((metric) =>
      `${metricDisplayName(metric.metricKey, metric)} ${metric.metricKey} ${metric.canonicalUnit ?? ""}`
        .toLocaleLowerCase()
        .includes(query)
    );
  }, [metricFilter, metrics]);

  return (
    <div className="explorer-selection-grid">
      <fieldset className="explorer-selector">
        <legend>Dispositivos <span>{deviceIds.length}/10</span></legend>
        <input
          type="search"
          value={deviceFilter}
          onChange={(event) => setDeviceFilter(event.target.value)}
          placeholder="Filtrar por nome ou identificador"
          aria-label="Filtrar dispositivos"
        />
        <div className="explorer-option-list">
          {visibleDevices.map((device) => (
            <label className="explorer-option" key={device.id}>
              <input
                type="checkbox"
                checked={deviceIds.includes(device.id)}
                disabled={!deviceIds.includes(device.id) && deviceIds.length >= 10}
                onChange={() => onToggleDevice(device.id)}
                aria-label={`Selecionar dispositivo ${device.name}`}
              />
              <span className="explorer-option-copy">
                <strong>{device.name}</strong>
                <code>{device.identifier}</code>
              </span>
              <DeviceStatusBadge status={device.operationalStatus} />
            </label>
          ))}
          {visibleDevices.length === 0 ? <p className="explorer-list-empty">Nenhum dispositivo corresponde ao filtro.</p> : null}
        </div>
        {deviceIds.length >= 10 ? <small>Limite de 10 dispositivos atingido.</small> : null}
      </fieldset>

      <fieldset className="explorer-selector explorer-metric-selector">
        <legend>Métricas <span>{metricKeys.length}/10</span></legend>
        <input
          type="search"
          value={metricFilter}
          onChange={(event) => setMetricFilter(event.target.value)}
          placeholder="Filtrar por nome, key ou unidade"
          aria-label="Filtrar métricas"
          disabled={loadingMetrics || metrics.length === 0}
        />
        {loadingMetrics ? (
          <LoadingState title="Carregando métricas" description="Consultando o catálogo do workspace." compact />
        ) : (
          <div className="explorer-option-list">
            {visibleMetrics.map((metric) => (
              <label className="explorer-option explorer-metric-option" key={metric.id}>
                <input
                  type="checkbox"
                  checked={metricKeys.includes(metric.metricKey)}
                  disabled={!metricKeys.includes(metric.metricKey) && metricKeys.length >= 10}
                  onChange={() => onToggleMetric(metric.metricKey)}
                  aria-label={`Selecionar métrica ${metricDisplayName(metric.metricKey, metric)}`}
                />
                <span className="explorer-option-copy">
                  <strong>{metricDisplayName(metric.metricKey, metric)}</strong>
                  <code title={metric.metricKey}>{metric.metricKey}</code>
                  <span className="explorer-metric-metadata">
                    <span>{valueTypeLabels[metric.valueType]}</span>
                    <span className={metric.canonicalUnit ? "" : "is-missing"}>
                      {metric.canonicalUnit ?? "Unidade não informada"}
                    </span>
                  </span>
                </span>
              </label>
            ))}
            {visibleMetrics.length === 0 && metrics.length > 0 ? (
              <p className="explorer-list-empty">Nenhuma métrica corresponde ao filtro.</p>
            ) : null}
            {metrics.length === 0 ? (
              <p className="explorer-list-empty">Selecione um dispositivo para carregar as métricas disponíveis.</p>
            ) : null}
          </div>
        )}
        {metricKeys.length >= 10 ? <small>Limite de 10 métricas atingido.</small> : null}
      </fieldset>
    </div>
  );
}
