#!/usr/bin/env node
"use strict";

const fs = require("fs");
const os = require("os");
const path = require("path");
const crypto = require("crypto");
const { execFileSync } = require("child_process");

const db = require("./db.js");

// Carrega o mesmo arquivo que o systemd injeta via EnvironmentFile=- na unit, para que
// `edgewarden-test <comando>` rodado à mão via SSH veja exatamente a mesma configuração que o
// daemon gerenciado pelo systemd -- sem isso, um operador rodando o CLI direto (sem ter exportado
// as mesmas variáveis do serviço) pode criar/ler uma sessão num gateway_id/caminho de banco
// diferente do que o daemon está observando, sem aviso nenhum.
function loadEnvironmentFileIfPresent() {
  const envFilePath = process.env.EDGEWARDEN_TEST_ENV_FILE || "/etc/edgewarden-test/environment";
  let content;
  try {
    content = fs.readFileSync(envFilePath, "utf8");
  } catch {
    return; // arquivo opcional -- ausência é normal em ambiente local/dev
  }
  for (const line of content.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eq = trimmed.indexOf("=");
    if (eq === -1) continue;
    const key = trimmed.slice(0, eq).trim();
    const value = trimmed.slice(eq + 1).trim();
    // Uma variável já definida no ambiente do processo (export explícito do operador) sempre
    // vence sobre o arquivo -- mesma precedência que dotenv-style loaders.
    if (key && !(key in process.env)) process.env[key] = value;
  }
}

loadEnvironmentFileIfPresent();

const config = {
  gatewayId: process.env.EDGEWARDEN_TEST_GATEWAY_ID || "edgewarden",
  dbPath: process.env.EDGEWARDEN_TEST_DB_PATH || "/var/lib/edgewarden-test/test.db",
  targetDurationSeconds: positiveInt("EDGEWARDEN_TEST_TARGET_DURATION_SECONDS", 86400),
  checkpointIntervalSeconds: positiveInt("EDGEWARDEN_TEST_CHECKPOINT_INTERVAL_SECONDS", 30),
  spoolScriptPath:
    process.env.EDGEWARDEN_TEST_SPOOL_SCRIPT_PATH ||
    "/home/junior/.node-red/fluxo-gateway/scripts/gateway-spool.js",
  nodeRedServiceName: process.env.EDGEWARDEN_TEST_NODE_RED_SERVICE || "nodered",
  netInterface: process.env.EDGEWARDEN_TEST_NET_IFACE || "wlan0",
  diskWarningPercent: positiveInt("EDGEWARDEN_TEST_DISK_WARNING_PERCENT", 80),
  diskCriticalPercent: positiveInt("EDGEWARDEN_TEST_DISK_CRITICAL_PERCENT", 90)
};

function positiveInt(name, fallback) {
  const value = Number.parseInt(process.env[name] || "", 10);
  return Number.isSafeInteger(value) && value > 0 ? value : fallback;
}

function log(message) {
  process.stdout.write(`${new Date().toISOString()} ${message}\n`);
}

function newTestId() {
  return crypto.randomUUID();
}

// --- Observadores best-effort: nunca derrubam o daemon, retornam null/defaults se algo faltar
// (ex.: rodando localmente no Windows para testar só a máquina de estados, sem systemd/journalctl/
// sensores Linux disponíveis).

function readSpoolStatus() {
  try {
    const output = execFileSync("node", [config.spoolScriptPath, "status"], {
      encoding: "utf8",
      timeout: 5000
    });
    return JSON.parse(output.trim());
  } catch (error) {
    log(`WARN: gateway-spool status indisponível: ${error.message}`);
    return null;
  }
}

