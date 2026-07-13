import { sanitizeText } from "./sanitize";

export function validateEmail(email: string): string | null {
  const sanitized = sanitizeText(email);
  const pattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  if (!sanitized) {
    return "Email e obrigatorio.";
  }

  return pattern.test(sanitized) ? null : "Email invalido.";
}

export function validatePassword(password: string): string | null {
  if (password.length < 12) {
    return "Senha deve ter no minimo 12 caracteres.";
  }

  const hasUpper = /[A-Z]/.test(password);
  const hasLower = /[a-z]/.test(password);
  const hasDigit = /\d/.test(password);
  const hasSymbol = /[^\w\s]/.test(password);

  if (!hasUpper || !hasLower || !hasDigit || !hasSymbol) {
    return "Senha deve conter maiuscula, minuscula, numero e simbolo.";
  }

  return null;
}

export function validateRequired(value: string, label: string): string | null {
  return sanitizeText(value) ? null : `${label} e obrigatorio.`;
}
