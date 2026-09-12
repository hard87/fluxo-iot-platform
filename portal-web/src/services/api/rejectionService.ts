import type { TelemetryRejectionPage } from "../../types";
import { apiRequest } from "./httpClient";
export function listTelemetryRejections(token:string,workspaceId:string,page=1,pageSize=20,search=""){
 const query=new URLSearchParams({page:String(page),pageSize:String(pageSize)});
 if(search.trim())query.set("search",search.trim());
 return apiRequest<TelemetryRejectionPage>("/api/workspaces/"+workspaceId+"/telemetry-rejections?"+query.toString(),{token});
}
