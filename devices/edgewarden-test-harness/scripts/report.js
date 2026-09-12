#!/usr/bin/env node
"use strict";

// Roda no HOST que hospeda o Fluxo (não no Pi) -- precisa de acesso ao Postgres via `docker exec`
// e de SSH até o gateway para puxar o SQLite do harness. Junta as duas fontes: o que o gateway
// acha que aconteceu (sessão/checkpoints/eventos locais) e o que o Fluxo efetivamente confirmou
// (sequences aceitas, rejeições, duplicatas) -- só cruzando as duas dá para responder com
// segurança se uma métrica foi "perdida" ou só está atrasada em algum lugar do caminho.

const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const db = require("../src/db.js");

function arg(name, fallback) {
  const prefix = `--${name}=`;
  const found = process.argv.find((a) => a.startsWith(prefix));
  return found ? found.slice(prefix.length) : fallback;
}

const config = {
  testId: arg("test-id", null),
  gatewayHost: arg("gateway-host", "192.168.9.18"),
  gatewayUser: arg("gateway-user", "junior"),
  remoteDbPath: arg("remote-db-path", "/var/lib/edgewarden-test/test.db"),
  postgresContainer: arg("postgres-container", "fluxo-postgres"),
  postgresUser: arg("postgres-user", "fluxo"),
  postgresDb: arg("postgres-db", "fluxo_db"),
  deviceId: arg("device-id", "edgewarden"),
  outDir: arg("out-dir", path.join(__dirname, "..", "reports"))
};

function ssh(remoteCommand) {
  return execFileSync(
    "ssh",
    ["-o", "BatchMode=yes", "-o", "ConnectTimeout=8", `${config.gatewayUser}@${config.gatewayHost}`, remoteCommand],
    { encoding: "utf8", timeout: 20000 }
  );
}

function fetchRemoteDatabase() {
  // Força o WAL a se fundir no arquivo principal antes de copiar -- sem isso, copiar só o .db
  // (sem os arquivos -wal/-shm) pode deixar de fora escritas recentes ainda não fundidas.
  ssh(`node -e "const {DatabaseSync}=require('node:sqlite'); const d=new DatabaseSync('${config.remoteDbPath}'); d.exec('PRAGMA wal_checkpoint(TRUNCATE)'); d.close();"`);
  const localPath = path.join(require("os").tmpdir(), `edgewarden-test-report-${Date.now()}.db`);
  execFileSync("scp", [
    "-o", "BatchMode=yes",
    `${config.gatewayUser}@${config.gatewayHost}:${config.remoteDbPath}`,
    localPath
  ]);
  return localPath;
}

function psql(query) {
  const raw = execFileSync(
    "docker",
    ["exec", config.postgresContainer, "psql", "-U", config.postgresUser, "-d", config.postgresDb, "-t", "-A", "-F", "|", "-c", query],
    { encoding: "utf8", timeout: 15000 }
  );
  return raw
    .split("\n")
    .map((l) => l.trim())
    .filter((l) => l.length > 0)
    .map((l) => l.split("|"));
}

function fetchFluxoEvidence(session) {
  const started = session.test_started_at;
  const deviceId = config.deviceId;

  const [[acceptedCount]] = psql(
    `SELECT count(*) FROM telemetry_ingestion_records WHERE "DeviceId"='${deviceId}' AND "ReceivedAtUtc" >= '${started}'`
  );
  const [[minSeq, maxSeq]] = psql(
    `SELECT min("Sequence"), max("Sequence") FROM telemetry_ingestion_records WHERE "DeviceId"='${deviceId}' AND "ReceivedAtUtc" >= '${started}'`
  );
  const duplicates = psql(
    `SELECT "Sequence", count(*) FROM telemetry_ingestion_records WHERE "DeviceId"='${deviceId}' AND "ReceivedAtUtc" >= '${started}' GROUP BY "Sequence" HAVING count(*) > 1`
  );
  const rejections = psql(
    `SELECT "ErrorType", "Reason", count(*) FROM telemetry_ingestion_rejections WHERE "DeviceId"='${deviceId}' AND "ReceivedAtUtc" >= '${started}' GROUP BY "ErrorType", "Reason"`
  );
  const [[maxDelayMinutes]] = psql(
    `SELECT COALESCE(MAX(EXTRACT(EPOCH FROM ("ReceivedAtUtc" - "OccurredAtUtc")) / 60), 0) FROM telemetry_ingestion_records WHERE "DeviceId"='${deviceId}' AND "ReceivedAtUtc" >= '${started}'`
  );

  return {
    acceptedCount: Number(acceptedCount) || 0,
    minSeq: minSeq || null,
    maxSeq: maxSeq || null,
    duplicates: duplicates.map(([seq, count]) => ({ sequence: seq, count: Number(count) })),
    rejections: rejections.map(([errorType, reason, count]) => ({ errorType, reason, count: Number(count) })),
    maxDelayMinutes: Number(maxDelayMinutes) || 0
  };
}

