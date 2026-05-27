import type { PlatformStatusResponse } from "../../types";
import { apiRequest } from "./httpClient";

export async function getPlatformStatus(): Promise<PlatformStatusResponse> {
  return await apiRequest<PlatformStatusResponse>("/api/status");
}
