import type { MetricDefinitionResponse, TelemetryQueryRequest, TelemetryQueryResponse } from "../../types";
import { apiRequest } from "./httpClient";

export const listMetricDefinitions = (token: string, workspaceId: string) =>
  apiRequest<MetricDefinitionResponse[]>(`/api/workspaces/${workspaceId}/metric-definitions`, { token });
export const queryTelemetry = (token: string, workspaceId: string, body: TelemetryQueryRequest) =>
  apiRequest<TelemetryQueryResponse>(`/api/workspaces/${workspaceId}/telemetry/query`, { method: "POST", token, body });