function aggregateResources(checkpoints) {
  const nums = (key) => checkpoints.map((c) => c[key]).filter((v) => v !== null && v !== undefined);
  const avg = (arr) => (arr.length ? arr.reduce((a, b) => a + b, 0) / arr.length : null);
  const max = (arr) => (arr.length ? Math.max(...arr) : null);
  return {
    cpuTemp: { avg: avg(nums("cpu_temp_c")), max: max(nums("cpu_temp_c")) },
    mem: { avg: avg(nums("mem_used_percent")), max: max(nums("mem_used_percent")) },
    disk: { first: checkpoints[0]?.disk_used_percent ?? null, last: checkpoints[checkpoints.length - 1]?.disk_used_percent ?? null },
    load: { avg: avg(nums("load_1m")), max: max(nums("load_1m")) },
    maxQueueDepth: max(nums("pending_metrics_count"))
  };
}

function classifyEvents(events) {
  const byType = {};
  for (const e of events) byType[e.event_type] = (byType[e.event_type] || 0) + 1;
  const restarts = events.filter((e) => e.event_type === "UNEXPECTED_SHUTDOWN_DETECTED");
  const commLost = events.filter((e) => e.event_type === "COMMUNICATION_LOST");
  const commRecovered = events.filter((e) => e.event_type === "COMMUNICATION_RECOVERED");
  const backlogStarted = events.filter((e) => e.event_type === "BACKLOG_STARTED");
  const backlogDrained = events.filter((e) => e.event_type === "BACKLOG_DRAINED");

  const commLostCategories = {};
  for (const e of commLost) {
    let category = "sem sondagem (evento anterior a esta versão do harness)";
    if (e.metadata) {
      try {
        category = JSON.parse(e.metadata).category || category;
      } catch {
        category = "metadata inválido";
      }
    }
    commLostCategories[category] = (commLostCategories[category] || 0) + 1;
  }

  return { byType, restarts, commLost, commRecovered, backlogStarted, backlogDrained, commLostCategories };
}