function readSystemStats() {
  const stats = {};
  try {
    stats.cpuTempC =
      Number(fs.readFileSync("/sys/class/thermal/thermal_zone0/temp", "utf8")) / 1000;
  } catch {
    stats.cpuTempC = null;
  }
  try {
    const total = os.totalmem();
    stats.memUsedPercent = ((total - os.freemem()) / total) * 100;
  } catch {
    stats.memUsedPercent = null;
  }
  try {
    const stat = fs.statfsSync("/");
    stats.diskUsedPercent = ((stat.blocks - stat.bavail) / stat.blocks) * 100;
    stats.diskFreeBytes = stat.bavail * stat.bsize;
  } catch {
    stats.diskUsedPercent = null;
    stats.diskFreeBytes = null;
  }
  try {
    stats.load1m = os.loadavg()[0];
  } catch {
    stats.load1m = null;
  }
  try {
    stats.netRxBytes = Number(
      fs.readFileSync(`/sys/class/net/${config.netInterface}/statistics/rx_bytes`, "utf8")
    );
    stats.netTxBytes = Number(
      fs.readFileSync(`/sys/class/net/${config.netInterface}/statistics/tx_bytes`, "utf8")
    );
    stats.netOperstate = fs
      .readFileSync(`/sys/class/net/${config.netInterface}/operstate`, "utf8")
      .trim();
  } catch {
    stats.netRxBytes = null;
    stats.netTxBytes = null;
    stats.netOperstate = null;
  }
  return stats;
}

// Cursor de journalctl: guardamos o timestamp do último evento MQTT já classificado para não
// reprocessar o mesmo log a cada tick.
let journalCursorIso = new Date().toISOString();

// Estado conhecido mais recente de conexão MQTT, independente de ter havido uma transição *nova*
// desde o último tick -- sem isso, um ensaio de 24h com conexão estável mostraria "?" (desconhecido)
// o tempo todo, porque só saberíamos o estado quando ele *mudasse*. Inicializado por uma varredura
// única do journal inteiro do boot atual, feita uma vez no start do daemon.
let lastKnownMqttConnected = null;

function bootstrapMqttState() {
  let raw;
  try {
    raw = execFileSync("journalctl", ["-u", config.nodeRedServiceName, "-b", "-o", "cat", "--no-pager"], {
      encoding: "utf8",
      timeout: 8000
    });
  } catch (error) {
    return;
  }
  for (const line of raw.split("\n")) {
    if (/Connected to broker/.test(line)) lastKnownMqttConnected = true;
    else if (/Connection failed to broker/.test(line) || /disconnected/i.test(line))
      lastKnownMqttConnected = false;
  }
}

