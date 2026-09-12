import { Link } from "react-router-dom";
import { EmptyState } from "../components/feedback/FeedbackStates";
import { PageHeader } from "../components/PageHeader";

export function NotFoundPage() {
  return (
    <section>
      <PageHeader title="Página não encontrada" description="O endereço pode estar incorreto ou não estar mais disponível." />
      <EmptyState
        title="Não encontramos esta página"
        description="Volte aos workspaces para continuar navegando no portal."
        action={<Link className="button-link" to="/workspaces">Voltar aos workspaces</Link>}
      />
    </section>
  );
}
