import type {
  DashboardResponse,
  DeviceProvisioningDetails,
  DeviceResponse,
  ProvisionedDeviceResponse,
  RotateCredentialResponse,
  TelemetryResponse
} from "../../types";
import { apiRequest } from "./httpClient";

export interface WorkspaceDevicePayload {
  name: string;
  identifier: string;
  category: "Sensor" | "Actuator" | "Gateway";
  metadataJson?: string;
}

export async function getDashboard(token: string, workspaceId: string): Promise<DashboardResponse> {
  return await apiRequest<DashboardResponse>(`/api/workspaces/${workspaceId}/dashboard`, { token });
}

export async function listDevices(token: string, workspaceId: string): Promise<DeviceResponse[]> {
  return await apiRequest<DeviceResponse[]>(`/api/workspaces/${workspaceId}/devices`, { token });
}

export async function createDevice(
  token: string,
  workspaceId: string,
  payload: WorkspaceDevicePayload
): Promise<DeviceResponse> {
  return await apiRequest<DeviceResponse>(`/api/workspaces/${workspaceId}/devices`, {
    method: "POST",
    token,
    body: payload
  });
}

export async function provisionDevice(
  token: string,
  workspaceId: string,
  payload: WorkspaceDevicePayload
): Promise<ProvisionedDeviceResponse> {
  return await apiRequest<ProvisionedDeviceResponse>(`/api/workspaces/${workspaceId}/devices/provision`, {
    method: "POST",
    token,
    body: payload
  });
}

export async function getDevice(token: string, workspaceId: string, deviceId: string): Promise<DeviceResponse> {
  return await apiRequest<DeviceResponse>(`/api/workspaces/${workspaceId}/devices/${deviceId}`, { token });
}

export async function getProvisioningDetails(
  token: string,
  workspaceId: string,
  deviceId: string
): Promise<DeviceProvisioningDetails> {
  return await apiRequest<DeviceProvisioningDetails>(
    `/api/workspaces/${workspaceId}/devices/${deviceId}/provisioning`,
    { token }
  );
}

export async function rotateCredential(
  token: string,
  workspaceId: string,
  deviceId: string
): Promise<RotateCredentialResponse> {
  return await apiRequest<RotateCredentialResponse>(
    `/api/workspaces/${workspaceId}/devices/${deviceId}/credentials/rotate`,
    {
      method: "POST",
      token
    }
  );
}

export async function getTelemetry(
  token: string,
  workspaceId: string,
  deviceId: string
): Promise<TelemetryResponse[]> {
  return await apiRequest<TelemetryResponse[]>(
    `/api/workspaces/${workspaceId}/devices/${deviceId}/telemetry?page=1&pageSize=20`,
    { token }
  );
}
