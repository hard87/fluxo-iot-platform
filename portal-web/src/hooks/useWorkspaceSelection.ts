import { useCallback, useEffect, useState } from "react";

const STORAGE_KEY = "fluxo:selected-workspace";

export function useWorkspaceSelection() {
  const [workspaceId, setWorkspaceIdState] = useState<string | null>(null);

  useEffect(() => {
    const value = sessionStorage.getItem(STORAGE_KEY);
    if (value) {
      setWorkspaceIdState(value);
    }
  }, []);

  const setWorkspaceId = useCallback((value: string | null) => {
    setWorkspaceIdState(value);

    if (value) {
      sessionStorage.setItem(STORAGE_KEY, value);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }, []);

  return {
    workspaceId,
    setWorkspaceId
  };
}
