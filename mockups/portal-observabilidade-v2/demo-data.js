/*
 * Fluxo Portal — POC "Observabilidade do dispositivo"
 * demo-data.js — GERADOR DE DADOS SIMULADOS. Nada aqui é telemetria real.
 *
 * Todos os valores são sintéticos e determinísticos (seed fixa) para que a
 * demonstração seja reproduzível. As unidades imitam as métricas nativas que o
 * Gateway Pi de referência realmente emite (gateway-spool.js -> diagnostics()).
 */
(function (global) {
  "use strict";

  // ---- PRNG determinístico (mulberry32) --------------------------------------
  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a |= 0; a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const NOW = Date.now();
  const STEP_MS = 120 * 1000;          // 1 amostra a cada 2 min
  const SPAN_MS = 3 * 24 * 3600 * 1000; // 3 dias de histórico "cheio"
  const COUNT = Math.floor(SPAN_MS / STEP_MS);

  // ---- Catálogo de métricas nativas ----------------------------------------
  // exists: já emitido hoje pelo gateway | derived: calculado no portal
  // backend: depende de mudança no backend | agent: depende do agente/dispositivo
  const METRICS = [
    { key: "gateway.cpu_temperature_c", name: "Temperatura da CPU", unit: "°C", group: "recursos", type: "numeric", origin: "exists", base: 42, expected: [null, 70], decimals: 1 },
    { key: "gateway.load_1m", name: "Carga (1 min)", unit: "", group: "recursos", type: "numeric", origin: "exists", base: 0.18, expected: [null, 2], decimals: 2 },
    { key: "gateway.memory_used_percent", name: "Memória utilizada", unit: "%", group: "recursos", type: "numeric", origin: "exists", base: 33, expected: [null, 85], decimals: 1 },
    { key: "gateway.disk_used_percent", name: "Disco utilizado", unit: "%", group: "recursos", type: "numeric", origin: "exists", base: 17.5, expected: [null, 90], decimals: 1 },
    { key: "environment.temperature_c", name: "Temperatura ambiente", unit: "°C", group: "recursos", type: "numeric", origin: "exists", base: 22.4, expected: [10, 45], decimals: 1 },
    { key: "environment.humidity_percent", name: "Umidade ambiente", unit: "%", group: "recursos", type: "numeric", origin: "exists", base: 55, expected: [20, 80], decimals: 1 },

    { key: "gateway.mqtt_connected", name: "MQTT conectado", unit: "", group: "conectividade", type: "boolean", origin: "exists" },
    { key: "gateway.network_interface_up", name: "Interface de rede", unit: "", group: "conectividade", type: "boolean", origin: "exists" },
    { key: "gateway.time_synchronized", name: "Horário sincronizado", unit: "", group: "conectividade", type: "boolean", origin: "exists" },
    { key: "gateway.network_rx_rate", name: "Rede — recepção", unit: "kB/s", group: "conectividade", type: "numeric", origin: "derived", base: 6.2, expected: [null, null], decimals: 1 },
    { key: "gateway.network_tx_rate", name: "Rede — envio", unit: "kB/s", group: "conectividade", type: "numeric", origin: "derived", base: 3.1, expected: [null, null], decimals: 1 },

    { key: "gateway.messages_per_min", name: "Mensagens / min", unit: "msg/min", group: "processamento", type: "numeric", origin: "derived", base: 42, expected: [null, null], decimals: 0 },
    { key: "gateway.queue_depth", name: "Fila local (spool)", unit: "msg", group: "processamento", type: "numeric", origin: "exists", base: 0.4, expected: [null, 50], decimals: 0 },
    { key: "gateway.replayed_messages", name: "Mensagens reenviadas (acum.)", unit: "msg", group: "processamento", type: "counter", origin: "exists", base: 0, decimals: 0 },
    { key: "gateway.dropped_messages", name: "Mensagens descartadas (acum.)", unit: "msg", group: "processamento", type: "counter", origin: "exists", base: 0, decimals: 0 },
    { key: "gateway.uptime_sec", name: "Tempo ativo", unit: "s", group: "processamento", type: "numeric", origin: "exists", base: 0, decimals: 0 }
  ];

  const GROUPS = {
    recursos: "Recursos",
    conectividade: "Conectividade",
    processamento: "Processamento"
  };

  // ---- Geração de série por cenário ----------------------------------------
  function diurnal(t) {
    const h = new Date(t).getHours() + new Date(t).getMinutes() / 60;
    return Math.sin(((h - 6) / 24) * Math.PI * 2); // -1..1, pico ~12h
  }

  function buildSeries(metric, scenario, r) {
    const pts = [];
    let droppedC = 71;
    let replayedC = 10820;
    let walk = 0; // ruído autocorrelado (random walk amortecido) — traços realistas em qualquer zoom

    // Janela de dado disponível conforme cenário
    let firstIdx = 0;
    let lastValidTs = NOW;
    if (scenario === "offline") lastValidTs = NOW - 41 * 60 * 1000;
    if (scenario === "delayed") lastValidTs = NOW; // chega, mas com atraso de ingestão
    if (scenario === "no-history") firstIdx = COUNT - Math.floor((9 * 60 * 1000) / STEP_MS);
    if (scenario === "rejections") lastValidTs = NOW;

    for (let i = firstIdx; i < COUNT; i++) {
      const ts = NOW - (COUNT - i) * STEP_MS;
      if (ts > lastValidTs) break;

      // Lacuna de sensor: dia -2, 02:10–02:40
      const d = new Date(ts);
      const gapDayAgo = (NOW - ts) > 40 * 3600 * 1000 && (NOW - ts) < 46 * 3600 * 1000;
      const inGap = gapDayAgo && d.getHours() === 2 && d.getMinutes() >= 10 && d.getMinutes() <= 40;

      // Evento correlacionado: pico de carga+temp no dia -1 por volta de 20:00–20:20
      const eventWindow = (NOW - ts) > 20.5 * 3600 * 1000 && (NOW - ts) < 24 * 3600 * 1000;
      const d1 = new Date(ts);
      const inEvent = eventWindow && d1.getHours() === 20 && d1.getMinutes() <= 25;
      const eventBoost = inEvent ? (1 - d1.getMinutes() / 25) : 0;

      // Janela offline no fim (só cenário offline): últimas ~50 min antes de cair
      const offlineRamp = scenario === "offline" && ts > NOW - 90 * 60 * 1000;

      let value = null;
      if (metric.type === "boolean") {
        if (metric.key === "gateway.mqtt_connected") {
          value = !(offlineRamp && ts > NOW - 55 * 60 * 1000);
          if (gapDayAgo && d.getHours() === 2 && d.getMinutes() < 55) value = d.getMinutes() % 12 !== 0;
        } else if (metric.key === "gateway.time_synchronized") {
          value = true;
        } else {
          value = !(scenario === "offline" && ts > NOW - 20 * 60 * 1000);
        }
        pts.push({ t: ts, v: value });
        continue;
      }

      if (inGap) { pts.push({ t: ts, v: null }); continue; }

      walk = walk * 0.88 + (r() - 0.5) * 0.9;
      const noise = walk; // ~ -1..1, suave
      switch (metric.key) {
        case "gateway.cpu_temperature_c":
          value = metric.base + diurnal(ts) * 3.5 + noise * 1.2 + eventBoost * 18 + (offlineRamp ? 4 : 0);
          break;
        case "gateway.load_1m":
          value = Math.max(0, metric.base + noise * 0.12 + eventBoost * 1.5 + (offlineRamp ? 0.4 : 0));
          break;
        case "gateway.memory_used_percent":
          value = metric.base + diurnal(ts) * 2 + noise * 1.5 + eventBoost * 9;
          break;
        case "gateway.disk_used_percent":
          // sobe devagar ao longo dos dias, com pequena variação de escrita/rotação de log
          value = metric.base - ((COUNT - i) / COUNT) * 0.8 + Math.sin(i / 40) * 0.25 + noise * 0.15;
          break;
        case "environment.temperature_c":
          value = metric.base + diurnal(ts) * 4.5 + noise * 0.3;
          break;
        case "environment.humidity_percent":
          value = metric.base - diurnal(ts) * 10 + noise * 1.5;
          break;
        case "gateway.network_rx_rate":
          value = Math.max(0, metric.base + noise * 2.4 + (r() < 0.05 ? r() * 30 : 0));
          break;
        case "gateway.network_tx_rate":
          value = Math.max(0, metric.base + noise * 1.5 + eventBoost * 2);
          break;
        case "gateway.messages_per_min":
          value = offlineRamp ? Math.max(0, 42 - (NOW - ts < 55 * 60 * 1000 ? 42 : 10)) : 40 + Math.round(noise * 4);
          if (scenario === "offline" && ts > NOW - 55 * 60 * 1000) value = 0;
          break;
        case "gateway.queue_depth":
          value = offlineRamp && ts > NOW - 55 * 60 * 1000
            ? Math.round((NOW - 55 * 60 * 1000 < ts ? (ts - (NOW - 55 * 60 * 1000)) / (55 * 60 * 1000) * 44 : 0))
            : (r() < 0.12 ? 1 : 0);
          if (scenario === "rejections" && r() < 0.3) value = 2 + Math.floor(r() * 4);
          break;
        case "gateway.uptime_sec":
          value = (ts - (NOW - COUNT * STEP_MS)) / 1000;
          break;
        case "gateway.replayed_messages":
          // reenvio do spool: quase parado em operação normal, salta na recuperação pós-offline
          replayedC += (scenario === "offline" && ts > NOW - 20 * 60 * 1000) ? Math.round(2 + r() * 4) : (r() < 0.04 ? 1 : 0);
          value = replayedC;
          break;
        case "gateway.dropped_messages":
          droppedC += scenario === "rejections" ? Math.round(r() * 3) : (r() < 0.02 ? 1 : 0);
          value = droppedC;
          break;
        default:
          value = metric.base + noise;
      }
      if (metric.decimals != null) value = Number(value.toFixed(metric.decimals));
      pts.push({ t: ts, v: value });
    }
    return pts;
  }

  function buildDataset(scenario) {
    const r = rng(0x51F0 + scenario.length * 7);
    const series = {};
    METRICS.forEach((m) => { series[m.key] = buildSeries(m, scenario, r); });

    const lastTs = Math.max(...METRICS
      .filter((m) => series[m.key].length)
      .map((m) => series[m.key][series[m.key].length - 1].t));

    const ingestionLagMs = scenario === "delayed" ? 8.5 * 60 * 1000 : 1200;

    const events = [];
    events.push({
      t: NOW - 21.7 * 3600 * 1000,
      title: "Pico de temperatura acompanhado de carga alta",
      detail: "gateway.cpu_temperature_c subiu para ~60 °C junto com gateway.load_1m ~1,6. Voltou à faixa normal em ~6 min. Co-ocorrência detectada; causa não inferida.",
      metrics: ["gateway.cpu_temperature_c", "gateway.load_1m"],
      severity: "warning"
    });
    events.push({
      t: NOW - 43.5 * 3600 * 1000,
      title: "Lacuna de dados (~30 min)",
      detail: "Sem amostras entre 02:10 e 02:40. O agente continuou no ar; provável reinício do coletor local. Nenhum dado foi convertido em zero.",
      metrics: ["gateway.cpu_temperature_c", "gateway.memory_used_percent"],
      severity: "neutral"
    });
    if (scenario === "offline") {
      events.push({
        t: NOW - 55 * 60 * 1000,
        title: "MQTT desconectado — fila local acumulando",
        detail: "gateway.mqtt_connected = false. gateway.queue_depth passou de 0 para 44 mensagens. Última telemetria recebida há ~41 min.",
        metrics: ["gateway.mqtt_connected", "gateway.queue_depth"],
        severity: "danger"
      });
    }
    if (scenario === "rejections") {
      events.push({
        t: NOW - 3.2 * 3600 * 1000,
        title: "Aumento de mensagens descartadas",
        detail: "gateway.dropped_messages subiu ~40 em 3 h. Verificar payloads fora do schema ou limite de cardinalidade do workspace.",
        metrics: ["gateway.dropped_messages"],
        severity: "warning"
      });
    }

    return {
      scenario,
      generatedAt: NOW,
      device: {
        name: "Edgewarden Gateway",
        identifier: "edgewarden",
        category: "Gateway",
        agent: "fluxo-gateway 2.4.1",
        address: "192.168.9.18",
        workspace: "Gateway Pi Pilot",
        tenant: "gateway-pilot-91f6a6e1be"
      },
      lastTelemetryTs: lastTs,
      ingestionLagMs,
      tz: "UTC−03:00",
      series,
      events: events.sort((a, b) => b.t - a.t)
    };
  }

  global.FluxoDemo = {
    NOW,
    STEP_MS,
    METRICS,
    GROUPS,
    SCENARIOS: [
      { id: "normal", label: "Operação normal" },
      { id: "offline", label: "Dispositivo offline" },
      { id: "delayed", label: "Dado atrasado" },
      { id: "no-history", label: "Sem histórico anterior" },
      { id: "rejections", label: "Rejeições subindo" }
    ],
    build: buildDataset
  };
})(window);
