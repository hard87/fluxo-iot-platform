import { useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiErrorMessage } from "../components/ApiErrorMessage";
import { useAuth } from "../hooks/useAuth";
import * as authService from "../services/api/authService";
import { sanitizeText } from "../utils/sanitize";
import { validateEmail } from "../utils/validators";

export function LoginPage() {
  const navigate = useNavigate();
  const { applyLogin } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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

    if (!password) {
      setError(new Error("Senha e obrigatoria."));
      return;
    }

    setLoading(true);
    try {
      const response = await authService.login({
        email: sanitizeText(email),
        password
      });

      applyLogin(response);
      navigate("/workspaces");
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="auth-card">
      <h1>Entrar no Fluxo Portal</h1>
      <p>Use sua conta para acessar workspaces e dispositivos.</p>
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
            autoComplete="current-password"
            maxLength={128}
            required
          />
        </label>
        <button type="submit" disabled={loading}>
          {loading ? "Entrando..." : "Entrar"}
        </button>
        <ApiErrorMessage error={error} />
      </form>
      <p>
        Ainda nao tem conta? <Link to="/register">Criar conta inicial</Link>
      </p>
    </section>
  );
}
