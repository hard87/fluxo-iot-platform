import { describe, expect, it } from "vitest";
import { ApiError } from "../services/api/httpClient";
import { getApiErrorMessage } from "./apiErrorMessage";

describe("getApiErrorMessage", () => {
  it("returns the ApiError message as-is", () => {
    expect(getApiErrorMessage(new ApiError(400, "Validation failed"))).toBe("Validation failed");
  });

  it("never leaks a non-ApiError message and falls back to the generic default", () => {
    expect(getApiErrorMessage(new Error("boom"))).toBe("Ocorreu um erro inesperado.");
  });

  it("accepts a custom fallback for a non-ApiError error", () => {
    expect(getApiErrorMessage(new Error("boom"), "Erro ao consultar X.")).toBe("Erro ao consultar X.");
  });

  it("falls back for null/undefined without throwing", () => {
    expect(getApiErrorMessage(null)).toBe("Ocorreu um erro inesperado.");
    expect(getApiErrorMessage(undefined)).toBe("Ocorreu um erro inesperado.");
  });
});
