import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <section>
      <h1>Pagina nao encontrada</h1>
      <p>
        <Link to="/workspaces">Voltar para workspaces</Link>
      </p>
    </section>
  );
}
