import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import * as authService from "../services/api/authService";
import { sanitizeText } from "../utils/sanitize";
import { validateEmail, validatePassword } from "../utils/validators";

export function RegisterPage() {
  const navigate = useNavigate();
  const { applyLogin } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    const emailError = validateEmail(email);
    if (emailError) {
      setError(new Error(emailError));
      return;
    }

    const passwordError = validatePassword(password);
    if (passwordError) {
      setError(new Error(passwordError));
      return;
    }

    if (password !== confirmPassword) {
      setError(new Error("Confirmacao de senha nao confere."));
      return;
    }

    setLoading(true);
    try {
      const sanitizedEmail = sanitizeText(email);
      await authService.register({ email: sanitizedEmail, password });
      const loginResponse = await authService.login({ email: sanitizedEmail, password });
      applyLogin(loginResponse);
      navigate("/workspaces");
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="auth-card">
      <h1>Criar conta inicial</h1>
      <p>Conta para acessar o portal web do Fluxo.</p>
      <form onSubmit={handleSubmit} className="form-grid">
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            maxLength={320}
            required
          />
        </label>
        <label>
          Senha
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            maxLength={128}
            required
          />
        </label>
        <label>
          Confirmar senha
          <input
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            autoComplete="new-password"
            maxLength={128}
            required
          />
        </label>
        <button type="submit" disabled={loading}>
          {loading ? "Criando..." : "Criar conta"}
        </button>
        <ApiErrorMessage error={error} />
      </form>
      <p>
        Ja tem conta? <Link to="/login">Entrar</Link>
      </p>
    </section>
  );
}
