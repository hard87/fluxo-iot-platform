#!/usr/bin/env node
"use strict";

const fs = require("fs");
const mqtt = require("/usr/lib/node_modules/node-red/node_modules/mqtt");

const host = process.env.FLUXO_MQTT_HOST;
const port = Number(process.env.FLUXO_MQTT_PORT);
const username = process.env.FLUXO_MQTT_USERNAME;
const password = process.env.FLUXO_MQTT_PASSWORD;
const ca = fs.readFileSync(process.env.FLUXO_MQTT_CA_PATH);
const topic = `fluxo/tenants/${process.env.FLUXO_TENANT_ID}/workspaces/${process.env.FLUXO_WORKSPACE_ID}/devices/${process.env.FLUXO_DEVICE_ID}/telemetry`;

function client(options) {
  return mqtt.connect({ protocol: "mqtts", host, port, ca, username, password, rejectUnauthorized: true,
    reconnectPeriod: 0, connectTimeout: 5000, clientId: `fluxo-negative-test-${process.pid}`, ...options });
}

function expectConnectFailure(name, options) {
  return new Promise((resolve, reject) => {
    const c = client(options);
    const timer = setTimeout(() => { c.end(true); reject(new Error(`${name} timed out`)); }, 7000);
    c.once("connect", () => { clearTimeout(timer); c.end(true); reject(new Error(`${name} unexpectedly connected`)); });
    c.once("error", () => { clearTimeout(timer); c.end(true); process.stdout.write(`PASS: ${name} was rejected\n`); resolve(); });
  });
}

function expectAclFailure() {
  return new Promise((resolve, reject) => {
    const c = client({ protocolVersion: 5 });
    let settled = false;
    const finish = (ok, message) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      c.end(true);
      if (ok) { process.stdout.write(`PASS: ${message}\n`); resolve(); }
      else reject(new Error(message));
    };
    const timer = setTimeout(() => finish(true, "ACL for another topic withheld PUBACK"), 5000);
    c.once("error", () => finish(true, "ACL for another topic was rejected"));
    c.once("connect", () => c.publish(`${topic}-other`, "{}", { qos: 1 }, (error, packet) => {
      const reasonCode = packet && packet.reasonCode;
      if (error || (Number.isInteger(reasonCode) && reasonCode >= 128)) finish(true, `ACL for another topic returned reason ${reasonCode || 'error'}`);
      else finish(false, "ACL for another topic unexpectedly received a successful PUBACK");
    }));
  });
}

async function main() {
  await expectConnectFailure("invalid CA", { ca: Buffer.from("not-a-certificate") });
  await expectConnectFailure("invalid credential", { password: `${password}invalid` });
  await expectAclFailure();
}

main().catch((error) => { process.stderr.write(`FAIL: ${error.message}\n`); process.exitCode = 1; });
