export interface ApiProblem {
  status?: number;
  title?: string;
  detail?: string;
  traceId?: string;
}

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
  role: "Owner" | "Admin" | "Viewer";
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
  operationalStatus: "Unknown" | "Online" | "Offline";
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

export interface StatusComponent {
  name: string;
  status: string;
  durationMs: number;
  description?: string;
}

export interface PlatformStatusResponse {
  status: string;
  checkedAtUtc: string;
  components: StatusComponent[];
}