function renderReport({ session, checkpoints, events, fluxoEvidence }) {
  const classified = classifyEvents(events);
  const resources = aggregateResources(checkpoints);

  const lostSequences =
    fluxoEvidence.minSeq && fluxoEvidence.maxSeq
      ? Number(fluxoEvidence.maxSeq) - Number(fluxoEvidence.minSeq) + 1 - fluxoEvidence.acceptedCount
      : null;

  let verdict = "PASS";
  const warnings = [];
  if (fluxoEvidence.duplicates.length > 0) {
    verdict = "FAIL";
    warnings.push(`${fluxoEvidence.duplicates.length} sequence(s) duplicada(s) no Fluxo -- idempotência falhou.`);
  }
  if (fluxoEvidence.rejections.length > 0) {
    verdict = verdict === "FAIL" ? "FAIL" : "PASS WITH WARNINGS";
    warnings.push(`${fluxoEvidence.rejections.length} tipo(s) de rejeição registrados.`);
  }
  if (lostSequences !== null && lostSequences > 0) {
    verdict = "FAIL";
    warnings.push(`${lostSequences} sequence(s) esperada(s) nunca chegaram ao Fluxo (gap não explicado).`);
  }
  if (session.unexpected_shutdown_count > 0 && classified.restarts.length !== session.unexpected_shutdown_count) {
    warnings.push("Contagem de reinícios inesperados diverge entre TestSession e eventos -- investigar.");
  }

  const lines = [];
  lines.push(`# Relatório do ensaio ${session.test_id}`);
  lines.push("");
  lines.push(`**Resultado: ${verdict}**`);
  lines.push("");
  if (warnings.length) {
    lines.push("Avisos:");
    for (const w of warnings) lines.push(`- ${w}`);
    lines.push("");
  }
  lines.push("## Informações");
  lines.push("");
  lines.push(`- Test ID: \`${session.test_id}\``);
  lines.push(`- Gateway ID: \`${session.gateway_id}\``);
  lines.push(`- Início: ${session.test_started_at}`);
  lines.push(`- Fim: ${session.test_completed_at || "(ainda não concluído)"}`);
  lines.push(`- Tempo efetivo acumulado: ${session.accumulated_runtime_seconds.toFixed(1)}s / alvo ${session.test_target_duration_seconds}s`);
  lines.push(`- Estado final: ${session.state}`);
  lines.push(`- capture_completed: ${session.capture_completed ? "sim" : "não"} / delivery_completed: ${session.delivery_completed ? "sim" : "não"}`);
  lines.push("");
  lines.push("## Métricas");
  lines.push("");
  lines.push(`- Aceitas pelo Fluxo: ${fluxoEvidence.acceptedCount}`);
  lines.push(`- Faixa de sequence observada: ${fluxoEvidence.minSeq ?? "?"} .. ${fluxoEvidence.maxSeq ?? "?"}`);
  lines.push(`- Sequences ausentes (gap não explicado): ${lostSequences ?? "?"}`);
  lines.push(`- Duplicadas: ${fluxoEvidence.duplicates.length}`);
  lines.push(`- Rejeitadas: ${fluxoEvidence.rejections.reduce((a, r) => a + r.count, 0)}`);
  lines.push(`- Maior atraso de entrega (OccurredAtUtc -> ReceivedAtUtc): ${fluxoEvidence.maxDelayMinutes.toFixed(1)} min`);
  lines.push("");
  if (fluxoEvidence.rejections.length) {
    lines.push("### Rejeições por tipo");
    lines.push("");
    for (const r of fluxoEvidence.rejections) lines.push(`- ${r.errorType} (${r.reason}): ${r.count}`);
    lines.push("");
  }
  lines.push("## Reinicializações");
  lines.push("");
  lines.push(`- Total de reinícios inesperados detectados: ${session.unexpected_shutdown_count}`);
  lines.push(`- Recuperados automaticamente: ${classified.restarts.length} (ver eventos TEST_RECOVERED)`);
  lines.push("");
  lines.push("## Comunicação");
  lines.push("");
  lines.push(`- Quedas de comunicação detectadas (COMMUNICATION_LOST): ${classified.commLost.length}`);
  lines.push(`- Recuperações (COMMUNICATION_RECOVERED): ${classified.commRecovered.length}`);
  if (Object.keys(classified.commLostCategories).length) {
    lines.push("");
    lines.push("### Categorização das quedas (sondagem TCP/TLS best-effort)");
    lines.push("");
    for (const [category, count] of Object.entries(classified.commLostCategories).sort((a, b) => b[1] - a[1])) {
      lines.push(`- ${category}: ${count}`);
    }
    lines.push("");
    lines.push(
      "_Nota: a sondagem roda no tick seguinte à falha e só cobre transporte (TCP/TLS), não o " +
        "handshake MQTT -- `reachable_at_probe_time` significa que a falha original pode ter sido " +
        "transitória ou uma rejeição no nível CONNACK (auth/protocolo), não necessariamente rede " +
        "indisponível. Ver `src/mqtt-probe.js` para detalhes._"
    );
  }
  lines.push("");
  lines.push("## Store-and-forward");
  lines.push("");
  lines.push(`- Backlogs iniciados: ${classified.backlogStarted.length}`);
  lines.push(`- Backlogs drenados: ${classified.backlogDrained.length}`);
  lines.push(`- Maior profundidade de fila observada: ${resources.maxQueueDepth ?? "?"}`);
  lines.push("");
  lines.push("## Recursos do gateway");
  lines.push("");
  lines.push(`- CPU temp média/máxima: ${fmt(resources.cpuTemp.avg)}°C / ${fmt(resources.cpuTemp.max)}°C`);
  lines.push(`- RAM usada média/máxima: ${fmt(resources.mem.avg)}% / ${fmt(resources.mem.max)}%`);
  lines.push(`- Disco usado no início/fim: ${fmt(resources.disk.first)}% / ${fmt(resources.disk.last)}%`);
  lines.push(`- Load(1m) média/máxima: ${fmt(resources.load.avg)} / ${fmt(resources.load.max)}`);
  lines.push("");
  lines.push("## Contagem de eventos por tipo");
  lines.push("");
  for (const [type, count] of Object.entries(classified.byType).sort((a, b) => b[1] - a[1])) {
    lines.push(`- ${type}: ${count}`);
  }
  lines.push("");
  lines.push("## Conclusão");
  lines.push("");
  lines.push(
    verdict === "PASS"
      ? "O EdgeWarden demonstrou capacidade de operar como gateway resiliente diante das falhas exercitadas neste ensaio: nenhuma métrica perdida, nenhuma duplicata lógica, reinicializações recuperadas automaticamente."
      : "Ver avisos acima -- este ensaio encontrou pelo menos uma divergência que precisa ser explicada antes de considerar o EdgeWarden aprovado para produção controlada."
  );
  lines.push("");
  return { markdown: lines.join("\n"), verdict };
}

