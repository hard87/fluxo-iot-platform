#!/usr/bin/env node
"use strict";

const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");

const root = process.env.FLUXO_GATEWAY_STATE_DIR || "/home/junior/.node-red/fluxo-gateway/state";
const spool = path.join(root, "spool");
const sequenceFile = path.join(root, "sequence.json");
const sequenceBackupFile = path.join(root, "sequence.backup.json");
const countersFile = path.join(root, "counters.json");
const lockFile = path.join(root, ".lock");
const maxMessages = positiveInt("FLUXO_QUEUE_MAX_MESSAGES", 10000);
const maxBytes = positiveInt("FLUXO_QUEUE_MAX_BYTES", 50 * 1024 * 1024);
const maxAgeSeconds = positiveInt("FLUXO_QUEUE_MAX_AGE_SECONDS", 7 * 24 * 60 * 60);

function positiveInt(name, fallback) {
  const value = Number.parseInt(process.env[name] || "", 10);
  return Number.isSafeInteger(value) && value > 0 ? value : fallback;
}

function required(name) {
  const value = process.env[name];
  if (!value || !value.trim()) throw new Error(`${name} is required`);
  return value.trim();
}

function ensureDirectories() {
  fs.mkdirSync(spool, { recursive: true, mode: 0o700 });
  fs.chmodSync(root, 0o700);
  fs.chmodSync(spool, 0o700);
}

function fsyncDirectory(directory) {
  const fd = fs.openSync(directory, "r");
  try {
    fs.fsyncSync(fd);
  } catch (error) {
    // Windows does not permit fsync on directory handles. Linux (the target) does.
    if (process.platform !== "win32" || !["EINVAL", "EPERM"].includes(error.code)) throw error;
  } finally { fs.closeSync(fd); }
}

function atomicWriteJson(target, value) {
  const temporary = `${target}.tmp-${process.pid}-${Date.now()}`;
  const fd = fs.openSync(temporary, "wx", 0o600);
  try {
    fs.writeFileSync(fd, `${JSON.stringify(value)}\n`, "utf8");
    fs.fsyncSync(fd);
  } finally {
    fs.closeSync(fd);
  }
  fs.renameSync(temporary, target);
  fsyncDirectory(path.dirname(target));
}

function withLock(action) {
  ensureDirectories();
  let fd;
  try {
    fd = fs.openSync(lockFile, "wx", 0o600);
  } catch (error) {
    if (error.code !== "EEXIST") throw error;
    const ageMs = Date.now() - fs.statSync(lockFile).mtimeMs;
    if (ageMs <= 30000) throw new Error("gateway spool is busy");
    fs.unlinkSync(lockFile);
    fd = fs.openSync(lockFile, "wx", 0o600);
    process.stderr.write("Recovered stale gateway spool lock.\n");
  }
  try {
    fs.writeFileSync(fd, `${process.pid}\n`, "utf8");
    fs.fsyncSync(fd);
    return action();
  } finally {
    fs.closeSync(fd);
    try { fs.unlinkSync(lockFile); } catch (error) { if (error.code !== "ENOENT") throw error; }
  }
}

function readJson(target) {
  return JSON.parse(fs.readFileSync(target, "utf8"));
}

function spoolFiles() {
  return fs.readdirSync(spool)
    .filter((name) => /^\d{20}\.json$/.test(name))
    .sort();
}

function sequenceFromName(name) {
  return Number.parseInt(name.slice(0, 20), 10);
}

function loadSequence() {
  const files = spoolFiles();
  const queuedMaximum = files.length ? sequenceFromName(files[files.length - 1]) : 0;
  const candidates = [queuedMaximum];
  let existed = false;
  let corrupt = false;
  for (const target of [sequenceFile, sequenceBackupFile]) {
    if (!fs.existsSync(target)) continue;
    existed = true;
    try {
      const value = readJson(target).sequence;
      if (!Number.isSafeInteger(value) || value < 0) throw new Error("invalid sequence");
      candidates.push(value);
    } catch (error) {
      corrupt = true;
      process.stderr.write(`Cannot read ${target}: ${error.message}\n`);
    }
  }
  if (existed && corrupt && candidates.length === 1 && queuedMaximum === 0) {
    throw new Error("sequence state is corrupt and no safe high-water mark is available; recover manually");
  }
  return Math.max(...candidates);
}

function persistSequence(sequence) {
  const state = { sequence, updatedAtUtc: new Date().toISOString() };
  atomicWriteJson(sequenceBackupFile, state);
  atomicWriteJson(sequenceFile, state);
}

