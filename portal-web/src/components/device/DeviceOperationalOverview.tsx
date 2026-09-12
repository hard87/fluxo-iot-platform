import type { DeviceResponse, TelemetryResponse } from "../../types";
import { EmptyState } from "../feedback/FeedbackStates";
import { Timestamp } from "../Timestamp";
import { OperationalHistory } from "./OperationalHistory";

type Value = number | boolean;
type Sample = { timestamp: string; metrics: Record<string, Value> };
const info: Record<string, [string, string, string]> = {
  "gateway.cpu_temperature_c":["Temperatura da CPU","°C","Recursos"],
  "gateway.load_1m":["Carga do sistema","","Recursos"],
  "gateway.memory_used_percent":["Memória utilizada","%","Recursos"],
  "gateway.disk_used_percent":["Disco utilizado","%","Recursos"],
  "gateway.uptime_sec":["Tempo ativo","s","Recursos"],
  "gateway.mqtt_connected":["Conexão MQTT","","Conectividade"],
  "gateway.network_interface_up":["Interface de rede","","Conectividade"],
  "gateway.time_synchronized":["Horário sincronizado","","Conectividade"],
  "gateway.network_rx_bytes":["Dados recebidos","bytes","Conectividade"],
  "gateway.network_tx_bytes":["Dados enviados","bytes","Conectividade"],
  "gateway.queue_depth":["Fila local","mensagens","Processamento"],
  "gateway.replayed_messages":["Mensagens reenviadas","mensagens","Processamento"],
  "gateway.dropped_messages":["Mensagens descartadas","mensagens","Processamento"],
  "environment.temperature_c":["Temperatura ambiente","°C","Ambiente"],
  "environment.humidity_percent":["Umidade","%","Ambiente"]
};
function parse(raw:string, fallback:string):Sample|null{try{const p=JSON.parse(raw) as {occurredAtUtc?:unknown;metrics?:unknown};if(!p.metrics||typeof p.metrics!=="object"||Array.isArray(p.metrics))return null;const metrics=Object.fromEntries(Object.entries(p.metrics).filter(([,v])=>typeof v==="number"||typeof v==="boolean")) as Record<string,Value>;return{timestamp:typeof p.occurredAtUtc==="string"?p.occurredAtUtc:fallback,metrics};}catch{return null}}
function display(value:Value,unit:string){if(typeof value==="boolean")return value?"Conectado":"Desconectado";if(unit==="s"){const h=Math.floor(value/3600),m=Math.floor(value%3600/60);return h?h+" h "+m+" min":m+" min"}if(unit==="bytes")return new Intl.NumberFormat("pt-BR",{notation:"compact",maximumFractionDigits:1}).format(value)+"B";return new Intl.NumberFormat("pt-BR",{maximumFractionDigits:1}).format(value)+(unit?" "+unit:"")}
function Sparkline({values}:{values:number[]}){if(values.length<2)return null;const min=Math.min(...values),span=Math.max(...values)-min||1;const points=values.map((v,i)=>(i/(values.length-1)*100)+","+(28-(v-min)/span*24)).join(" ");return <svg className="native-metric-sparkline" viewBox="0 0 100 32" preserveAspectRatio="none" aria-hidden="true"><polyline points={points}/></svg>}
export function DeviceOperationalOverview({device,telemetry,token,workspaceId}:{device:DeviceResponse;telemetry:TelemetryResponse[];token:string;workspaceId:string}){
 const samples=telemetry.map(x=>parse(x.payloadJson,x.occurredAtUtc)).filter((x):x is Sample=>Boolean(x)).reverse();
 const latest=parse(device.lastTelemetryPayloadJson??"",device.lastTelemetryOccurredAtUtc??device.lastTelemetryReceivedAtUtc??"")??samples[samples.length-1];
 if(!latest)return <EmptyState title="Sem métricas operacionais" description="Este dispositivo ainda não enviou um payload com métricas nativas."/>;
 const age=device.lastTelemetryReceivedAtUtc?Date.now()-new Date(device.lastTelemetryReceivedAtUtc).getTime():Infinity;
 const freshness=age<300000?"Atual":age<3600000?"Atrasado":"Sem dados recentes";
 const keys=Object.keys(latest.metrics).filter(k=>info[k]),groups=[...new Set(keys.map(k=>info[k][2]))];
 return <div className="operational-overview"><section className="panel operational-status"><div><p className="dashboard-card-eyebrow">Estado do dispositivo</p><h2>{device.name}</h2><p>{freshness} · última telemetria {device.lastTelemetryReceivedAtUtc?<Timestamp value={device.lastTelemetryReceivedAtUtc}/>:"indisponível"}</p></div><span className={"freshness-badge "+(age<300000?"is-current":"is-stale")}>{freshness}</span></section>
 {groups.map(group=><section key={group} className="native-metric-group"><h3>{group}</h3><div className="native-metric-grid">{keys.filter(k=>info[k][2]===group).map(k=>{const value=latest.metrics[k],values=samples.map(s=>s.metrics[k]).filter((v):v is number=>typeof v==="number"),previous=values[values.length-2],trend=typeof value==="number"&&previous!==undefined?(value>previous?"subindo":value<previous?"caindo":"estável"):null,peak=values.length?Math.max(...values):null;return <article key={k} className="panel native-metric-card"><div className="native-metric-heading"><h4>{info[k][0]}</h4>{trend?<span>{trend}</span>:null}</div><strong>{display(value,info[k][1])}</strong><small>{peak!==null?"Pico recente: "+display(peak,info[k][1]):"Valor atual"}</small><Sparkline values={values}/><code>{k}</code></article>})}</div></section>)}
 <OperationalHistory token={token} workspaceId={workspaceId} deviceId={device.id} availableKeys={keys.filter(k=>typeof latest.metrics[k]==="number")}/>
 <p className="operational-continuity">As métricas continuam armazenadas e consultáveis individualmente. Esta visão organiza apenas a leitura do estado atual.</p></div>
}
