import { act, renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it } from "vitest";
import { AuthProvider, useAuth } from "./useAuth";

const wrapper = ({ children }: { children: ReactNode }) => <AuthProvider>{children}</AuthProvider>;

describe("useAuth", () => {
  beforeEach(() => sessionStorage.clear());

  it("persiste e reidrata a sessão na aba atual", () => {
    const first = renderHook(() => useAuth(), { wrapper });
    act(() => first.result.current.applyLogin({
      accessToken: "token",
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      user: { userId: "user-1", email: "user@fluxo.local" }
    }));
    first.unmount();

    const second = renderHook(() => useAuth(), { wrapper });
    expect(second.result.current.isAuthenticated).toBe(true);
    expect(second.result.current.user?.email).toBe("user@fluxo.local");
  });

  it("descarta uma sessão expirada", () => {
    sessionStorage.setItem("fluxo.auth.session", JSON.stringify({
      token: "expired",
      expiresAtUtc: new Date(Date.now() - 60_000).toISOString(),
      user: { userId: "user-1", email: "user@fluxo.local" }
    }));
    const result = renderHook(() => useAuth(), { wrapper });
    expect(result.result.current.isAuthenticated).toBe(false);
    expect(sessionStorage.getItem("fluxo.auth.session")).toBeNull();
  });
});
