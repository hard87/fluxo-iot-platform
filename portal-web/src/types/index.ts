export interface ApiProblem {
  status?: number;
  title?: string;
  detail?: string;
  traceId?: string;
  errorCode?: string;
}

export type MetricValueType = "Numeric" | "Boolean" | "Text";
export type TelemetryAggregation = "raw" | "avg" | "min" | "max" | "sum" | "count" | "last";
export type TelemetryBucket = "1m" | "5m" | "15m" | "1h" | "6h" | "1d";
export interface TelemetryQueryRequest { deviceIds: string[]; metricKeys: string[]; fromUtc: string; toUtc: string;
  aggregation: TelemetryAggregation; bucket: TelemetryBucket | null; }
export interface TelemetryPointResponse { timestampUtc: string; numericValue: number | null; booleanValue: boolean | null;
  textValue: string | null; sampleCount: number | null; }
export interface TelemetrySeriesResponse { deviceId: string; metricKey: string; valueType: MetricValueType;
  canonicalUnit: string | null; semanticType: string | null; points: TelemetryPointResponse[]; truncated: boolean; }
export interface TelemetryQueryMeta { totalPoints: number; maxPointsAllowed: number; executionTimeMs: number; }
export interface TelemetryQueryResponse { workspaceId: string; fromUtc: string; toUtc: string;
  aggregation: TelemetryAggregation; bucket: TelemetryBucket | null; series: TelemetrySeriesResponse[]; meta: TelemetryQueryMeta; }
export interface MetricDefinitionResponse { id: string; metricKey: string; displayName: string; valueType: MetricValueType;
  semanticType: string | null; canonicalUnit: string | null; status: string; isQueryable: boolean; }

export interface AuthenticatedUser {
  userId: string;
  email: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
}

export interface Workspace {
  id: string;
  tenantId: string;
  name: string;
  role: "Owner" | "Admin" | "Viewer" | 1 | 2 | 3;
  createdAtUtc: string;
}

export interface DeviceResponse {
  id: string;
  tenantId: string;
  workspaceId: string;
  name: string;
  identifier: string;
  category: string;
  metadataJson?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  lastContactAtUtc?: string | null;
  lastTelemetryReceivedAtUtc?: string | null;
  lastTelemetryOccurredAtUtc?: string | null;
  lastTelemetrySequence?: number | null;
  lastTelemetryPayloadJson?: string | null;
  operationalStatus: "Unknown" | "Online" | "Offline" | 1 | 2 | 3;
}

export interface ProvisionedDeviceResponse {
  deviceId: string;
  tenantId: string;
  workspaceId: string;
  deviceName: string;
  deviceIdentifier: string;
  deviceCategory: string;
  deviceIsActive: boolean;
  deviceCreatedAtUtc: string;
  lastContactAtUtc?: string | null;
  operationalStatus: "Unknown" | "Online" | "Offline";
  credentialId: string;
  credentialUsername: string;
  credentialStatus: string;
  credentialCreatedAtUtc: string;
  provisioningSecret: string;
  mqttPublishTopic: string;
}

export interface RotateCredentialResponse {
  deviceId: string;
  credentialId: string;
  credentialUsername: string;
  credentialStatus: string;
  credentialCreatedAtUtc: string;
  provisioningSecret: string;
  mqttPublishTopic: string;
}

export interface DeviceProvisioningDetails {
  deviceId: string;
  tenantId: string;
  workspaceId: string;
  deviceName: string;
  deviceIdentifier: string;
  deviceCategory: string;
  deviceIsActive: boolean;
  deviceCreatedAtUtc: string;
  lastContactAtUtc?: string | null;
  lastTelemetryReceivedAtUtc?: string | null;
  lastTelemetryOccurredAtUtc?: string | null;
  lastTelemetrySequence?: number | null;
  lastTelemetryPayloadJson?: string | null;
  operationalStatus: "Unknown" | "Online" | "Offline";
  activeCredentialId?: string | null;
  activeCredentialUsername?: string | null;
  activeCredentialStatus?: string | null;
  activeCredentialCreatedAtUtc?: string | null;
  mqttPublishTopic: string;
}

export interface TelemetryResponse {
  id: string;
  deviceId: string;
  payloadJson: string;
  occurredAtUtc: string;
  ingestedAtUtc: string;
}

export interface DashboardResponse {
  workspaceId: string;
  tenantId: string;
  devicesTotal: number;
  devicesOnline: number;
  devicesOffline: number;
  devicesUnknown: number;
  lastTelemetryReceivedAtUtc?: string | null;
  messagesProcessed: number;
  messagesRejected: number;
}

export interface TelemetryRejectionItem {
  id: string;
  receivedAtUtc: string;
  topic: string;
  payloadPreview: string;
  errorType: string;
  reason: string;
  deviceId?: string | null;
  messageType?: string | null;
  sequence?: number | null;
  reprocessed: boolean;
  reprocessAttempts: number;
}

export interface TelemetryRejectionPage {
  items: TelemetryRejectionItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface StatusComponent {
  name: string;
  status: string;
  durationMs: number;
  description?: string;
  lastCheckedUtc: string;
}

export interface PlatformStatusResponse {
  status: string;
  checkedAtUtc: string;
  components: StatusComponent[];
}
