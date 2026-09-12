#!/usr/bin/env node
"use strict";

const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const sourceDir = path.resolve(__dirname, "..");
const userDir = process.env.NODE_RED_USER_DIR || "/home/junior/.node-red";
const flowsPath = path.join(userDir, "flows.json");
const credentialsPath = path.join(userDir, "flows_cred.json");
const runtimeConfigPath = path.join(userDir, ".config.runtime.json");
const tabId = "f5a0000000000001";
const brokerId = "f5a00000000000b1";
const tlsId = "f5a00000000000c1";

function atomicWrite(target, content, mode) {
  const temporary = `${target}.tmp-${process.pid}`;
  const fd = fs.openSync(temporary, "wx", mode);
  try { fs.writeFileSync(fd, content, "utf8"); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
  fs.renameSync(temporary, target);
  fs.chmodSync(target, mode);
}

function keyFor(secret) {
  return crypto.createHash("sha256").update(secret).digest();
}

function decryptCredentials(secret, encrypted) {
  if (!encrypted || !encrypted.$) return {};
  const value = encrypted.$;
  const decipher = crypto.createDecipheriv("aes-256-ctr", keyFor(secret), Buffer.from(value.slice(0, 32), "hex"));
  return JSON.parse(decipher.update(value.slice(32), "base64", "utf8") + decipher.final("utf8"));
}

function encryptCredentials(secret, credentials) {
  const iv = crypto.randomBytes(16);
  const cipher = crypto.createCipheriv("aes-256-ctr", keyFor(secret), iv);
  return { $: iv.toString("hex") + cipher.update(JSON.stringify(credentials), "utf8", "base64") + cipher.final("base64") };
}

function main() {
  for (const name of ["FLUXO_MQTT_USERNAME", "FLUXO_MQTT_PASSWORD"]) {
    if (!process.env[name]) throw new Error(`${name} is required`);
  }

  const incoming = JSON.parse(fs.readFileSync(path.join(sourceDir, "flow.json"), "utf8"));
  const current = fs.existsSync(flowsPath) ? JSON.parse(fs.readFileSync(flowsPath, "utf8")) : [];
  if (!Array.isArray(current) || !Array.isArray(incoming)) throw new Error("flow file must contain a JSON array");
  const brokerNode = incoming.find((node) => node.id === brokerId);
  if (!brokerNode) throw new Error("MQTT broker config node is missing");
  brokerNode.clientid = `fluxo-${process.env.FLUXO_DEVICE_ID}`;
  const kept = current.filter((node) => node.id !== tabId && node.z !== tabId && node.id !== brokerId && node.id !== tlsId);

  const runtimeConfig = fs.existsSync(runtimeConfigPath)
    ? JSON.parse(fs.readFileSync(runtimeConfigPath, "utf8"))
    : {};
  let secret = runtimeConfig._credentialSecret;
  if (!secret) {
    secret = crypto.randomBytes(32).toString("hex");
    runtimeConfig._credentialSecret = secret;
    atomicWrite(runtimeConfigPath, `${JSON.stringify(runtimeConfig, null, 4)}\n`, 0o600);
  }

  let credentials = {};
  if (fs.existsSync(credentialsPath)) {
    credentials = decryptCredentials(secret, JSON.parse(fs.readFileSync(credentialsPath, "utf8")));
  }
  credentials[brokerId] = {
    user: process.env.FLUXO_MQTT_USERNAME,
    password: process.env.FLUXO_MQTT_PASSWORD
  };

  atomicWrite(flowsPath, `${JSON.stringify([...kept, ...incoming], null, 4)}\n`, 0o600);
  atomicWrite(credentialsPath, `${JSON.stringify(encryptCredentials(secret, credentials))}\n`, 0o600);
  process.stdout.write("Gateway flow and encrypted credentials installed offline.\n");
}

try { main(); } catch (error) { process.stderr.write(`${error.message}\n`); process.exitCode = 1; }
