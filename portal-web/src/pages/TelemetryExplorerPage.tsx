import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import * as deviceService from "../services/api/deviceService";
import * as telemetryService from "../services/api/telemetryService";
import { ApiError } from "../services/api/httpClient";
import type { DeviceResponse, MetricDefinitionResponse, MetricValueType, TelemetryAggregation,
  TelemetryBucket, TelemetryQueryResponse, TelemetrySeriesResponse } from "../types";

const compatibility: Record<MetricValueType, TelemetryAggregation[]> = {
  Numeric: ["raw", "avg", "min", "max", "sum", "count", "last"],
  Boolean: ["raw", "count", "last"], Text: ["raw", "count", "last"]
};
const buckets: TelemetryBucket[] = ["1m", "5m", "15m", "1h", "6h", "1d"];
const colors = ["#0b5c4b", "#d86f33", "#315e9e", "#8a3f7d", "#6d6b20"];
const localInput = (date: Date) => { const d = new Date(date.getTime() - date.getTimezoneOffset() * 60000); return d.toISOString().slice(0, 16); };

export function TelemetryExplorerPage() {
  const { workspaceId } = useParams<{ workspaceId: string }>(); const { token } = useAuth();
  const [devices, setDevices] = useState<DeviceResponse[]>([]); const [metrics, setMetrics] = useState<MetricDefinitionResponse[]>([]);
  const [deviceIds, setDeviceIds] = useState<string[]>([]); const [metricKeys, setMetricKeys] = useState<string[]>([]);
  const [from, setFrom] = useState(localInput(new Date(Date.now() - 6 * 3600000))); const [to, setTo] = useState(localInput(new Date()));
  const [aggregation, setAggregation] = useState<TelemetryAggregation>("raw"); const [bucket, setBucket] = useState<TelemetryBucket>("1m");
  const [loadingDevices, setLoadingDevices] = useState(true); const [loadingMetrics, setLoadingMetrics] = useState(false);
  const [loadingQuery, setLoadingQuery] = useState(false); const [error, setError] = useState<unknown>();
  const [result, setResult] = useState<TelemetryQueryResponse>();

  useEffect(() => { if (!token || !workspaceId) return; setLoadingDevices(true);
    deviceService.listDevices(token, workspaceId).then(setDevices).catch(setError).finally(() => setLoadingDevices(false)); }, [token, workspaceId]);
  useEffect(() => { if (!token || !workspaceId || deviceIds.length === 0 || metrics.length) return; setLoadingMetrics(true);
    telemetryService.listMetricDefinitions(token, workspaceId).then(setMetrics).catch(setError).finally(() => setLoadingMetrics(false));
  }, [token, workspaceId, deviceIds.length, metrics.length]);

  const selectedMetrics = metrics.filter(x => metricKeys.includes(x.metricKey));
  const aggregations = useMemo(() => selectedMetrics.length === 0 ? compatibility.Numeric :
    compatibility[selectedMetrics[0].valueType].filter(a => selectedMetrics.every(m => compatibility[m.valueType].includes(a))), [selectedMetrics]);
  useEffect(() => { if (!aggregations.includes(aggregation)) setAggregation(aggregations[0]); }, [aggregation, aggregations]);
  const rangeMs = new Date(to).getTime() - new Date(from).getTime();
  const minimumBucket: TelemetryBucket = rangeMs <= 6*3600000 ? "1m" : rangeMs <= 24*3600000 ? "5m" :
    rangeMs <= 7*86400000 ? "15m" : rangeMs <= 30*86400000 ? "1h" : "6h";
  const availableBuckets = buckets.slice(buckets.indexOf(minimumBucket));
  useEffect(() => { if (!availableBuckets.includes(bucket)) setBucket(availableBuckets[0]); }, [bucket, minimumBucket]);
  const rangeValid = rangeMs > 0 && (aggregation === "raw" ? rangeMs <= 24*3600000 : rangeMs <= 90*86400000);
  const canQuery = !!token && !!workspaceId && deviceIds.length > 0 && metricKeys.length > 0 && rangeValid && !loadingQuery;

  async function execute() { if (!canQuery || !token || !workspaceId) return; setLoadingQuery(true); setError(undefined); setResult(undefined);
    try { setResult(await telemetryService.queryTelemetry(token, workspaceId, { deviceIds, metricKeys,
      fromUtc: new Date(from).toISOString(), toUtc: new Date(to).toISOString(), aggregation, bucket: aggregation === "raw" ? null : bucket })); }
    catch (e) { setError(e); } finally { setLoadingQuery(false); }
  }
  const groups = useMemo(() => { const map = new Map<string, TelemetrySeriesResponse[]>(); for (const s of result?.series ?? []) {
    const key = `${s.valueType}|${s.canonicalUnit ?? "sem unidade"}`; map.set(key, [...(map.get(key) ?? []), s]); } return [...map.entries()]; }, [result]);
  const toggle = (value: string, values: string[], set: (v:string[])=>void, max=10) => set(values.includes(value) ? values.filter(x=>x!==value) : values.length < max ? [...values,value] : values);
  const deviceName = (id:string) => devices.find(x=>x.id===id)?.name ?? id;
  const bucketError = error instanceof ApiError && error.problem?.errorCode === "BUCKET_BELOW_MINIMUM";

  return <section className="explorer-page"><h1>Telemetry Explorer</h1><ApiErrorMessage error={error} />
    {error instanceof ApiError && error.status === 504 ? <p className="muted">Reduza o período ou a quantidade de séries selecionadas.</p> : null}
    {loadingDevices ? <p>Carregando dispositivos...</p> : null}
    {!loadingDevices && devices.length === 0 ? <div className="panel"><p>Nenhum dispositivo cadastrado.</p><Link to={`/workspaces/${workspaceId}/devices/new`}>Cadastrar dispositivo</Link></div> : null}
    {devices.length ? <div className="panel explorer-filters">
      <fieldset><legend>Dispositivos ({deviceIds.length}/10)</legend>{devices.map(d=><label className="checkbox-field" key={d.id}><input type="checkbox" checked={deviceIds.includes(d.id)} disabled={!deviceIds.includes(d.id)&&deviceIds.length>=10} onChange={()=>toggle(d.id,deviceIds,setDeviceIds)}/>{d.name}</label>)}{deviceIds.length>=10?<small>Limite de 10 dispositivos atingido.</small>:null}</fieldset>
      <fieldset><legend>Métricas ({metricKeys.length}/10)</legend>{loadingMetrics?<p>Carregando métricas...</p>:null}{!loadingMetrics&&deviceIds.length>0&&metrics.length===0?<p>Nenhuma métrica disponível ainda — envie telemetria primeiro.</p>:null}{metrics.map(m=><label className="checkbox-field" key={m.id}><input type="checkbox" checked={metricKeys.includes(m.metricKey)} disabled={!metricKeys.includes(m.metricKey)&&metricKeys.length>=10} onChange={()=>toggle(m.metricKey,metricKeys,setMetricKeys)}/><span>{m.displayName} <code>{m.metricKey}</code> · {m.valueType} · {m.canonicalUnit??"sem unidade"}</span></label>)}</fieldset>
      <label>De<input type="datetime-local" value={from} onChange={e=>setFrom(e.target.value)}/></label><label>Até<input type="datetime-local" value={to} onChange={e=>setTo(e.target.value)}/></label>
      <label>Agregação<select value={aggregation} onChange={e=>setAggregation(e.target.value as TelemetryAggregation)}>{aggregations.map(x=><option key={x}>{x}</option>)}</select></label>
      <label className={bucketError?"field-error":""}>Bucket<select disabled={aggregation==="raw"} value={bucket} onChange={e=>setBucket(e.target.value as TelemetryBucket)}>{availableBuckets.map(x=><option key={x}>{x}</option>)}</select></label>
      {!rangeValid?<p className="error-message">{aggregation==="raw"?"Consultas raw são limitadas a 24 horas.":"Consultas agregadas são limitadas a 90 dias."}</p>:null}
      <button type="button" disabled={!canQuery} onClick={()=>void execute()}>{loadingQuery?"Processando consulta...":"Executar consulta"}</button>
    </div>:null}
    {result?.series.some(x=>x.truncated)?<div className="truncate-banner">Resultado truncado — refine o período ou reduza a seleção.</div>:null}
    {groups.map(([key, series])=><SeriesPanel key={key} label={key} series={series} aggregation={result!.aggregation} deviceName={deviceName}/>) }
  </section>;
}

