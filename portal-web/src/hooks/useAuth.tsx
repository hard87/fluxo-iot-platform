import { createContext, useCallback, useContext, useMemo, useState } from "react";
import type { AuthenticatedUser, LoginResponse } from "../types";

interface AuthContextValue {
  token: string | null;
  user: AuthenticatedUser | null;
  expiresAtUtc: string | null;
  isAuthenticated: boolean;
  applyLogin: (response: LoginResponse) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(null);

  const applyLogin = useCallback((response: LoginResponse) => {
    setToken(response.accessToken);
    setUser(response.user);
    setExpiresAtUtc(response.expiresAtUtc);
  }, []);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    setExpiresAtUtc(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      user,
      expiresAtUtc,
      isAuthenticated: Boolean(token && user),
      applyLogin,
      logout
    }),
    [token, user, expiresAtUtc, applyLogin, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }

  return ctx;
}
