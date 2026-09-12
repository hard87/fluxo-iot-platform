import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiRequest, ApiError } from "./httpClient";

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

describe("apiRequest", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("uses GET by default and omits Authorization/Content-Type when not needed", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    const result = await apiRequest<{ ok: boolean }>("/api/things");

    expect(result).toEqual({ ok: true });
    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.method).toBe("GET");
    expect((init?.headers as Record<string, string>).Authorization).toBeUndefined();
    expect((init?.headers as Record<string, string>)["Content-Type"]).toBeUndefined();
  });

  it("sends Authorization when a token is provided", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, {}));

    await apiRequest("/api/things", { token: "abc123" });

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect((init?.headers as Record<string, string>).Authorization).toBe("Bearer abc123");
  });

  it("sets Content-Type and serializes the body when a body is provided", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, {}));

    await apiRequest("/api/things", { method: "POST", body: { name: "x" } });

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect((init?.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
    expect(init?.body).toBe(JSON.stringify({ name: "x" }));
  });

  it("forwards an AbortSignal to fetch", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(200, {}));
    const controller = new AbortController();

    await apiRequest("/api/things", { signal: controller.signal });

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.signal).toBe(controller.signal);
  });

  it("returns undefined for a 204 response", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(jsonResponse(204, null));

    const result = await apiRequest("/api/things", { method: "DELETE" });

    expect(result).toBeUndefined();
  });

  it("throws ApiError with status and problem when the response is not ok", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(
      jsonResponse(404, { status: 404, title: "Not Found", detail: "Resource not found." })
    );

    await expect(apiRequest("/api/things/missing")).rejects.toMatchObject({
      status: 404,
      message: "Resource not found."
    });
  });

  it("falls back to a generic HTTP message when the problem body cannot be parsed", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error("not json");
      }
    } as unknown as Response);

    const error = await apiRequest("/api/things").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe("Erro HTTP 500.");
  });
});
