"use strict";

const { DatabaseSync } = require("node:sqlite");
const fs = require("fs");
const path = require("path");

const SCHEMA = `
CREATE TABLE IF NOT EXISTS TestSession (
  test_id TEXT PRIMARY KEY,
  gateway_id TEXT NOT NULL,
  state TEXT NOT NULL,
  test_started_at TEXT NOT NULL,
  test_target_duration_seconds INTEGER NOT NULL,
  accumulated_runtime_seconds REAL NOT NULL DEFAULT 0,
  last_checkpoint_at TEXT,
  last_shutdown_reason TEXT,
  unexpected_shutdown_count INTEGER NOT NULL DEFAULT 0,
  capture_completed INTEGER NOT NULL DEFAULT 0,
  delivery_completed INTEGER NOT NULL DEFAULT 0,
  test_completed_at TEXT,
  last_metric_sequence INTEGER,
  first_metric_sequence INTEGER,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Checkpoint (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  test_id TEXT NOT NULL,
  checkpoint_at TEXT NOT NULL,
  state TEXT NOT NULL,
  accumulated_runtime_seconds REAL NOT NULL,
  remaining_runtime_seconds REAL NOT NULL,
  last_metric_sequence INTEGER,
  pending_metrics_count INTEGER,
  queue_bytes INTEGER,
  mqtt_connected INTEGER,
  cpu_temp_c REAL,
  mem_used_percent REAL,
  disk_used_percent REAL,
  disk_free_bytes INTEGER,
  load_1m REAL,
  net_rx_bytes INTEGER,
  net_tx_bytes INTEGER,
  net_operstate TEXT,
  FOREIGN KEY (test_id) REFERENCES TestSession(test_id)
);
CREATE INDEX IF NOT EXISTS ix_checkpoint_test_id ON Checkpoint(test_id, checkpoint_at);

CREATE TABLE IF NOT EXISTS Event (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  test_id TEXT NOT NULL,
  timestamp TEXT NOT NULL,
  event_type TEXT NOT NULL,
  severity TEXT NOT NULL,
  gateway_id TEXT NOT NULL,
  description TEXT NOT NULL,
  metadata TEXT,
  FOREIGN KEY (test_id) REFERENCES TestSession(test_id)
);
CREATE INDEX IF NOT EXISTS ix_event_test_id ON Event(test_id, timestamp);
`;

function openDatabase(dbPath) {
  fs.mkdirSync(path.dirname(dbPath), { recursive: true, mode: 0o700 });
  const db = new DatabaseSync(dbPath);
  // synchronous=FULL: fsync a cada commit. Custa mais IO, mas o objetivo do ensaio é
  // sobreviver a queda de energia -- um checkpoint que "commitou" mas não foi fsync'ado
  // pode sumir exatamente no cenário que estamos tentando provar que não perde dado.
  db.exec("PRAGMA journal_mode = WAL");
  db.exec("PRAGMA synchronous = FULL");
  db.exec("PRAGMA foreign_keys = ON");
  db.exec(SCHEMA);
  return db;
}

function nowIso() {
  return new Date().toISOString();
}

function getLatestSession(db, gatewayId) {
  const row = db
    .prepare(
      "SELECT * FROM TestSession WHERE gateway_id = ? ORDER BY created_at DESC LIMIT 1"
    )
    .get(gatewayId);
  return row || null;
}

function getSession(db, testId) {
  return db.prepare("SELECT * FROM TestSession WHERE test_id = ?").get(testId) || null;
}

function createSession(db, { testId, gatewayId, targetDurationSeconds }) {
  const ts = nowIso();
  db.prepare(
    `INSERT INTO TestSession
      (test_id, gateway_id, state, test_started_at, test_target_duration_seconds,
       accumulated_runtime_seconds, last_checkpoint_at, created_at, updated_at)
     VALUES (?, ?, 'CREATED', ?, ?, 0, ?, ?, ?)`
  ).run(testId, gatewayId, ts, targetDurationSeconds, ts, ts, ts);
  return getSession(db, testId);
}

function updateSession(db, testId, fields) {
  const allowed = [
    "state",
    "accumulated_runtime_seconds",
    "last_checkpoint_at",
    "last_shutdown_reason",
    "unexpected_shutdown_count",
    "capture_completed",
    "delivery_completed",
    "test_completed_at",
    "last_metric_sequence",
    "first_metric_sequence"
  ];
  const keys = Object.keys(fields).filter((k) => allowed.includes(k));
  if (keys.length === 0) return getSession(db, testId);
  const assignments = keys.map((k) => `${k} = ?`).join(", ");
  const values = keys.map((k) => fields[k]);
  db.prepare(`UPDATE TestSession SET ${assignments}, updated_at = ? WHERE test_id = ?`).run(
    ...values,
    nowIso(),
    testId
  );
  return getSession(db, testId);
}

function insertCheckpoint(db, checkpoint) {
  db.prepare(
    `INSERT INTO Checkpoint
      (test_id, checkpoint_at, state, accumulated_runtime_seconds, remaining_runtime_seconds,
       last_metric_sequence, pending_metrics_count, queue_bytes, mqtt_connected, cpu_temp_c,
       mem_used_percent, disk_used_percent, disk_free_bytes, load_1m, net_rx_bytes, net_tx_bytes,
       net_operstate)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  ).run(
    checkpoint.testId,
    nowIso(),
    checkpoint.state,
    checkpoint.accumulatedRuntimeSeconds,
    checkpoint.remainingRuntimeSeconds,
    checkpoint.lastMetricSequence ?? null,
    checkpoint.pendingMetricsCount ?? null,
    checkpoint.queueBytes ?? null,
    checkpoint.mqttConnected === undefined || checkpoint.mqttConnected === null
      ? null
      : checkpoint.mqttConnected
      ? 1
      : 0,
    checkpoint.cpuTempC ?? null,
    checkpoint.memUsedPercent ?? null,
    checkpoint.diskUsedPercent ?? null,
    checkpoint.diskFreeBytes ?? null,
    checkpoint.load1m ?? null,
    checkpoint.netRxBytes ?? null,
    checkpoint.netTxBytes ?? null,
    checkpoint.netOperstate ?? null
  );
}

function getLatestCheckpoint(db, testId) {
  return (
    db
      .prepare(
        "SELECT * FROM Checkpoint WHERE test_id = ? ORDER BY id DESC LIMIT 1"
      )
      .get(testId) || null
  );
}

const VALID_SEVERITIES = new Set(["INFO", "WARN", "ERROR", "CRITICAL"]);

function insertEvent(db, { testId, gatewayId, eventType, severity, description, metadata }) {
  const sev = VALID_SEVERITIES.has(severity) ? severity : "INFO";
  db.prepare(
    `INSERT INTO Event (test_id, timestamp, event_type, severity, gateway_id, description, metadata)
     VALUES (?, ?, ?, ?, ?, ?, ?)`
  ).run(
    testId,
    nowIso(),
    eventType,
    sev,
    gatewayId,
    description,
    metadata ? JSON.stringify(metadata) : null
  );
}

function listEvents(db, testId, { limit = 200 } = {}) {
  return db
    .prepare("SELECT * FROM Event WHERE test_id = ? ORDER BY id DESC LIMIT ?")
    .all(testId, limit);
}

module.exports = {
  openDatabase,
  getLatestSession,
  getSession,
  createSession,
  updateSession,
  insertCheckpoint,
  getLatestCheckpoint,
  insertEvent,
  listEvents,
  nowIso
};
