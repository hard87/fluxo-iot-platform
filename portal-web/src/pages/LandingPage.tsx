import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import "../styles/landing.css";
const examples = {
    ambiente: { title: "Acompanhe condições do ambiente.", copy: "Consulte temperatura e umidade ao longo do tempo. Configure limites para suas métricas e investigue os eventos que exigem atenção.", metric: "Temperatura", condition: "Acima de 8 °C" },
    energia: { title: "Entenda a evolução das medições.", copy: "Explore o histórico de potência, corrente e outras métricas enviadas pelos seus medidores. Defina limites conforme o contexto da sua operação.", metric: "Potência", condition: "Acima de 12 kW" },
    equipamentos: { title: "Acompanhe os sinais dos equipamentos.", copy: "Consulte temperatura, vibração ou estados enviados pelo dispositivo. Configure regras para sinalizar valores fora dos limites definidos pela equipe.", metric: "Temperatura do motor", condition: "Acima de 75 °C" },
};
const iconPaths: Record<string, string> = {
    "activity": "M2 12h4l3-8 6 16 3-8h4",
    "arrow-up-right": "M7 17 17 7M7 7h10v10",
    "arrow-right": "M4 12h16m-6-6 6 6-6 6",
    "triangle-alert": "m12 3 10 18H2L12 3Zm0 6v5m0 3v1",
    "key-round": "M14 7a5 5 0 1 1-3 9l-7 5-2-2 5-7a5 5 0 0 1 7-5Zm2 2h.01",
    "radio": "M5 5a10 10 0 0 0 0 14M19 5a10 10 0 0 1 0 14M8 8a6 6 0 0 0 0 8M16 8a6 6 0 0 1 0 8M12 11v2",
    "chart-no-axes-combined": "M3 17 9 11l4 3 8-10M3 21V3m0 18h18",
    "chart-line": "M3 3v18h18M7 14l4-5 4 3 5-7",
    "bell": "M6 8a6 6 0 0 1 12 0v8l2 2H4l2-2V8Zm4 13h4",
    "bell-ring": "M6 8a6 6 0 0 1 12 0v8l2 2H4l2-2V8Zm4 13h4M2 3v4m20-4v4",
    "shield-check": "M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6l-9-4Zm-5 10 3 3 7-7",
};
function LandingIcon({ name }: {
    name: string;
}) {
    return <svg className="fl-glyph" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d={iconPaths[name]}/>
    </svg>;
}
export function LandingPage() {
    const { isAuthenticated } = useAuth();
    const [application, setApplication] = useState<keyof typeof examples>("ambiente");
    const example = examples[application];
    const destination = isAuthenticated ? "/workspaces" : "/register";
    return (<main id="fluxo-lp">
 <header className="fl-wrap fl-nav">
    <Link className="fl-logo" to="/" aria-label="Fluxo — página inicial">
    <LandingIcon name="activity"/>Fluxo</Link>
    <nav className="fl-navlinks" aria-label="Navegação">
    <a href="#fl-platform">Plataforma</a>
    <a href="#fl-use">Aplicações</a>
    <a href="#fl-start">Como começar</a>
    </nav>
    <div className="fl-access">{!isAuthenticated && <Link to="/login">Entrar</Link>}<Link className="fl-button" to={destination}>{isAuthenticated ? "Abrir plataforma" : "Criar conta"} <LandingIcon name="arrow-up-right"/>
    </Link>
    </div>
    </header>
 <section className="fl-wrap fl-hero" id="fl-top">
    <div>
    <div className="fl-kicker">Plataforma IoT · da conexão ao alerta</div>
    <h1>Seus dispositivos<br />enviam dados.<br />
    <span>Enxergue o que<br />está acontecendo.</span>
    </h1>
    <p className="fl-lead">Conecte dispositivos com segurança, acompanhe suas métricas e configure alertas. Do primeiro sensor à investigação de um evento, tudo no mesmo lugar.</p>
    <div className="fl-actions">
    <Link className="fl-button" to={destination}>{isAuthenticated ? "Abrir plataforma" : "Começar com o Fluxo"} <LandingIcon name="arrow-up-right"/>
    </Link>
    <a className="fl-button fl-outline" href="#fl-platform">Conhecer a plataforma</a>
    </div>
    <p className="fl-note">Para equipes técnicas e operações que estão construindo seu piloto IoT.</p>
    </div>
 <div>
    <div className="fl-demo">
    <div className="fl-demohead">
    <strong>Fluxo <span className="fl-demo-label">/ Telemetria</span>
    </strong>
    <span className="fl-pill">Exemplo ilustrativo</span>
    </div>
    <div className="fl-demobody">
    <div className="fl-demo-label">Workspace · Ambiente</div>
    <div className="fl-device-title">
    <b>Câmara fria 01</b>
    <span className="fl-pill">Último dado · há 12 s</span>
    </div>
    <div className="fl-demo-label">Temperatura · últimas 6 horas</div>
    <div className="fl-value">8,4 <small>°C</small>
    </div>
    <svg className="fl-chart" viewBox="0 0 380 148" role="img" aria-label="Exemplo: temperatura sobe de 4 para 8,4 graus entre 8 e 14 horas, ultrapassando o limite de 8 graus">
    <text x="0" y="13">°C</text>
    <path d="M32 22H365 M32 62H365 M32 102H365" fill="none" stroke="var(--fl-line)"/>
    <text x="7" y="26">10</text>
    <text x="13" y="66">6</text>
    <text x="13" y="106">2</text>
    <path d="M32 42H365" fill="none" stroke="currentColor" strokeDasharray="4 4" opacity=".55"/>
    <text x="255" y="35">Limite · 8 °C</text>
    <path d="M32 83L62 81L92 84L122 79L152 81L182 73L212 78L242 66L272 56L302 54L332 40L365 38" fill="none" stroke="currentColor" strokeWidth="2.5"/>
    <circle cx="365" cy="38" r="4" fill="currentColor"/>
    <text x="32" y="129">08:00</text>
    <text x="180" y="129">11:00</text>
    <text x="332" y="129">14:00</text>
    </svg>
    <div className="fl-alert">
    <LandingIcon name="triangle-alert"/>
    <p>
    <strong>Temperatura acima do limite</strong>
    <br />Regra: temperatura &gt; 8 °C · Evento ativo no portal</p>
    </div>
    </div>
    </div>
    <p className="fl-democaption">Representação conceitual com dados fictícios.</p>
    </div>
    </section>
 <div className="fl-wrap">
    <div className="fl-strip">
    <span>
    <LandingIcon name="key-round"/>Credenciais por dispositivo</span>
    <span>
    <LandingIcon name="radio"/>Telemetria via MQTT</span>
    <span>
    <LandingIcon name="chart-no-axes-combined"/>Histórico consultável</span>
    <span>
    <LandingIcon name="bell"/>Alertas no portal</span>
    </div>
    </div>
 <section id="fl-platform" className="fl-wrap fl-section">
    <div className="fl-kicker">Visibilidade para operar</div>
    <h2>Receber dados é só o começo.</h2>
    <p className="fl-sub">O Fluxo reúne as ferramentas para conectar, consultar e investigar — sem perder o contexto de cada dispositivo.</p>
    <div className="fl-three">
    <article className="fl-feature">
    <div className="fl-icon">
    <LandingIcon name="shield-check"/>
    </div>
    <h3>Conecte com segurança</h3>
    <p>Cadastre dispositivos no workspace e provisione credenciais próprias para cada conexão. Organize o acesso desde o primeiro sensor.</p>
    <footer>Identidade e acesso por dispositivo</footer>
    </article>
    <article className="fl-feature">
    <div className="fl-icon">
    <LandingIcon name="chart-line"/>
    </div>
    <h3>Explore além do último valor</h3>
    <p>Selecione dispositivos, métricas e períodos. Consulte séries históricas em gráficos ou tabelas e acompanhe como os sinais evoluem.</p>
    <footer>Explorer de telemetria e catálogo de métricas</footer>
    </article>
    <article className="fl-feature">
    <div className="fl-icon">
    <LandingIcon name="bell-ring"/>
    </div>
    <h3>Do limite ao evento</h3>
    <p>Configure regras para suas métricas, acompanhe eventos no portal e registre seu reconhecimento. Consulte o histórico para investigar o que ocorreu.</p>
    <footer>Regras, eventos e histórico de alertas</footer>
    </article>
    </div>
    </section>
 <section id="fl-use" className="fl-use">
    <div className="fl-wrap fl-section">
    <div className="fl-kicker">Aplicações possíveis</div>
    <h2>Diferentes sinais. A mesma visibilidade.</h2>
    <p className="fl-sub">Exemplos de como usar as métricas enviadas por dispositivos compatíveis.</p>
    <div className="fl-tabs" role="group" aria-label="Exemplos de aplicação">
    <button type="button" aria-pressed={application === "ambiente"} onClick={() => setApplication("ambiente")}>Ambiente</button>
    <button type="button" aria-pressed={application === "energia"} onClick={() => setApplication("energia")}>Energia</button>
    <button type="button" aria-pressed={application === "equipamentos"} onClick={() => setApplication("equipamentos")}>Equipamentos</button>
    </div>
    <div className="fl-usebody" aria-live="polite">
    <div>
    <h3 id="fl-use-title">{example.title}</h3>
    <p id="fl-use-copy">{example.copy}</p>
    </div>
    <div className="fl-rule">
    <div className="fl-demo-label">EXEMPLO DE REGRA CONFIGURÁVEL</div>
    <div className="fl-rule-line">
    <span>Métrica</span>
    <strong id="fl-metric">{example.metric}</strong>
    </div>
    <div className="fl-rule-line">
    <span>Condição</span>
    <strong id="fl-condition">{example.condition}</strong>
    </div>
    <div className="fl-rule-line">
    <span>Resposta</span>
    <strong>Evento e notificação no portal</strong>
    </div>
    <div className="fl-rulefooter">Limites definidos pela sua equipe · dados ilustrativos</div>
    </div>
    </div>
    </div>
    </section>
 <section id="fl-start" className="fl-wrap fl-section">
    <div className="fl-kicker">Seu primeiro piloto</div>
    <h2>Da conexão ao acompanhamento.</h2>
    <div className="fl-steps">
    <article>
    <div className="fl-stepno">01 / ORGANIZE</div>
    <h3>Crie seu workspace</h3>
    <p>Reúna os dispositivos do seu projeto e organize o acesso da equipe.</p>
    </article>
    <article>
    <div className="fl-stepno">02 / CONECTE</div>
    <h3>Envie sua telemetria</h3>
    <p>Provisione o dispositivo e integre o envio por MQTT com o contrato de dados do Fluxo.</p>
    </article>
    <article>
    <div className="fl-stepno">03 / ACOMPANHE</div>
    <h3>Explore e configure alertas</h3>
    <p>Consulte as métricas recebidas, acompanhe o histórico e defina regras para seu piloto.</p>
    </article>
    </div>
    </section>
 <section className="fl-wrap fl-section fl-faq">
    <h2>Antes de conectar.</h2>
    <details>
    <summary>Quais dispositivos posso integrar?</summary>
    <p>Dispositivos ou gateways capazes de se autenticar e publicar via MQTT no contrato de telemetria do Fluxo. A integração requer adequação do payload; a plataforma não presume compatibilidade automática com qualquer fabricante.</p>
    </details>
    <details>
    <summary>Como descubro problemas no envio dos dados?</summary>
    <p>Consulte o último dado recebido, o histórico de telemetria e as mensagens rejeitadas disponíveis no portal. O diagnóstico ajuda a investigar o envio e a validação dos dados.</p>
    </details>
    <details>
    <summary>Onde recebo os alertas?</summary>
    <p>Os eventos e as notificações ficam disponíveis no portal, com histórico e reconhecimento pela equipe.</p>
    </details>
    </section>
 <section className="fl-final">
    <div>
    <h2>Comece com um dispositivo.<br />Enxergue o próximo passo.</h2>
    <p>Conecte seu primeiro sensor e explore o que seus dados têm a mostrar.</p>
    </div>
    <Link className="fl-button" to={destination}>{isAuthenticated ? "Abrir plataforma" : "Criar minha conta"} <LandingIcon name="arrow-up-right"/>
    </Link>
    </section>
    <footer className="fl-wrap fl-footer">
    <strong>Fluxo · Plataforma IoT</strong>
    <span>Conexão segura. Dados compreensíveis. Visibilidade operacional.</span>
    </footer>
    </main>);
}
