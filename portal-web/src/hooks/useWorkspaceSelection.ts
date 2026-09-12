import { useCallback, useEffect, useState } from "react";

const STORAGE_KEY = "fluxo:selected-workspace";
const SELECTION_EVENT = "fluxo:workspace-selection-changed";

function readWorkspaceId() {
  return sessionStorage.getItem(STORAGE_KEY);
}

export function useWorkspaceSelection() {
  const [workspaceId, setWorkspaceIdState] = useState<string | null>(() => readWorkspaceId());

  useEffect(() => {
    const syncSelection = () => setWorkspaceIdState(readWorkspaceId());

    window.addEventListener(SELECTION_EVENT, syncSelection);
    window.addEventListener("storage", syncSelection);

    return () => {
      window.removeEventListener(SELECTION_EVENT, syncSelection);
      window.removeEventListener("storage", syncSelection);
    };
  }, []);

  const setWorkspaceId = useCallback((value: string | null) => {
    setWorkspaceIdState(value);

    if (value) {
      sessionStorage.setItem(STORAGE_KEY, value);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }

    window.dispatchEvent(new Event(SELECTION_EVENT));
  }, []);

  return {
    workspaceId,
    setWorkspaceId
  };
}
