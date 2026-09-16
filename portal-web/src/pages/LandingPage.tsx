import { Link } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";

export function LandingPage() {
  const { isAuthenticated } = useAuth();

  return (
    <main className="landing-page">
      <header className="landing-header">
        <Link className="landing-brand" to="/" aria-label="Fluxo — página inicial">
          <strong>Fluxo</strong>
          <span>Plataforma IoT</span>
        </Link>
        <nav className="landing-actions" aria-label="Acesso à plataforma">
          {isAuthenticated ? (
            <Link className="button-link" to="/workspaces">Abrir plataforma</Link>
          ) : (
            <>
              <Link className="button-link secondary" to="/login">Entrar</Link>
              <Link className="button-link" to="/register">Criar cadastro</Link>
            </>
          )}
        </nav>
      </header>

      <section className="landing-hero">
        <div>
          <p className="landing-eyebrow">Telemetria confiável, da borda à operação</p>
          <h1>Conecte dispositivos, acompanhe sinais e aja antes que a operação pare.</h1>
          <p className="landing-lead">
            O Fluxo reúne provisionamento seguro, ingestão de telemetria, métricas operacionais e
            alertas em uma plataforma preparada para pilotos IoT reais.
          </p>
          <div className="landing-cta">
            <Link className="button-link" to={isAuthenticated ? "/workspaces" : "/register"}>
              {isAuthenticated ? "Ir para meus workspaces" : "Começar agora"}
            </Link>
            {!isAuthenticated ? <Link className="button-link secondary" to="/login">Já tenho uma conta</Link> : null}
          </div>
        </div>
        <aside className="landing-signal" aria-label="Resumo das capacidades do Fluxo">
          <p>Operação em um só lugar</p>
          <ul>
            <li><strong>Dispositivos</strong><span>Cadastro e credenciais isoladas</span></li>
            <li><strong>Telemetria</strong><span>Histórico e exploração de métricas</span></li>
            <li><strong>Alertas</strong><span>Regras, eventos e notificações</span></li>
            <li><strong>Confiabilidade</strong><span>Rejeições auditáveis e diagnóstico</span></li>
          </ul>
        </aside>
      </section>

      <section className="landing-features" aria-labelledby="landing-features-title">
        <div className="landing-section-heading">
          <p className="landing-eyebrow">Visibilidade operacional</p>
          <h2 id="landing-features-title">Do primeiro dispositivo ao acompanhamento contínuo</h2>
        </div>
        <article><h3>Integração segura</h3><p>Provisionamento por workspace e credenciais próprias para cada dispositivo.</p></article>
        <article><h3>Dados compreensíveis</h3><p>Gráficos, tabelas acessíveis e catálogo tipado de métricas para investigar o comportamento.</p></article>
        <article><h3>Resposta operacional</h3><p>Alertas com histórico, notificações e uma trilha explícita para mensagens rejeitadas.</p></article>
      </section>
    </main>
  );
}
