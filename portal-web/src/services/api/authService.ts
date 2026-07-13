import type { AuthenticatedUser, LoginResponse } from "../../types";
import { apiRequest } from "./httpClient";

export interface RegisterPayload {
  email: string;
  password: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export async function register(payload: RegisterPayload): Promise<void> {
  await apiRequest("/api/auth/register", {
    method: "POST",
    body: payload
  });
}

export async function login(payload: LoginPayload): Promise<LoginResponse> {
  return await apiRequest<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: payload
  });
}

export async function me(token: string): Promise<AuthenticatedUser> {
  return await apiRequest<AuthenticatedUser>("/api/auth/me", {
    token
  });
}
