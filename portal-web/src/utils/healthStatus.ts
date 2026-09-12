import type { StatusTone } from "../components/StatusBadge";
import type { StatusComponent } from "../types";

export type HealthState = "Healthy" | "Degraded" | "Unavailable" | "Unknown";

const severity: Record<HealthState, number> = {
  Healthy: 0,
  Unknown: 1,
  Degraded: 2,
  Unavailable: 3
};

const toneByState: Record<HealthState, StatusTone> = {
  Healthy: "success",
  Degraded: "warning",
  Unavailable: "danger",
  Unknown: "neutral"
};

export function mapHealthStatus(raw: string): HealthState {
  switch (raw) {
    case "Healthy":
      return "Healthy";
    case "Degraded":
      return "Degraded";
    case "Unhealthy":
      return "Unavailable";
    default:
      return "Unknown";
  }
}

export function healthStatusTone(state: HealthState): StatusTone {
  return toneByState[state];
}

/**
 * Computes the platform-wide state from the real component entries instead of
 * trusting a top-level "status" string — ASP.NET reports Healthy by default
 * when zero checks are registered, so an empty/unrecognized component list
 * must never resolve to Healthy here.
 */
export function deriveOverallStatus(components: StatusComponent[]): HealthState {
  if (components.length === 0) {
    return "Unknown";
  }

  return components
    .map((component) => mapHealthStatus(component.status))
    .reduce((worst, current) => (severity[current] > severity[worst] ? current : worst), "Healthy" as HealthState);
}
