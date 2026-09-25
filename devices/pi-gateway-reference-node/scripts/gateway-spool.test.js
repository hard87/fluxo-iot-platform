"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const { validateEnvironmentReading } = require("./gateway-spool");

test("accepts aligned 14-bit HDC1080 readings", () => {
  assert.equal(validateEnvironmentReading({
    temperature_c: 16.25,
    humidity_percent: 81.5,
    t_raw: 22340,
    h_raw: 53412
  }), null);
});

test("rejects plausible converted values whose unused resolution bits are set", () => {
  assert.match(validateEnvironmentReading({
    temperature_c: 14.32,
    humidity_percent: 81.49,
    t_raw: 21569,
    h_raw: 53405
  }), /invalid 14-bit alignment/);

  assert.match(validateEnvironmentReading({
    temperature_c: 16.3,
    humidity_percent: 34.12,
    t_raw: 22357,
    h_raw: 22361
  }), /invalid 14-bit alignment/);
});

test("rejects known raw glitch signatures and out-of-range values", () => {
  assert.match(validateEnvironmentReading({
    temperature_c: -40,
    humidity_percent: 81,
    t_raw: 0,
    h_raw: 53084
  }), /raw glitch signature/);

  assert.match(validateEnvironmentReading({
    temperature_c: 93.17,
    humidity_percent: 81,
    t_raw: 52896,
    h_raw: 53084
  }), /out of plausible range/);
});