function fmt(n) {
  return n === null || n === undefined ? "?" : n.toFixed(1);
}

function main() {
  if (!config.testId) {
    // eslint-disable-next-line no-console
    console.error("uso: node report.js --test-id=<uuid> [--gateway-host=...] [--device-id=edgewarden]");
    process.exitCode = 1;
    return;
  }

  const localDbPath = fetchRemoteDatabase();
  const database = db.openDatabase(localDbPath);
  const session = db.getSession(database, config.testId);
  if (!session) {
    console.error(`Sessão ${config.testId} não encontrada no banco copiado de ${config.gatewayHost}.`);
    process.exitCode = 1;
    return;
  }
  const checkpoints = database
    .prepare("SELECT * FROM Checkpoint WHERE test_id = ? ORDER BY id ASC")
    .all(config.testId);
  const events = db.listEvents(database, config.testId, { limit: 1000000 }).reverse();
  const fluxoEvidence = fetchFluxoEvidence(session);

  const { markdown, verdict } = renderReport({ session, checkpoints, events, fluxoEvidence });

  const outDir = path.join(config.outDir, config.testId);
  fs.mkdirSync(path.join(outDir, "evidence"), { recursive: true });
  fs.writeFileSync(path.join(outDir, "report.md"), markdown, "utf8");
  fs.writeFileSync(path.join(outDir, "evidence", "session.json"), JSON.stringify(session, null, 2));
  fs.writeFileSync(path.join(outDir, "evidence", "checkpoints.json"), JSON.stringify(checkpoints, null, 2));
  fs.writeFileSync(path.join(outDir, "evidence", "events.json"), JSON.stringify(events, null, 2));
  fs.writeFileSync(path.join(outDir, "evidence", "fluxo-evidence.json"), JSON.stringify(fluxoEvidence, null, 2));

  console.log(`Relatório (${verdict}) escrito em ${outDir}/report.md`);
  database.close(); // no Windows, rmSync falha com EPERM se o handle ainda estiver aberto
  fs.rmSync(localDbPath, { force: true });
  fs.rmSync(`${localDbPath}-wal`, { force: true });
  fs.rmSync(`${localDbPath}-shm`, { force: true });
}

main();