function loadCounters() {
  if (!fs.existsSync(countersFile)) return { replayedMessages: 0, droppedMessages: 0 };
  try {
    const counters = readJson(countersFile);
    return {
      replayedMessages: Number.isSafeInteger(counters.replayedMessages) ? counters.replayedMessages : 0,
      droppedMessages: Number.isSafeInteger(counters.droppedMessages) ? counters.droppedMessages : 0
    };
  } catch (error) {
    process.stderr.write(`Cannot read counters: ${error.message}; using zero.\n`);
    return { replayedMessages: 0, droppedMessages: 0 };
  }
}

function queueStats(files = spoolFiles()) {
  let bytes = 0;
  for (const name of files) bytes += fs.statSync(path.join(spool, name)).size;
  return { depth: files.length, bytes };
}

function pruneExpired() {
  const cutoff = Date.now() - maxAgeSeconds * 1000;
  const counters = loadCounters();
  let changed = false;
  for (const name of spoolFiles()) {
    const target = path.join(spool, name);
    let record;
    try { record = readJson(target); } catch (error) {
      process.stderr.write(`Unreadable spool message retained for manual recovery: ${name}: ${error.message}\n`);
      continue;
    }
    if (Date.parse(record.enqueuedAtUtc) >= cutoff) continue;
    fs.unlinkSync(target);
    counters.droppedMessages += 1;
    changed = true;
    process.stderr.write(`Dropped expired spool message ${name}.\n`);
  }
  if (changed) atomicWriteJson(countersFile, counters);
}

const HDC1080_I2C_BUS = positiveInt("FLUXO_HDC1080_I2C_BUS", 1);
const HDC1080_I2C_ADDRESS = 0x40;
const HDC1080_READ_SCRIPT = `
import json, smbus2, sys, time

bus = smbus2.SMBus(${HDC1080_I2C_BUS})
addr = ${HDC1080_I2C_ADDRESS}

def read_reg(pointer, delay=0.025):
    bus.i2c_rdwr(smbus2.i2c_msg.write(addr, [pointer]))
    time.sleep(delay)
    read = smbus2.i2c_msg.read(addr, 2)
    bus.i2c_rdwr(read)
    data = list(read)
    return (data[0] << 8) | data[1]

try:
    t_raw = read_reg(0x00)
    h_raw = read_reg(0x01)
    temp_c = (t_raw / 65536.0) * 165.0 - 40.0
    hum_pct = (h_raw / 65536.0) * 100.0
    print(json.dumps({"temperature_c": round(temp_c, 2), "humidity_percent": round(hum_pct, 2)}))
finally:
    bus.close()
`;

function readEnvironmentSensor() {
  try {
    const output = execFileSync("python3", ["-c", HDC1080_READ_SCRIPT], { encoding: "utf8", timeout: 2000 });
    const reading = JSON.parse(output.trim());
    if (!Number.isFinite(reading.temperature_c) || !Number.isFinite(reading.humidity_percent)) return {};
    return {
      "environment.temperature_c": reading.temperature_c,
      "environment.humidity_percent": reading.humidity_percent
    };
  } catch (error) {
    process.stderr.write(`HDC1080 read failed: ${error.message}\n`);
    return {};
  }
}

function readTimeSyncStatus() {
  try {
    const value = execFileSync("timedatectl", ["show", "-p", "NTPSynchronized", "--value"], {
      encoding: "utf8",
      timeout: 1000
    }).trim();
    return value === "yes";
  } catch (error) {
    process.stderr.write(`timedatectl check failed: ${error.message}\n`);
    return null;
  }
}

const NET_IFACE = process.env.FLUXO_NET_IFACE || "wlan0";

function readNetworkStats() {
  try {
    const base = `/sys/class/net/${NET_IFACE}`;
    const operstate = fs.readFileSync(`${base}/operstate`, "utf8").trim();
    const rxBytes = Number(fs.readFileSync(`${base}/statistics/rx_bytes`, "utf8"));
    const txBytes = Number(fs.readFileSync(`${base}/statistics/tx_bytes`, "utf8"));
    if (!Number.isFinite(rxBytes) || !Number.isFinite(txBytes)) return {};
    return {
      "gateway.network_interface_up": operstate === "up",
      "gateway.network_rx_bytes": rxBytes,
      "gateway.network_tx_bytes": txBytes
    };
  } catch (error) {
    process.stderr.write(`network stats read failed (${NET_IFACE}): ${error.message}\n`);
    return {};
  }
}

