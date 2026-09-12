import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";
import { Timestamp } from "../components/Timestamp";
import { useAuth } from "../hooks/useAuth";
import * as rejectionService from "../services/api/rejectionService";
import type { TelemetryRejectionPage } from "../types";

export function TelemetryRejectionsPage(){
 const {workspaceId}=useParams<{workspaceId:string}>(),{token}=useAuth();
 const [result,setResult]=useState<TelemetryRejectionPage|null>(null),[page,setPage]=useState(1),[search,setSearch]=useState(""),[applied,setApplied]=useState(""),[loading,setLoading]=useState(true),[error,setError]=useState<unknown>(null);
 const load=useCallback(async()=>{if(!token||!workspaceId)return;setLoading(true);setError(null);try{setResult(await rejectionService.listTelemetryRejections(token,workspaceId,page,20,applied));}catch(e){setError(e)}finally{setLoading(false)}},[token,workspaceId,page,applied]);
 useEffect(()=>{void load()},[load]);
 function submit(event:FormEvent){event.preventDefault();setPage(1);setApplied(search)}
 const pages=result?Math.max(1,Math.ceil(result.totalCount/result.pageSize)):1;
 return <section><PageHeader title="Mensagens rejeitadas" description="Investigue mensagens que não entraram na telemetria. Os payloads aparecem apenas como uma prévia limitada."/>
 <form className="rejection-filters panel" onSubmit={submit}><label>Buscar<input type="search" value={search} onChange={e=>setSearch(e.target.value)} placeholder="Dispositivo, tópico, tipo ou motivo"/></label><button type="submit">Buscar</button>{applied?<button type="button" className="button-secondary" onClick={()=>{setSearch("");setApplied("");setPage(1)}}>Limpar</button>:null}</form>
 {loading?<LoadingState compact title="Carregando mensagens rejeitadas"/>:null}{error?<ErrorState title="Não foi possível carregar as rejeições" description="Tente novamente. Os dados armazenados não foram alterados." action={<button type="button" onClick={()=>void load()}>Tentar novamente</button>}/>:null}
 {!loading&&!error&&result&&!result.items.length?<EmptyState title="Nenhuma rejeição encontrada" description={applied?"Revise o termo pesquisado.":"Não há mensagens rejeitadas neste workspace."}/>:null}
 {!loading&&!error&&result&&result.items.length?<><div className="panel rejection-table-wrap"><div className="table-scroll"><table className="rejection-table"><caption>Mensagens rejeitadas, da mais recente para a mais antiga</caption><thead><tr><th scope="col">Recebida</th><th scope="col">Dispositivo</th><th scope="col">Tópico</th><th scope="col">Motivo</th><th scope="col">Prévia do payload</th><th scope="col">Reprocessamento</th></tr></thead><tbody>{result.items.map(item=><tr key={item.id}><td><Timestamp value={item.receivedAtUtc}/></td><td>{item.deviceId??"Não identificado"}</td><td><code>{item.topic}</code></td><td><strong>{item.errorType}</strong><span>{item.reason}</span></td><td><code className="payload-preview">{item.payloadPreview||"Sem conteúdo"}</code></td><td>{item.reprocessed?"Resolvida":item.reprocessAttempts?"Tentativa "+item.reprocessAttempts:"Não tentada"}</td></tr>)}</tbody></table></div></div><nav className="pagination" aria-label="Paginação das rejeições"><button type="button" disabled={page<=1} onClick={()=>setPage(p=>p-1)}>Anterior</button><span>Página {page} de {pages} · {result.totalCount} registros</span><button type="button" disabled={page>=pages} onClick={()=>setPage(p=>p+1)}>Próxima</button></nav></>:null}
 </section>
}
