#!/usr/bin/env node
"use strict";

// Sonda independente de baixo nível (TCP/TLS puro, sem handshake MQTT) para categorizar uma
// falha de conexão que o nó `mqtt out` do Node-RED já reportou de forma genérica ("Connection
// failed to broker"). O core do Node-RED descarta o erro real do cliente MQTT (10-mqtt.js:
// `node._clientOn('error', function (error) {})` -- handler vazio, "o próprio reconnect trata"),
// então não existe forma de extrair a causa (DNS/timeout/recusado/etc.) só reprocessando o log.
// Roda como subprocesso curto (chamado via execFileSync com timeout) para não acoplar o daemon
// do harness a um loop assíncrono só por causa disso.
//
// Uso: node mqtt-probe.js <host> <port> <useTls:true|false>
// Saída (stdout, uma linha JSON): { category, detail, probedAtIso }
//
// Limitação conhecida: a sondagem roda alguns segundos DEPOIS da falha original (no próximo tick
// do harness), então uma falha transitória já pode ter se resolvido -- nesse caso a categoria é
// "reachable_at_probe_time", não uma reclassificação da falha original. Também não faz o handshake
// MQTT (CONNECT/CONNACK), então não distingue usuário/senha inválidos de ACL negada -- só prova
// que o transporte (TCP/TLS) está de pé, o que já reduz bastante o espaço de causas possíveis.

const net = require("net");
const tls = require("tls");

const TIMEOUT_MS = 5000;

function emit(category, detail) {
  process.stdout.write(
    JSON.stringify({ category, detail: detail || null, probedAtIso: new Date().toISOString() })
  );
}

function classifyError(err) {
  const code = err && err.code;
  if (code === "ENOTFOUND" || code === "EAI_AGAIN") return "dns";
  if (code === "ECONNREFUSED") return "refused";
  if (code === "ETIMEDOUT") return "timeout";
  if (code === "ECONNRESET") return "reset";
  if (code === "EHOSTUNREACH" || code === "ENETUNREACH") return "network_unreachable";
  if (String(code).startsWith("ERR_TLS") || err.library) return "tls";
  return `unknown:${code || "?"}`;
}

function main() {
  const [, , host, portArg, useTlsArg] = process.argv;
  const port = Number(portArg);
  const useTls = useTlsArg === "true";

  if (!host || !Number.isInteger(port)) {
    emit("probe_failed", "argumentos inválidos: uso node mqtt-probe.js <host> <port> <useTls>");
    process.exit(1);
  }

  let finished = false;
  const finish = (category, detail) => {
    if (finished) return;
    finished = true;
    try {
      socket.destroy();
    } catch {
      /* socket já pode estar fechado -- não é um erro que precise de tratamento aqui */
    }
    emit(category, detail);
    process.exit(0);
  };

  // rejectUnauthorized:false é proposital -- este é só um teste de alcançabilidade de
  // transporte, não uma validação de identidade do broker; validar a cadeia exigiria plumbing
  // do mesmo CA/cert que o Node-RED usa, fora de escopo para uma sondagem best-effort.
  const socket = useTls
    ? tls.connect({ host, port, timeout: TIMEOUT_MS, rejectUnauthorized: false })
    : net.connect({ host, port, timeout: TIMEOUT_MS });

  const readyEvent = useTls ? "secureConnect" : "connect";
  socket.on(readyEvent, () =>
    finish(
      "reachable_at_probe_time",
      `TCP${useTls ? "+TLS" : ""} connect OK -- falha original pode ter sido transitória ou no nível CONNACK (auth/protocolo MQTT, fora do alcance desta sondagem)`
    )
  );
  socket.on("timeout", () => finish("timeout", `sem resposta em ${TIMEOUT_MS}ms`));
  socket.on("error", (err) => finish(classifyError(err), err.message));
}

main();
