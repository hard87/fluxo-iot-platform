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
const SESSION_KEY = "fluxo.auth.session";

interface StoredSession {
  token: string;
  user: AuthenticatedUser;
  expiresAtUtc: string;
}

function readSession(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const value = JSON.parse(raw) as Partial<StoredSession>;
    if (!value.token || !value.user?.userId || !value.user.email || !value.expiresAtUtc) {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
    if (new Date(value.expiresAtUtc).getTime() <= Date.now()) {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
    return value as StoredSession;
  } catch {
    sessionStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [initialSession] = useState(readSession);
  const [token, setToken] = useState<string | null>(initialSession?.token ?? null);
  const [user, setUser] = useState<AuthenticatedUser | null>(initialSession?.user ?? null);
  const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(initialSession?.expiresAtUtc ?? null);

  const applyLogin = useCallback((response: LoginResponse) => {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify({
      token: response.accessToken,
      user: response.user,
      expiresAtUtc: response.expiresAtUtc
    } satisfies StoredSession));
    setToken(response.accessToken);
    setUser(response.user);
    setExpiresAtUtc(response.expiresAtUtc);
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem(SESSION_KEY);
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
