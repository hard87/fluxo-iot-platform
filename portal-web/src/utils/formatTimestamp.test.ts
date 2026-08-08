import { describe, expect, it } from "vitest";
import { formatHumanTimestamp } from "./formatTimestamp";

describe("formatHumanTimestamp", () => {
  it("formata um timestamp ISO como data e hora locais no padrão dd/mm/aaaa hh:mm:ss", () => {
    expect(formatHumanTimestamp("2026-08-08T12:27:22.000Z")).toMatch(/^\d{2}\/\d{2}\/2026 \d{2}:\d{2}:\d{2}$/);
  });

  it("não usa notação ISO bruta na saída", () => {
    expect(formatHumanTimestamp("2026-08-08T12:27:22.000Z")).not.toContain("T");
  });
});