function extractBrokerAddress(logLine) {
  const match = /mqtts?:\/\/([^:/\s]+):(\d+)/.exec(logLine);
  if (!match) return null;
  return { host: match[1], port: Number(match[2]), useTls: /mqtts:\/\//.test(logLine) };
}

// Sonda best-effort (nunca derruba o daemon) para categorizar um COMMUNICATION_LOST -- ver
// comentário de topo em mqtt-probe.js para o porquê de precisar de uma sondagem separada em vez
// de só ler o log do Node-RED.
function probeMqttFailure(logLine) {
  const address = extractBrokerAddress(logLine);
  if (!address) return null;
  try {
    const output = execFileSync(
      "node",
      [path.join(__dirname, "mqtt-probe.js"), address.host, String(address.port), String(address.useTls)],
      { encoding: "utf8", timeout: 6000 }
    );
    return JSON.parse(output.trim());
  } catch (error) {
    log(`WARN: sondagem de falha MQTT indisponível: ${error.message}`);
    return { category: "probe_failed", detail: error.message, probedAtIso: new Date().toISOString() };
  }
}

function tailNodeRedJournal() {
  let raw;
  try {
    raw = execFileSync(
      "journalctl",
      ["-u", config.nodeRedServiceName, "--since", journalCursorIso, "-o", "cat", "--no-pager"],
      { encoding: "utf8", timeout: 5000 }
    );
  } catch (error) {
    return [];
  }
  journalCursorIso = new Date().toISOString();
  const events = [];
  for (const line of raw.split("\n")) {
    if (/Connected to broker/.test(line)) events.push({ type: "COMMUNICATION_RECOVERED", line });
    else if (/Connection failed to broker/.test(line))
      events.push({ type: "COMMUNICATION_LOST", line });
    else if (/Recovered stale gateway spool lock/.test(line))
      events.push({ type: "STALE_LOCK_RECOVERED", line });
  }
  return events;
}

// --- Máquina de estados

const TERMINAL_STATES = new Set(["COMPLETED", "FAILED"]);
const ACTIVE_STATES = new Set([
  "CREATED",
  "RUNNING",
  "PAUSED_UNEXPECTEDLY",
  "RECOVERING",
  "RUNNING_DEGRADED",
  "FINALIZING"
]);

function transition(database, session, newState, { eventType, severity = "INFO", description, metadata } = {}) {
  const updated = db.updateSession(database, session.test_id, { state: newState });
  if (eventType) {
    db.insertEvent(database, {
      testId: session.test_id,
      gatewayId: session.gateway_id,
      eventType,
      severity,
      description: description || `Transição de estado: ${session.state} -> ${newState}`,
      metadata
    });
  }
  log(`[${session.test_id}] ${session.state} -> ${newState}${description ? " (" + description + ")" : ""}`);
  return updated;
}

// Ao subir: decide se cria sessão nova, retoma uma existente, ou fica ocioso.
function adoptOrResumeSession(database) {
  let session = db.getLatestSession(database, config.gatewayId);

  if (!session || TERMINAL_STATES.has(session.state)) {
    // Não retoma sessão concluída/falha automaticamente (seção 10 da spec: "state == COMPLETED? SIM -> não retomar").
    // Fica ocioso esperando `edgewarden-test start` criar uma sessão nova (CREATED).
    return null;
  }

  if (session.state === "CREATED") {
    session = transition(database, session, "RUNNING", {
      eventType: "TEST_STARTED",
      description: "Ensaio iniciado",
      metadata: { targetDurationSeconds: session.test_target_duration_seconds }
    });
    return session;
  }

  // Sessão estava em progresso quando o processo (ou o Pi) parou. Distingue shutdown limpo de sujo.
  const wasClean = session.last_shutdown_reason === "clean";
  db.updateSession(database, session.test_id, { last_shutdown_reason: null });

  if (!wasClean) {
    session = { ...session, last_shutdown_reason: null };
    session = transition(database, session, "PAUSED_UNEXPECTEDLY", {
      eventType: "UNEXPECTED_SHUTDOWN_DETECTED",
      severity: "WARN",
      description: "Reinício sem marcador de shutdown limpo — tratado como interrupção inesperada"
    });
    db.updateSession(database, session.test_id, {
      unexpected_shutdown_count: session.unexpected_shutdown_count + 1
    });
    session = { ...session, unexpected_shutdown_count: session.unexpected_shutdown_count + 1 };

    session = transition(database, session, "RECOVERING", {
      eventType: "TEST_RECOVERED",
      description: "Checkpoint e fila local recuperados; retomando ensaio"
    });
  }

  const targetState = session.state === "FINALIZING" ? "FINALIZING" : "RUNNING";
  session = transition(database, session, targetState, {
    eventType: wasClean ? undefined : undefined,
    description: "Retomando monitoramento"
  });
  return session;
}

// Cria a sessão em CREATED e NÃO avança para RUNNING aqui. A transição CREATED->RUNNING é
// responsabilidade exclusiva do daemon (adoptOrResumeSession no boot, ou o loop ocioso em main()
// se o daemon já estiver rodando) -- um único código faz essa transição, para que um processo de
// CLI de vida curta (que cria a sessão e sai) nunca seja confundido com "o processo que estava
// rodando o ensaio e caiu", o que geraria um UNEXPECTED_SHUTDOWN_DETECTED falso-positivo.
function createNewSession(database) {
  const testId = process.env.EDGEWARDEN_TEST_ID || newTestId();
  const session = db.createSession(database, {
    testId,
    gatewayId: config.gatewayId,
    targetDurationSeconds: config.targetDurationSeconds
  });
  log(`[${session.test_id}] sessão criada em CREATED; aguardando o daemon assumir`);
  return session;
}

// --- Loop principal

function runTick(database, session, lastTickMonotonicNs) {
  const nowMonotonicNs = process.hrtime.bigint();
  const elapsedSeconds = Number(nowMonotonicNs - lastTickMonotonicNs) / 1e9;
  const accumulated = session.accumulated_runtime_seconds + elapsedSeconds;
  const remaining = Math.max(0, session.test_target_duration_seconds - accumulated);

  const spoolStatus = readSpoolStatus();
  const sysStats = readSystemStats();
  const journalEvents = tailNodeRedJournal();

  for (const evt of journalEvents) {
    const severity = evt.type === "COMMUNICATION_LOST" ? "WARN" : "INFO";
    const metadata = evt.type === "COMMUNICATION_LOST" ? probeMqttFailure(evt.line) : undefined;
    db.insertEvent(database, {
      testId: session.test_id,
      gatewayId: session.gateway_id,
      eventType: evt.type,
      severity,
      description: evt.line.slice(0, 300),
      metadata
    });
    log(`[${session.test_id}] evento: ${evt.type}${metadata ? ` [categoria=${metadata.category}]` : ""}`);
  }

  const previousCheckpoint = db.getLatestCheckpoint(database, session.test_id);
  const previousDepth = previousCheckpoint ? previousCheckpoint.pending_metrics_count : null;
  const currentDepth = spoolStatus ? spoolStatus.depth : null;

  if (currentDepth !== null && previousDepth !== null) {
    if (previousDepth === 0 && currentDepth > 0) {
      db.insertEvent(database, {
        testId: session.test_id,
        gatewayId: session.gateway_id,
        eventType: "BACKLOG_STARTED",
        severity: "WARN",
        description: `Fila local começou a crescer (depth=${currentDepth})`
      });
    } else if (previousDepth > 0 && currentDepth === 0) {
      db.insertEvent(database, {
        testId: session.test_id,
        gatewayId: session.gateway_id,
        eventType: "BACKLOG_DRAINED",
        description: "Fila local voltou a zero"
      });
    }
  }

  if (sysStats.diskUsedPercent !== null) {
    const wasCritical = previousCheckpoint && previousCheckpoint.disk_used_percent >= config.diskCriticalPercent;
    const wasWarning = previousCheckpoint && previousCheckpoint.disk_used_percent >= config.diskWarningPercent;
    const nowCritical = sysStats.diskUsedPercent >= config.diskCriticalPercent;
    const nowWarning = sysStats.diskUsedPercent >= config.diskWarningPercent;
    if (nowCritical && !wasCritical) {
      db.insertEvent(database, {
        testId: session.test_id,
        gatewayId: session.gateway_id,
        eventType: "LOCAL_STORAGE_WARNING",
        severity: "CRITICAL",
        description: `Disco em ${sysStats.diskUsedPercent.toFixed(1)}% — nível CRITICAL`
      });
    } else if (nowWarning && !wasWarning) {
      db.insertEvent(database, {
        testId: session.test_id,
        gatewayId: session.gateway_id,
        eventType: "LOCAL_STORAGE_WARNING",
        severity: "WARN",
        description: `Disco em ${sysStats.diskUsedPercent.toFixed(1)}% — nível WARNING`
      });
    }
  }

  let nextState = session.state;
  if (nextState === "RECOVERING") nextState = "RUNNING";

  for (const evt of journalEvents) {
    if (evt.type === "COMMUNICATION_RECOVERED") lastKnownMqttConnected = true;
    else if (evt.type === "COMMUNICATION_LOST") lastKnownMqttConnected = false;
  }
  const mqttConnected = lastKnownMqttConnected;

  const degraded = currentDepth !== null && currentDepth > 0;
  if (nextState === "RUNNING" && degraded) nextState = "RUNNING_DEGRADED";
  else if (nextState === "RUNNING_DEGRADED" && !degraded) nextState = "RUNNING";

  let captureCompleted = session.capture_completed;
  let deliveryCompleted = session.delivery_completed;

  if (accumulated >= session.test_target_duration_seconds && nextState !== "FINALIZING" && nextState !== "COMPLETED") {
    nextState = "FINALIZING";
    db.insertEvent(database, {
      testId: session.test_id,
      gatewayId: session.gateway_id,
      eventType: "TEST_FINALIZING",
      description: "Duração alvo atingida; aguardando fila local esvaziar antes de concluir"
    });
  }

  if (nextState === "FINALIZING" && !captureCompleted) {
    captureCompleted = 1;
    db.insertEvent(database, {
      testId: session.test_id,
      gatewayId: session.gateway_id,
      eventType: "TEST_CAPTURE_COMPLETED",
      description: "24h de captura efetiva concluídas; entrega do backlog pode continuar pendente"
    });
  }

  if (nextState === "FINALIZING" && currentDepth === 0) {
    deliveryCompleted = 1;
    nextState = "COMPLETED";
  }

  db.insertCheckpoint(database, {
    testId: session.test_id,
    state: nextState,
    accumulatedRuntimeSeconds: accumulated,
    remainingRuntimeSeconds: remaining,
    lastMetricSequence: spoolStatus ? spoolStatus.sequence : session.last_metric_sequence,
    pendingMetricsCount: currentDepth,
    queueBytes: spoolStatus ? spoolStatus.bytes : null,
    mqttConnected,
    cpuTempC: sysStats.cpuTempC,
    memUsedPercent: sysStats.memUsedPercent,
    diskUsedPercent: sysStats.diskUsedPercent,
    diskFreeBytes: sysStats.diskFreeBytes,
    load1m: sysStats.load1m,
    netRxBytes: sysStats.netRxBytes,
    netTxBytes: sysStats.netTxBytes,
    netOperstate: sysStats.netOperstate
  });

  const updateFields = {
    state: nextState,
    accumulated_runtime_seconds: accumulated,
    last_checkpoint_at: db.nowIso(),
    capture_completed: captureCompleted,
    delivery_completed: deliveryCompleted
  };
  if (spoolStatus) updateFields.last_metric_sequence = spoolStatus.sequence;
  if (nextState === "COMPLETED") updateFields.test_completed_at = db.nowIso();

  const updated = db.updateSession(database, session.test_id, updateFields);

  if (nextState === "COMPLETED" && session.state !== "COMPLETED") {
    db.insertEvent(database, {
      testId: session.test_id,
      gatewayId: session.gateway_id,
      eventType: "TEST_COMPLETED",
      description: "Duração alvo atingida e fila local drenada — ensaio concluído"
    });
    log(`[${session.test_id}] ENSAIO CONCLUÍDO`);
  }

  return { session: updated, monotonicNs: nowMonotonicNs };
}

function installShutdownHandlers(database, getSession) {
  const handler = (signal) => {
    const session = getSession();
    if (session && ACTIVE_STATES.has(session.state)) {
      db.updateSession(database, session.test_id, { last_shutdown_reason: "clean" });
      db.insertEvent(database, {
        testId: session.test_id,
        gatewayId: session.gateway_id,
        eventType: "SHUTDOWN_REQUESTED",
        description: `Encerramento solicitado (${signal}); checkpoint final gravado`
      });
      log(`[${session.test_id}] shutdown limpo (${signal})`);
    }
    process.exit(0);
  };
  process.on("SIGTERM", () => handler("SIGTERM"));
  process.on("SIGINT", () => handler("SIGINT"));
}

function main() {
  const database = db.openDatabase(config.dbPath);
  let session = adoptOrResumeSession(database);
  bootstrapMqttState();

  installShutdownHandlers(database, () => session);

  if (!session) {
    log(`Nenhum ensaio ativo para gateway '${config.gatewayId}'. Aguardando 'edgewarden-test start'.`);
  }

  let lastTickMonotonicNs = process.hrtime.bigint();

  const timer = setInterval(() => {
    if (!session) {
      // Ocioso: procura sessão CREATED nova a cada tick.
      const latest = db.getLatestSession(database, config.gatewayId);
      if (latest && latest.state === "CREATED") {
        session = transition(database, latest, "RUNNING", {
          eventType: "TEST_STARTED",
          description: "Ensaio iniciado"
        });
        lastTickMonotonicNs = process.hrtime.bigint();
      }
      return;
    }
    const result = runTick(database, session, lastTickMonotonicNs);
    session = result.session;
    lastTickMonotonicNs = result.monotonicNs;
    if (session.state === "COMPLETED") {
      session = null; // volta a ficar ocioso, pronto para um novo ensaio
    }
  }, config.checkpointIntervalSeconds * 1000);
  void timer;
}

if (require.main === module) {
  main();
}

module.exports = { config, adoptOrResumeSession, createNewSession, runTick, TERMINAL_STATES, ACTIVE_STATES };
