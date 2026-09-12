import { describe, expect, it } from "vitest";
import type { StatusComponent } from "../types";
import { deriveOverallStatus, healthStatusTone, mapHealthStatus } from "./healthStatus";

function component(status: string): StatusComponent {
  return { name: "postgresql", status, durationMs: 1, lastCheckedUtc: "2026-08-08T12:00:00Z" };
}

describe("mapHealthStatus", () => {
  it("mapeia os estados reais do backend para a taxonomia padronizada", () => {
    expect(mapHealthStatus("Healthy")).toBe("Healthy");
    expect(mapHealthStatus("Degraded")).toBe("Degraded");
    expect(mapHealthStatus("Unhealthy")).toBe("Unavailable");
  });

  it("cai em Unknown para valores não reconhecidos, nunca em Healthy", () => {
    expect(mapHealthStatus("")).toBe("Unknown");
    expect(mapHealthStatus("weird-value")).toBe("Unknown");
  });
});

describe("healthStatusTone", () => {
  it("mapeia cada estado para o tom semântico correto", () => {
    expect(healthStatusTone("Healthy")).toBe("success");
    expect(healthStatusTone("Degraded")).toBe("warning");
    expect(healthStatusTone("Unavailable")).toBe("danger");
    expect(healthStatusTone("Unknown")).toBe("neutral");
  });
});

describe("deriveOverallStatus", () => {
  it("nunca resolve para Healthy quando não há componentes monitorados", () => {
    expect(deriveOverallStatus([])).toBe("Unknown");
  });

  it("usa o pior estado entre os componentes reais", () => {
    expect(deriveOverallStatus([component("Healthy"), component("Unhealthy")])).toBe("Unavailable");
    expect(deriveOverallStatus([component("Healthy"), component("Degraded")])).toBe("Degraded");
    expect(deriveOverallStatus([component("Healthy"), component("Healthy")])).toBe("Healthy");
  });

  it("trata um status não reconhecido como Unknown mesmo com outros componentes saudáveis", () => {
    expect(deriveOverallStatus([component("Healthy"), component("weird-value")])).toBe("Unknown");
  });
});
