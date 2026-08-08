#!/usr/bin/env node
"use strict";

const { execFileSync } = require("child_process");
const db = require("./db.js");
const { config, createNewSession, TERMINAL_STATES, ACTIVE_STATES } = require("./harness.js");

const SERVICE_NAME = process.env.EDGEWARDEN_TEST_SYSTEMD_UNIT || "edgewarden-test-harness";

function openDb() {
  return db.openDatabase(config.dbPath);
}

function systemctl(args) {
  // `systemctl start/stop` em uma unit de sistema (não --user) exige privilégio elevado; o mesmo
  // padrão de `sudo systemctl stop/start nodered` já usado no restante do projeto.
  try {
    return execFileSync("sudo", ["systemctl", ...args], {
      encoding: "utf8",
      timeout: 5000,
      stdio: ["ignore", "pipe", "pipe"]
    });
  } catch (error) {
    return null;
  }
}

function formatDuration(totalSeconds) {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hh = String(Math.floor(s / 3600)).padStart(2, "0");
  const mm = String(Math.floor((s % 3600) / 60)).padStart(2, "0");
  const ss = String(s % 60).padStart(2, "0");
  return `${hh}:${mm}:${ss}`;
}

function formatBytes(bytes) {
  if (bytes === null || bytes === undefined) return "?";
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(1)} ${units[unit]}`;
}

function cmdStart() {
  // Ordem importa: `systemctl start` primeiro. O diretório de estado (/var/lib/edgewarden-test)
  // só existe depois que o systemd o cria via StateDirectory= no boot da unit -- em um Pi onde o
  // serviço nunca rodou, tentar abrir/criar o banco ANTES disso falha com EACCES (junior não pode
  // criar subdiretório em /var/lib, que é do root). Depois que a unit sobe uma vez, o diretório já
  // existe com dono junior:junior e o CLI consegue escrever nele normalmente.
  const out = systemctl(["start", SERVICE_NAME]);
  if (out === null) {
    const stateDir = require("path").dirname(config.dbPath);
    console.log(
      `AVISO: não foi possível chamar 'sudo systemctl start ${SERVICE_NAME}' (systemctl/sudo indisponível ` +
        `neste ambiente, ou serviço ainda não instalado). Se o diretório de estado ainda não existir, crie-o ` +
        `manualmente antes de continuar: sudo mkdir -p ${stateDir} && sudo chown junior:junior ${stateDir}`
    );
  } else {
    console.log(`systemctl start ${SERVICE_NAME}: ok`);
  }

  // `systemctl start` retorna assim que o processo é criado; a criação de
  // /var/lib/edgewarden-test pelo systemd (StateDirectory=) e a abertura do banco pelo daemon
  // podem levar uma fração de segundo a mais. Tenta algumas vezes antes de desistir, em vez de
  // falhar de cara com EACCES/ENOENT numa corrida com o próprio boot do serviço.
  let database;
  let lastError;
  for (let attempt = 0; attempt < 10; attempt += 1) {
    try {
      database = openDb();
      lastError = null;
      break;
    } catch (error) {
      lastError = error;
      sleepSync(300);
    }
  }
  if (!database) throw lastError;

  const existing = db.getLatestSession(database, config.gatewayId);
  if (existing && ACTIVE_STATES.has(existing.state)) {
    console.log(`Já existe um ensaio ativo (${existing.test_id}, estado ${existing.state}). O serviço vai retomá-lo.`);
  } else {
    const session = createNewSession(database);
    console.log(`Ensaio criado: ${session.test_id} (gateway=${session.gateway_id}, alvo=${config.targetDurationSeconds}s)`);
    console.log("O daemon (já rodando) vai adotar essa sessão CREATED no próximo ciclo.");
  }
}

function sleepSync(ms) {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    /* busy-wait curto e deliberado -- só usado no retry de start, não no loop principal */
  }
}

function cmdStop() {
  console.log(
    "Solicitando parada manual controlada (o ensaio NÃO é considerado concluído; pode ser retomado com 'start')."
  );
  const out = systemctl(["stop", SERVICE_NAME]);
  if (out === null) {
    console.log(`AVISO: não foi possível chamar 'systemctl stop ${SERVICE_NAME}'. Envie SIGTERM manualmente ao processo.`);
  } else {
    console.log(`systemctl stop ${SERVICE_NAME}: ok`);
  }
}

function cmdStatus() {
  const database = openDb();
  const session = db.getLatestSession(database, config.gatewayId);
  if (!session) {
    console.log(`Nenhum ensaio encontrado para gateway '${config.gatewayId}'.`);
    return;
  }
  const checkpoint = db.getLatestCheckpoint(database, session.test_id);
  const remaining = Math.max(0, session.test_target_duration_seconds - session.accumulated_runtime_seconds);

  console.log(`Test ID:             ${session.test_id}`);
  console.log(`Gateway:              ${session.gateway_id}`);
  console.log(`State:                ${session.state}`);
  console.log(`Elapsed runtime:      ${formatDuration(session.accumulated_runtime_seconds)}`);
  console.log(`Remaining runtime:    ${formatDuration(remaining)}`);
  console.log(`Capture completed:    ${session.capture_completed ? "yes" : "no"}`);
  console.log(`Delivery completed:   ${session.delivery_completed ? "yes" : "no"}`);
  console.log("");
  if (checkpoint) {
    console.log(`Last checkpoint:      ${checkpoint.checkpoint_at}`);
    console.log(`Metrics sequence:     ${checkpoint.last_metric_sequence ?? "?"}`);
    console.log(`Pending (queue):      ${checkpoint.pending_metrics_count ?? "?"} (${formatBytes(checkpoint.queue_bytes)})`);
    console.log(
      `MQTT connected:       ${checkpoint.mqtt_connected === null ? "?" : checkpoint.mqtt_connected ? "ONLINE" : "OFFLINE"}`
    );
    console.log(`CPU temp:             ${checkpoint.cpu_temp_c ?? "?"} °C`);
    console.log(`Memory used:          ${checkpoint.mem_used_percent?.toFixed?.(1) ?? "?"}%`);
    console.log(`Disk used / free:     ${checkpoint.disk_used_percent?.toFixed?.(1) ?? "?"}% / ${formatBytes(checkpoint.disk_free_bytes)}`);
    console.log(`Load (1m):            ${checkpoint.load_1m ?? "?"}`);
  } else {
    console.log("Nenhum checkpoint gravado ainda.");
  }
  console.log("");
  console.log(`Unexpected restarts:  ${session.unexpected_shutdown_count}`);
  console.log(`Test started at:      ${session.test_started_at}`);
  if (session.test_completed_at) console.log(`Test completed at:    ${session.test_completed_at}`);
}

function cmdEvents(args) {
  const database = openDb();
  const session = db.getLatestSession(database, config.gatewayId);
  if (!session) {
    console.log(`Nenhum ensaio encontrado para gateway '${config.gatewayId}'.`);
    return;
  }
  const limitArg = args.find((a) => a.startsWith("--limit="));
  const limit = limitArg ? Number.parseInt(limitArg.split("=")[1], 10) : 50;
  const events = db.listEvents(database, session.test_id, { limit });
  for (const e of events.reverse()) {
    console.log(`${e.timestamp}  [${e.severity.padEnd(8)}] ${e.event_type.padEnd(28)} ${e.description}`);
  }
}

function cmdReport() {
  const database = openDb();
  const session = db.getLatestSession(database, config.gatewayId);
  if (!session) {
    console.log(`Nenhum ensaio encontrado para gateway '${config.gatewayId}'.`);
    return;
  }
  const events = db.listEvents(database, session.test_id, { limit: 100000 });
  const byType = {};
  for (const e of events) byType[e.event_type] = (byType[e.event_type] || 0) + 1;

  console.log(`Resumo local do ensaio ${session.test_id} (gateway ${session.gateway_id})`);
  console.log(`Estado atual: ${session.state}`);
  console.log(`Tempo acumulado: ${formatDuration(session.accumulated_runtime_seconds)} / alvo ${formatDuration(session.test_target_duration_seconds)}`);
  console.log(`Reinícios inesperados: ${session.unexpected_shutdown_count}`);
  console.log("Contagem de eventos por tipo:");
  for (const [type, count] of Object.entries(byType).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${type.padEnd(30)} ${count}`);
  }
  console.log("");
  console.log(
    "Este é um resumo LOCAL (só o que o gateway sabe sobre si mesmo). Para o relatório completo, " +
      "cruzando com o que o Fluxo efetivamente confirmou (sequences aceitas, rejeições, duplicatas), " +
      "rode 'scripts/report.js' a partir do host que hospeda o Fluxo — o Postgres não é alcançável " +
      "pela rede a partir do Pi."
  );
}

function main() {
  const [, , command, ...args] = process.argv;
  switch (command) {
    case "start":
      return cmdStart();
    case "stop":
      return cmdStop();
    case "status":
      return cmdStatus();
    case "events":
      return cmdEvents(args);
    case "report":
      return cmdReport();
    default:
      console.log("uso: edgewarden-test start|stop|status|events [--limit=N]|report");
      process.exitCode = 1;
  }
}

main();
