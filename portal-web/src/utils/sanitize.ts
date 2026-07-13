const CONTROL_CHARS = /[\u0000-\u001F\u007F]/g;

export function sanitizeText(value: string): string {
  return value.replace(CONTROL_CHARS, "").trim();
}

export function safeJsonPreview(value: string | null | undefined): string {
  if (!value) {
    return "";
  }

  try {
    const parsed = JSON.parse(value);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return value;
  }
}