function SeriesPanel({label,series,aggregation,deviceName}:{label:string;series:TelemetrySeriesResponse[];aggregation:TelemetryAggregation;deviceName:(id:string)=>string}) {
  const [type, unit] = label.split("|"); const nonEmpty=series.filter(s=>s.points.length); const empty=series.filter(s=>!s.points.length);
  if (!nonEmpty.length) return <div className="panel"><h2>{type} · {unit}</h2><p>Sem dados neste período</p></div>;
  if (type === "Text" || aggregation === "count") return <div className="panel"><h2>{type} · {unit}</h2>{empty.length?<p>Sem dados neste período</p>:null}<div className="table-scroll"><table><thead><tr><th>Série</th><th>Timestamp</th><th>Valor</th></tr></thead><tbody>{nonEmpty.flatMap(s=>s.points.map(p=><tr key={`${s.deviceId}-${s.metricKey}-${p.timestampUtc}`}><td>{deviceName(s.deviceId)} · {s.metricKey}</td><td>{new Date(p.timestampUtc).toLocaleString()}</td><td>{aggregation==="count"?p.sampleCount:p.textValue}</td></tr>))}</tbody></table></div></div>;
  const rows = new Map<string,Record<string,string|number>>(); nonEmpty.forEach((s,i)=>s.points.forEach(p=>{const r=rows.get(p.timestampUtc)??{timestamp:p.timestampUtc}; r[`s${i}`]=type==="Boolean"?(p.booleanValue?1:0):(p.numericValue??0); rows.set(p.timestampUtc,r);}));
  return <div className="panel chart-panel"><h2>{type} · {unit}</h2>{empty.length?<p>Sem dados neste período: {empty.map(x=>`${deviceName(x.deviceId)} · ${x.metricKey}`).join(", ")}</p>:null}<ResponsiveContainer width="100%" height={340}><LineChart data={[...rows.values()]}><CartesianGrid strokeDasharray="3 3"/><XAxis dataKey="timestamp" tickFormatter={x=>new Date(x).toLocaleTimeString()}/><YAxis domain={type==="Boolean"?[0,1]:["auto","auto"]} ticks={type==="Boolean"?[0,1]:undefined} tickFormatter={v=>type==="Boolean"?(v?"true":"false"):String(v)}/><Tooltip labelFormatter={x=>new Date(String(x)).toLocaleString()}/><Legend/>{nonEmpty.map((s,i)=><Line key={`${s.deviceId}-${s.metricKey}`} dataKey={`s${i}`} name={`${deviceName(s.deviceId)} · ${s.metricKey}`} type={type==="Boolean"?"stepAfter":"monotone"} stroke={colors[i%colors.length]} dot={false} connectNulls={false}/>)}</LineChart></ResponsiveContainer></div>;
}