function diagnostics(queueDepth, mqttConnected, counters) {
  let cpuTemperatureC = null;
  try { cpuTemperatureC = Number(fs.readFileSync("/sys/class/thermal/thermal_zone0/temp", "utf8")) / 1000; } catch { }
  let diskUsedPercent = null;
  try {
    const stat = fs.statfsSync(root);
    diskUsedPercent = ((stat.blocks - stat.bavail) / stat.blocks) * 100;
  } catch { }
  const totalMemory = os.totalmem();
  const timeSynchronized = readTimeSyncStatus();
  return {
    "gateway.uptime_sec": Math.floor(os.uptime()),
    ...(Number.isFinite(cpuTemperatureC) ? { "gateway.cpu_temperature_c": Number(cpuTemperatureC.toFixed(1)) } : {}),
    "gateway.memory_used_percent": Number((((totalMemory - os.freemem()) / totalMemory) * 100).toFixed(1)),
    ...(Number.isFinite(diskUsedPercent) ? { "gateway.disk_used_percent": Number(diskUsedPercent.toFixed(1)) } : {}),
    "gateway.load_1m": Number(os.loadavg()[0].toFixed(2)),
    "gateway.queue_depth": queueDepth,
    "gateway.mqtt_connected": mqttConnected,
    "gateway.replayed_messages": counters.replayedMessages,
    "gateway.dropped_messages": counters.droppedMessages,
    ...(timeSynchronized !== null ? { "gateway.time_synchronized": timeSynchronized } : {}),
    ...readNetworkStats(),
    ...readEnvironmentSensor()
  };
}

function enqueue(mqttConnected) {
  pruneExpired();
  const before = queueStats();
  if (before.depth >= maxMessages || before.bytes >= maxBytes) {
    const counters = loadCounters();
    counters.droppedMessages += 1;
    atomicWriteJson(countersFile, counters);
    throw new Error(`queue limit reached (messages=${before.depth}/${maxMessages}, bytes=${before.bytes}/${maxBytes}); newest message discarded`);
  }
  const sequence = loadSequence() + 1;
  persistSequence(sequence);
  const counters = loadCounters();
  const topic = `fluxo/tenants/${required("FLUXO_TENANT_ID")}/workspaces/${required("FLUXO_WORKSPACE_ID")}/devices/${required("FLUXO_DEVICE_ID")}/telemetry`;
  const payload = {
    schemaVersion: 2,
    sequence,
    occurredAtUtc: new Date().toISOString(),
    metrics: diagnostics(before.depth + 1, mqttConnected, counters)
  };
  const name = `${String(sequence).padStart(20, "0")}.json`;
  const target = path.join(spool, name);
  atomicWriteJson(target, {
    id: name.slice(0, -5),
    sequence,
    topic,
    payload,
    enqueuedAtUtc: payload.occurredAtUtc,
    queuedWhileOffline: !mqttConnected
  });
  return { sequence, depth: before.depth + 1 };
}

function next() {
  pruneExpired();
  const files = spoolFiles();
  if (!files.length) return { empty: true, queueDepth: 0 };
  const record = readJson(path.join(spool, files[0]));
  return { ...record, queueDepth: files.length };
}

function ack(id) {
  if (!/^\d{20}$/.test(id || "")) throw new Error("invalid spool id");
  const target = path.join(spool, `${id}.json`);
  if (!fs.existsSync(target)) return { removed: false, depth: spoolFiles().length };
  const record = readJson(target);
  fs.unlinkSync(target);
  fsyncDirectory(spool);
  if (record.queuedWhileOffline) {
    const counters = loadCounters();
    counters.replayedMessages += 1;
    atomicWriteJson(countersFile, counters);
  }
  return { removed: true, depth: spoolFiles().length, sequence: record.sequence };
}

function status() {
  ensureDirectories();
  const stats = queueStats();
  return { ...stats, sequence: loadSequence(), ...loadCounters(), limits: { maxMessages, maxBytes, maxAgeSeconds } };
}

function main() {
  const command = process.argv[2];
  const result = withLock(() => {
    if (command === "enqueue") return enqueue(process.argv[3] === "connected");
    if (command === "next") return next();
    if (command === "ack") return ack(process.argv[3]);
    if (command === "status") return status();
    throw new Error("usage: gateway-spool.js enqueue <connected|disconnected> | next | ack <id> | status");
  });
  process.stdout.write(`${JSON.stringify(result)}\n`);
}

try { main(); } catch (error) {
  process.stderr.write(`gateway-spool: ${error.message}\n`);
  process.exitCode = 1;
}
