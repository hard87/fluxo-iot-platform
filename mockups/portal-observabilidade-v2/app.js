/*
 * Fluxo Portal — POC "Observabilidade do dispositivo"
 * app.js — render + interações. Vanilla JS, sem dependências. Dados simulados.
 */
(function () {
  "use strict";
  var D = window.FluxoDemo;
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var NS = "http://www.w3.org/2000/svg";

  // ---------------------------------------------------------------- preferences
  var PREFS = { theme: "auto", contrast: "normal", "text-scale": "100", motion: "system" };
  function loadPrefs() {
    try {
      var raw = JSON.parse(localStorage.getItem("fluxo-poc-prefs") || "{}");
      Object.keys(PREFS).forEach(function (k) { if (raw[k]) PREFS[k] = raw[k]; });
    } catch (e) { /* private mode / blocked — usa defaults */ }
  }
  function savePrefs() { try { localStorage.setItem("fluxo-poc-prefs", JSON.stringify(PREFS)); } catch (e) {} }
  function applyPrefs() {
    var html = document.documentElement;
    html.setAttribute("data-theme", PREFS.theme);
    html.setAttribute("data-contrast", PREFS.contrast);
    html.setAttribute("data-text-scale", PREFS["text-scale"]);
    html.setAttribute("data-motion", PREFS.motion);
    $$('[data-pref] input').forEach(function (input) {
      var group = input.closest("[data-pref]").getAttribute("data-pref");
      input.checked = String(PREFS[group]) === input.value;
    });
  }

  // ---------------------------------------------------------------- state
  var STATE = {
    scenario: "normal",
    range: "6h",
    view: "mult",
    solo: null,
    visible: null // Set of metric keys shown in the chart
  };

  // métricas numéricas que entram no gráfico consolidado (curadoria)
  var CHART_KEYS = [
    "gateway.cpu_temperature_c",
    "gateway.load_1m",
    "gateway.memory_used_percent",
    "gateway.disk_used_percent",
    "gateway.queue_depth",
    "gateway.messages_per_min"
  ];
  var COLORS = ["--c1", "--c2", "--c3", "--c4", "--c5", "--c6", "--c7", "--c8"];
  var DASH = ["", "dash", "dot", "", "dash", "dot", "", "dash"];

  var dataset = null;

  function meta(key) { return D.METRICS.filter(function (m) { return m.key === key; })[0]; }
  function cssvar(name) { return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || "#888"; }

  // ---------------------------------------------------------------- formatting
  function nf(v, dec) {
    if (v == null || isNaN(v)) return "—";
    return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: dec || 0, maximumFractionDigits: dec == null ? 2 : dec }).format(v);
  }
  function fmtValue(m, v) {
    if (v == null) return "—";
    if (m.key === "gateway.uptime_sec") return fmtDuration(v);
    return nf(v, m.decimals == null ? 1 : m.decimals) + (m.unit ? " " + m.unit : "");
  }
  function fmtDuration(sec) {
    sec = Math.max(0, Math.floor(sec));
    var d = Math.floor(sec / 86400); sec -= d * 86400;
    var h = Math.floor(sec / 3600); sec -= h * 3600;
    var mn = Math.floor(sec / 60);
    var parts = [];
    if (d) parts.push(d + " d");
    if (h || d) parts.push(h + " h");
    parts.push(mn + " min");
    return parts.join(" ");
  }
  function crossesDay(t0, t1) {
    return new Date(t0).getDate() !== new Date(t1).getDate() || (t1 - t0) > 24 * 3600 * 1000;
  }
  function fmtTick(t, withDay) {
    var o = withDay ? { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" } : { hour: "2-digit", minute: "2-digit" };
    return new Intl.DateTimeFormat("pt-BR", o).format(new Date(t));
  }
  function fmtFull(t) {
    return new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" }).format(new Date(t)).replace(",", "");
  }
  function relTime(t, now) {
    now = now || Date.now();
    var s = Math.round((now - t) / 1000);
    if (s < 45) return "há " + s + " s";
    var mn = Math.round(s / 60);
    if (mn < 60) return "há " + mn + " min";
    var h = Math.round(mn / 60);
    if (h < 48) return "há " + h + " h";
    return "há " + Math.round(h / 24) + " dias";
  }

  // ---------------------------------------------------------------- windowing
  function windowRange() {
    var now = D.NOW;
    if (STATE.range === "1h") return [now - 3600e3, now];
    if (STATE.range === "6h") return [now - 6 * 3600e3, now];
    if (STATE.range === "24h") return [now - 24 * 3600e3, now];
    // all: do primeiro ao último ponto disponível em qualquer série
    var min = now, max = now - 999 * 24 * 3600e3;
    CHART_KEYS.concat(["gateway.uptime_sec"]).forEach(function (k) {
      var p = dataset.series[k];
      if (p && p.length) { min = Math.min(min, p[0].t); max = Math.max(max, p[p.length - 1].t); }
    });
    return [min, max];
  }
  function slice(points, t0, t1) {
    return points.filter(function (p) { return p.t >= t0 && p.t <= t1; });
  }
  function downsample(points, maxN) {
    if (points.length <= maxN) return points;
    var step = points.length / maxN, out = [];
    for (var i = 0; i < points.length; i += step) out.push(points[Math.floor(i)]);
    if (out[out.length - 1] !== points[points.length - 1]) out.push(points[points.length - 1]);
    return out;
  }

  // stats ignorando null
  function stats(points) {
    var vals = points.filter(function (p) { return p.v != null; }).map(function (p) { return p.v; });
    if (!vals.length) return null;
    var min = Math.min.apply(null, vals), max = Math.max.apply(null, vals);
    var sum = vals.reduce(function (a, b) { return a + b; }, 0);
    var first = vals.slice(0, Math.max(1, Math.floor(vals.length / 3)));
    var last = vals.slice(-Math.max(1, Math.floor(vals.length / 3)));
    var favg = first.reduce(function (a, b) { return a + b; }, 0) / first.length;
    var lavg = last.reduce(function (a, b) { return a + b; }, 0) / last.length;
    var delta = lavg - favg;
    var range = max - min || 1;
    var trend = Math.abs(delta) < range * 0.08 ? "flat" : (delta > 0 ? "up" : "down");
    var gaps = 0;
    for (var i = 1; i < points.length; i++) if (points[i].v == null && points[i - 1].v != null) gaps++;
    return { min: min, max: max, avg: sum / vals.length, cur: vals[vals.length - 1], trend: trend, delta: delta, gaps: gaps, n: vals.length };
  }

  // ---------------------------------------------------------------- SVG line helpers
  function scaleLinear(d0, d1, r0, r1) {
    var m = (r1 - r0) / ((d1 - d0) || 1);
    return function (v) { return r0 + (v - d0) * m; };
  }
  function niceDomain(min, max) {
    if (min === max) { return [min - 1, max + 1]; }
    var pad = (max - min) * 0.12;
    return [min - pad, max + pad];
  }
  function pathFor(points, x, y) {
    var d = "", pen = false;
    points.forEach(function (p) {
      if (p.v == null) { pen = false; return; }
      d += (pen ? "L" : "M") + x(p.t).toFixed(1) + " " + y(p.v).toFixed(1) + " ";
      pen = true;
    });
    return d.trim();
  }
  function gapBands(points, x, h) {
    var bands = [], start = null;
    points.forEach(function (p, i) {
      if (p.v == null && start == null) start = points[i - 1] ? points[i - 1].t : p.t;
      if (p.v != null && start != null) { bands.push([start, p.t]); start = null; }
    });
    if (start != null) bands.push([start, points[points.length - 1].t]);
    return bands.map(function (b) {
      var el = document.createElementNS(NS, "rect");
      el.setAttribute("class", "gap-band");
      el.setAttribute("x", x(b[0])); el.setAttribute("y", 0);
      el.setAttribute("width", Math.max(1, x(b[1]) - x(b[0]))); el.setAttribute("height", h);
      return el;
    });
  }
  function svgEl(tag, attrs) {
    var el = document.createElementNS(NS, tag);
    Object.keys(attrs || {}).forEach(function (k) { el.setAttribute(k, attrs[k]); });
    return el;
  }

  // ---------------------------------------------------------------- render: banner
  function renderBanner() {
    var el = $("#state-banner");
    el.innerHTML = "";
    var sc = STATE.scenario;
    var lag = dataset.ingestionLagMs;
    var last = dataset.lastTelemetryTs;
    var items = [];
    if (sc === "offline") {
      items.push(["danger", "!", "Dispositivo offline",
        "Última telemetria recebida " + relTime(last) + " (" + fmtFull(last) + "). MQTT desconectado; a fila local está acumulando mensagens. Os valores abaixo são os últimos conhecidos, não o estado atual."]);
    } else if (sc === "delayed") {
      items.push(["warning", "~", "Dados chegando com atraso",
        "A ingestão está ~" + Math.round(lag / 60000) + " min atrás da ocorrência. O último ponto ocorreu " + relTime(last) + " mas foi ingerido depois. Compare 'ocorrência' e 'ingestão' antes de agir."]);
    } else if (sc === "no-history") {
      items.push(["info", "i", "Sem histórico anterior",
        "Só há telemetria dos últimos ~9 min para este dispositivo/período. Janelas maiores que isso aparecem vazias — não é falha, é ausência de dados."]);
    } else if (sc === "rejections") {
      items.push(["warning", "!", "Mensagens sendo descartadas",
        "gateway.dropped_messages subiu nas últimas horas. Veja o tile 'Mensagens descartadas' e o evento correlacionado abaixo."]);
    }
    items.forEach(function (it) {
      var b = document.createElement("div");
      b.className = "banner " + it[0];
      b.innerHTML = '<span class="glyph" aria-hidden="true">' + it[1] + '</span><span><b>' + it[2] + '</b>' + it[3] + '</span>';
      el.appendChild(b);
    });
  }

  // ---------------------------------------------------------------- render: status header
  function freshnessState() {
    var age = D.NOW - dataset.lastTelemetryTs;
    if (STATE.scenario === "offline") return { cls: "down", label: "Offline", detail: "sem telemetria " + relTime(dataset.lastTelemetryTs) };
    if (age > 5 * 60 * 1000) return { cls: "stale", label: "Sem dados recentes", detail: "última telemetria " + relTime(dataset.lastTelemetryTs) };
    return { cls: "live", label: "Recebendo dados", detail: "última telemetria " + relTime(dataset.lastTelemetryTs) };
  }
  function renderStatusHeader() {
    var el = $("#status-header");
    var f = freshnessState();
    var dev = dataset.device;
    var mqtt = lastBool("gateway.mqtt_connected");
    var net = lastBool("gateway.network_interface_up");
    var up = lastNum("gateway.uptime_sec");
    var opBadge = STATE.scenario === "offline"
      ? '<span class="badge danger"><span class="b-dot"></span>Offline</span>'
      : (STATE.scenario === "rejections"
        ? '<span class="badge warning"><span class="b-dot"></span>Atenção</span>'
        : '<span class="badge success"><span class="b-dot"></span>Online</span>');
    el.innerHTML =
      '<div>' +
        '<div class="freshness">' +
          '<span class="pulse-wrap"><span class="dot ' + f.cls + '"></span></span>' +
          '<span>' + f.label + '</span>' +
          '<span class="badge neutral" style="font-weight:600">' + f.detail + '</span>' +
        '</div>' +
        '<div class="meta">' +
          '<span><b>' + dev.name + '</b> · <code>' + dev.identifier + '</code></span>' +
          '<span>' + dev.category + ' · ' + dev.agent + '</span>' +
          '<span>' + dev.address + '</span>' +
          '<span>ligado há ' + (up != null ? fmtDuration(up) : "—") + '</span>' +
          '<span>' + dataset.device.workspace + ' · tenant <code>' + dataset.device.tenant + '</code></span>' +
        '</div>' +
      '</div>' +
      '<div style="display:grid;gap:8px;justify-items:end;align-content:start">' +
        opBadge +
        '<span class="bool-pill ' + (mqtt ? "on" : "off") + '"><span class="b-dot"></span>MQTT ' + (mqtt ? "conectado" : "desconectado") + '</span>' +
        '<span class="bool-pill ' + (net ? "on" : "off") + '"><span class="b-dot"></span>Rede ' + (net ? "ativa" : "inativa") + '</span>' +
        '<span class="section-note">Estado reportado pela API — não recalculado no portal.</span>' +
      '</div>';
  }
  function lastPoint(key) { var p = dataset.series[key]; return p && p.length ? p[p.length - 1] : null; }
  function lastNum(key) { for (var p = dataset.series[key] || [], i = p.length - 1; i >= 0; i--) if (p[i].v != null) return p[i].v; return null; }
  function lastBool(key) { var lp = lastPoint(key); return lp ? !!lp.v : false; }

  // ---------------------------------------------------------------- render: tiles
  function sparkline(points) {
    var w = 160, h = 30;
    var svg = svgEl("svg", { class: "spark", viewBox: "0 0 " + w + " " + h, preserveAspectRatio: "none", role: "presentation" });
    var pts = points.filter(function (p) { return p.v != null; });
    if (pts.length < 2) return svg;
    var t0 = points[0].t, t1 = points[points.length - 1].t;
    var vs = pts.map(function (p) { return p.v; });
    var dom = niceDomain(Math.min.apply(null, vs), Math.max.apply(null, vs));
    var x = scaleLinear(t0, t1, 2, w - 2), y = scaleLinear(dom[0], dom[1], h - 3, 3);
    svg.appendChild(svgEl("path", { d: pathFor(points, x, y), fill: "none", stroke: "currentColor", "stroke-width": "1.5" }));
    return svg;
  }
  function renderSignals() {
    var host = $("#signals");
    host.innerHTML = "";
    var w = windowRange();
    Object.keys(D.GROUPS).forEach(function (g) {
      var label = document.createElement("p");
      label.className = "group-label";
      label.textContent = D.GROUPS[g];
      host.appendChild(label);
      var grid = document.createElement("div");
      grid.className = "tile-grid";
      host.appendChild(grid);

      D.METRICS.filter(function (m) { return m.group === g; }).forEach(function (m) {
        grid.appendChild(tileFor(m, w));
      });
    });
  }
  function tileFor(m, w) {
    var pts = slice(dataset.series[m.key] || [], w[0], w[1]);
    var el = document.createElement("article");
    el.className = "tile";
    var originTag = m.origin === "derived" ? '<span class="origin-tag" data-o="derived">derivado</span>'
      : (m.origin === "backend" ? '<span class="origin-tag" data-o="backend">backend</span>' : "");

    if (m.type === "boolean") {
      var lp = pts.length ? pts[pts.length - 1] : lastPoint(m.key);
      var on = lp && lp.v;
      // encontrar última mudança
      var changed = null;
      for (var i = pts.length - 1; i > 0; i--) { if (!!pts[i].v !== !!pts[i - 1].v) { changed = pts[i].t; break; } }
      el.setAttribute("data-state", on ? "ok" : (m.key === "gateway.mqtt_connected" ? "bad" : "warn"));
      el.innerHTML =
        '<div class="t-name"><span>' + m.name + '</span>' + originTag + '</div>' +
        '<div class="t-value"><span class="bool-pill ' + (on ? "on" : "off") + '"><span class="b-dot"></span>' + (on ? "sim" : "não") + '</span></div>' +
        '<div class="t-foot"><span>' + (changed ? "mudou " + relTime(changed) : "estável na janela") + '</span></div>';
      return el;
    }

    if (m.type === "counter") {
      var st0 = stats(pts);
      var delta = st0 ? (st0.max - st0.min) : null;
      var isDrop = m.key === "gateway.dropped_messages";
      el.setAttribute("data-state", isDrop && delta > 5 ? "warn" : "ok");
      el.innerHTML =
        '<div class="t-name"><span>' + m.name + '</span>' + originTag + '</div>' +
        '<div class="t-value">' + (st0 ? nf(st0.cur, 0) : "—") + '<span class="unit"> acum.</span></div>' +
        '<div class="t-foot"><span>+' + (delta != null ? nf(delta, 0) : "—") + ' na janela</span></div>';
      return el;
    }

    if (m.key === "gateway.uptime_sec") {
      var upv = pts.length ? pts[pts.length - 1].v : lastNum(m.key);
      el.setAttribute("data-state", STATE.scenario === "offline" ? "stale" : "ok");
      el.innerHTML =
        '<div class="t-name"><span>' + m.name + '</span>' + originTag + '</div>' +
        '<div class="t-value" style="font-size:var(--step-1)">' + (upv != null ? fmtDuration(upv) : "—") + '</div>' +
        '<div class="t-foot"><span>contador monotônico do agente · zera em reinício</span></div>';
      return el;
    }

    var st = stats(pts);
    var stale = STATE.scenario === "offline" || (D.NOW - (lastPoint(m.key) ? lastPoint(m.key).t : 0) > 5 * 60 * 1000 && STATE.range !== "all");
    var cur = st ? st.cur : lastNum(m.key);
    var overMax = m.expected && m.expected[1] != null && cur != null && cur >= m.expected[1] * 0.9;
    var bad = m.expected && m.expected[1] != null && cur != null && cur >= m.expected[1];
    el.setAttribute("data-state", stale ? "stale" : (bad ? "bad" : (overMax ? "warn" : "ok")));

    var trendClass = st ? st.trend : "flat";
    var goodDir = (m.key === "gateway.disk_used_percent" || m.key === "gateway.cpu_temperature_c" || m.key === "gateway.load_1m" || m.key === "gateway.memory_used_percent" || m.key === "gateway.queue_depth");
    var trendMod = trendClass === "up" ? (goodDir ? "" : " good") : (trendClass === "down" ? (goodDir ? "" : " bad") : "");
    var arrow = trendClass === "up" ? "▲" : trendClass === "down" ? "▼" : "▬";

    var color = cssvar(COLORS[CHART_KEYS.indexOf(m.key) % COLORS.length] || "--c5");
    var sl = sparkline(downsample(pts, 60));
    sl.style.color = color;

    el.innerHTML =
      '<div class="t-name"><span>' + m.name + '</span>' + originTag + '</div>' +
      '<div class="t-value">' + (cur != null ? nf(cur, m.decimals == null ? 1 : m.decimals) : "—") +
        (m.unit ? '<span class="unit">' + m.unit + '</span>' : "") + '</div>';
    el.appendChild(sparkWrap(sl));
    var foot = document.createElement("div");
    foot.className = "t-foot";
    foot.innerHTML =
      (stale
        ? '<span class="t-stale">sem dado ' + relTime(lastPoint(m.key) ? lastPoint(m.key).t : D.NOW) + '</span>'
        : '<span class="trend ' + trendClass + trendMod + '">' + arrow + ' ' + (st ? (trendClass === "flat" ? "estável" : nf(Math.abs(st.delta), 1) + " " + (m.unit || "")) : "—") + '</span>') +
      '<span>pico ' + (st ? nf(st.max, m.decimals == null ? 1 : m.decimals) : "—") + (m.unit ? " " + m.unit : "") + '</span>';
    el.appendChild(foot);

    if (m.expected && (m.expected[0] != null || m.expected[1] != null)) {
      var exp = document.createElement("div");
      exp.className = "t-foot";
      exp.innerHTML = '<span>faixa esperada: ' +
        (m.expected[0] != null ? m.expected[0] : "—") + ' a ' + (m.expected[1] != null ? m.expected[1] : "—") +
        (m.unit ? " " + m.unit : "") + '</span>';
      el.appendChild(exp);
    } else {
      var exp2 = document.createElement("div");
      exp2.className = "t-foot";
      exp2.innerHTML = '<span>faixa esperada: não definida no catálogo</span>';
      el.appendChild(exp2);
    }
    return el;
  }
  function sparkWrap(svg) { var d = document.createElement("div"); d.appendChild(svg); return d; }

  // ---------------------------------------------------------------- chart series model
  function chartModels() {
    var w = windowRange();
    return CHART_KEYS.map(function (key, i) {
      var m = meta(key);
      var pts = downsample(slice(dataset.series[key] || [], w[0], w[1]), 260);
      return {
        key: key, name: m.name, unit: m.unit, decimals: m.decimals,
        color: cssvar(COLORS[i % COLORS.length]),
        dash: DASH[i % DASH.length],
        points: pts,
        stats: stats(pts)
      };
    });
  }
  function visibleModels(models) {
    if (!STATE.visible) return models;
    return models.filter(function (m) { return STATE.visible.has(m.key); });
  }

  var cursorIdx = null; // índice no eixo de tempo compartilhado
  var sharedTimes = [];

  function renderChart() {
    var body = $("#chart-body");
    body.innerHTML = "";
    var w = windowRange();
    var withDay = crossesDay(w[0], w[1]);
    $("#chart-period-label").textContent =
      fmtTick(w[0], withDay) + " — " + fmtTick(w[1], withDay) + " · horário local (" + dataset.tz + ")";

    var models = chartModels();
    var vis = visibleModels(models);
    // eixo de tempo compartilhado: união dos timestamps (downsampled)
    var tset = {};
    models.forEach(function (mm) { mm.points.forEach(function (p) { tset[p.t] = 1; }); });
    sharedTimes = Object.keys(tset).map(Number).sort(function (a, b) { return a - b; });
    if (cursorIdx == null || cursorIdx >= sharedTimes.length) cursorIdx = sharedTimes.length - 1;

    if (vis.length === 0) {
      body.innerHTML = '<div class="banner neutral"><span><b>Nenhuma série visível</b>Use a legenda ou "Ver tudo" para mostrar séries.</span></div>';
      renderLegend(models);
      renderSummary([]); renderTable([]);
      return;
    }

    if (STATE.view === "overlay") body.appendChild(overlayChart(vis, w, withDay));
    else body.appendChild(smallMultiples(vis, w, withDay));

    renderLegend(models);
    renderSummary(vis);
    renderTable(vis);
    updateCursorReadout(vis);
  }

  function xAxis(svg, x, w, withDay, W, H) {
    var ticks = 5;
    for (var i = 0; i <= ticks; i++) {
      var t = w[0] + (w[1] - w[0]) * (i / ticks);
      var px = x(t);
      svg.appendChild(svgEl("line", { class: "gridline", x1: px, y1: 0, x2: px, y2: H }));
      var tx = svgEl("text", { class: "axis-label", x: px, y: H + 12, "text-anchor": i === 0 ? "start" : (i === ticks ? "end" : "middle") });
      tx.textContent = fmtTick(t, withDay);
      svg.appendChild(tx);
    }
  }
  function eventMarkers(svg, x, w, H) {
    dataset.events.forEach(function (ev) {
      if (ev.t < w[0] || ev.t > w[1]) return;
      svg.appendChild(svgEl("line", { class: "event-line", x1: x(ev.t), y1: 0, x2: x(ev.t), y2: H }));
    });
  }

  function smallMultiples(models, w, withDay) {
    var wrap = document.createElement("div");
    wrap.className = "smallmults";
    var W = 900, H = 56;
    var x = scaleLinear(w[0], w[1], 4, W - 4);

    models.forEach(function (mm) {
      var row = document.createElement("div");
      row.className = "mult-row";
      var st = mm.stats;
      var cur = st ? nf(st.cur, mm.decimals == null ? 1 : mm.decimals) + (mm.unit ? " " + mm.unit : "") : "—";
      row.innerHTML =
        '<div class="m-label"><b><span class="swatch" style="background:' + mm.color + '"></span>' + mm.name + '</b>' +
        '<span class="m-cur">atual ' + cur + (st ? " · mín " + nf(st.min, mm.decimals == null ? 1 : mm.decimals) + " · máx " + nf(st.max, mm.decimals == null ? 1 : mm.decimals) : "") + '</span></div>';

      var svg = svgEl("svg", { class: "m-svg", viewBox: "0 0 " + W + " " + (H + 16), preserveAspectRatio: "none", role: "img" });
      svg.setAttribute("aria-label", mm.name + ": " + (st ? behaviorSentence(mm) : "sem dados na janela"));
      var dom = st ? niceDomain(st.min, st.max) : [0, 1];
      var y = scaleLinear(dom[0], dom[1], H - 4, 4);
      gapBands(mm.points, x, H).forEach(function (b) { svg.appendChild(b); });
      // baseline
      svg.appendChild(svgEl("line", { class: "gridline", x1: 4, y1: H, x2: W - 4, y2: H }));
      var p = svgEl("path", { class: "series-path " + mm.dash, d: pathFor(mm.points, x, y), stroke: mm.color });
      svg.appendChild(p);
      eventMarkers(svg, x, w, H);
      // cursor
      var cline = svgEl("line", { class: "cursor-line", x1: 0, y1: 0, x2: 0, y2: H, "data-cursor": "1" });
      svg.appendChild(cline);
      // x labels (só na última linha)
      if (mm === models[models.length - 1]) xAxis(svg, x, w, withDay, W, H);
      row.appendChild(svg);
      wrap.appendChild(row);
    });

    attachCursor(wrap, W, x, w);
    positionCursors(wrap, x);
    return wrap;
  }

  function overlayChart(models, w, withDay) {
    var wrap = document.createElement("div");
    wrap.className = "overlay-wrap";
    wrap.innerHTML = '<span class="norm-note">Escala normalizada 0–100% — os números do eixo não são as unidades reais. O valor real aparece no cursor/tooltip.</span>';
    var W = 900, H = 300;
    var x = scaleLinear(w[0], w[1], 40, W - 8);
    var y = scaleLinear(0, 1, H - 6, 6);
    var svg = svgEl("svg", { class: "overlay-svg", viewBox: "0 0 " + W + " " + (H + 18), preserveAspectRatio: "none", role: "img", tabindex: "0" });
    svg.setAttribute("aria-label", "Gráfico sobreposto normalizado de " + models.map(function (m) { return m.name; }).join(", ") + ". Use as setas para percorrer o tempo; o resumo textual está abaixo.");
    // grid Y
    [0, 0.25, 0.5, 0.75, 1].forEach(function (g) {
      svg.appendChild(svgEl("line", { class: "gridline", x1: 40, y1: y(g), x2: W - 8, y2: y(g) }));
      var t = svgEl("text", { class: "axis-label", x: 34, y: y(g) + 3, "text-anchor": "end" });
      t.textContent = (g * 100) + "%";
      svg.appendChild(t);
    });
    models.forEach(function (mm) {
      var st = mm.stats; if (!st) return;
      var span = (st.max - st.min) || 1;
      var norm = mm.points.map(function (p) { return { t: p.t, v: p.v == null ? null : (p.v - st.min) / span }; });
      gapBands(norm, x, H).forEach(function (b) { svg.appendChild(b); });
      svg.appendChild(svgEl("path", { class: "series-path " + mm.dash, d: pathFor(norm, x, y), stroke: mm.color }));
    });
    eventMarkers(svg, x, w, H);
    svg.appendChild(svgEl("line", { class: "cursor-line", x1: 0, y1: 0, x2: 0, y2: H, "data-cursor": "1" }));
    xAxis(svg, x, w, withDay, W, H);
    wrap.appendChild(svg);
    attachCursor(wrap, W, x, w);
    positionCursors(wrap, x);
    return wrap;
  }

  function nearestTimeIdx(t) {
    var best = 0, bd = Infinity;
    for (var i = 0; i < sharedTimes.length; i++) {
      var d = Math.abs(sharedTimes[i] - t);
      if (d < bd) { bd = d; best = i; }
    }
    return best;
  }
  function attachCursor(wrap, W, x, w) {
    function handle(clientX, target) {
      var svg = target.closest("svg") || wrap.querySelector("svg");
      var rect = svg.getBoundingClientRect();
      var frac = (clientX - rect.left) / rect.width;
      var t = w[0] + (w[1] - w[0]) * Math.max(0, Math.min(1, frac));
      cursorIdx = nearestTimeIdx(t);
      positionCursors(wrap, x);
      updateCursorReadout(visibleModels(chartModels()));
    }
    wrap.addEventListener("mousemove", function (e) { handle(e.clientX, e.target); });
    wrap.addEventListener("click", function (e) { handle(e.clientX, e.target); });
    $$("svg[tabindex]", wrap).forEach(function (svg) {
      svg.addEventListener("keydown", function (e) {
        if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
          e.preventDefault();
          cursorIdx = Math.max(0, Math.min(sharedTimes.length - 1, cursorIdx + (e.key === "ArrowRight" ? 1 : -1)));
          positionCursors(wrap, x);
          updateCursorReadout(visibleModels(chartModels()));
        } else if (e.key === "Home") { cursorIdx = 0; positionCursors(wrap, x); updateCursorReadout(visibleModels(chartModels())); }
        else if (e.key === "End") { cursorIdx = sharedTimes.length - 1; positionCursors(wrap, x); updateCursorReadout(visibleModels(chartModels())); }
      });
    });
  }
  function positionCursors(wrap, x) {
    if (cursorIdx == null || !sharedTimes.length) return;
    var px = x(sharedTimes[cursorIdx]);
    $$("[data-cursor]", wrap).forEach(function (l) { l.setAttribute("x1", px); l.setAttribute("x2", px); });
  }
  function valueAt(model, t) {
    var pts = model.points, best = null, bd = Infinity;
    pts.forEach(function (p) { var d = Math.abs(p.t - t); if (d < bd) { bd = d; best = p; } });
    return best;
  }
  function updateCursorReadout(models) {
    var el = $("#cursor-readout");
    if (cursorIdx == null || !sharedTimes.length) { el.textContent = ""; return; }
    var t = sharedTimes[cursorIdx];
    var parts = models.map(function (m) {
      var p = valueAt(m, t);
      var v = p && p.v != null ? nf(p.v, m.decimals == null ? 1 : m.decimals) + (m.unit ? " " + m.unit : "") : "sem dado";
      return m.name + ": " + v;
    });
    el.innerHTML = '<b>' + fmtFull(t) + '</b> — ' + parts.join(" · ");
  }

  // ---------------------------------------------------------------- legend
  function renderLegend(models) {
    var host = $("#legend");
    host.innerHTML = "";
    var all = document.createElement("button");
    all.className = "legend-all";
    all.type = "button";
    all.textContent = "Ver tudo";
    all.setAttribute("aria-pressed", STATE.visible ? "false" : "true");
    all.addEventListener("click", function () { STATE.visible = null; STATE.solo = null; renderChart(); });
    host.appendChild(all);

    models.forEach(function (m) {
      var shown = !STATE.visible || STATE.visible.has(m.key);
      var b = document.createElement("button");
      b.type = "button";
      b.style.setProperty("--lc", m.color);
      b.setAttribute("aria-pressed", shown ? "true" : "false");
      if (STATE.solo === m.key) b.setAttribute("data-solo", "true");
      var cur = m.stats ? nf(m.stats.cur, m.decimals == null ? 1 : m.decimals) + (m.unit ? " " + m.unit : "") : "—";
      b.innerHTML = '<span class="lg-line" aria-hidden="true"></span><span>' + m.name + '</span><span class="lg-val">' + cur + '</span>' +
        (shown ? "" : '<span class="visually-hidden"> (oculta)</span>');
      b.addEventListener("click", function (e) {
        if (e.ctrlKey || e.shiftKey || e.metaKey) {
          // combinar: alterna esta série no conjunto visível
          if (!STATE.visible) STATE.visible = new Set(models.map(function (x) { return x.key; }));
          if (STATE.visible.has(m.key)) STATE.visible.delete(m.key); else STATE.visible.add(m.key);
          STATE.solo = null;
          if (STATE.visible.size === models.length) STATE.visible = null;
        } else {
          // isolar / restaurar
          if (STATE.solo === m.key) { STATE.solo = null; STATE.visible = null; }
          else { STATE.solo = m.key; STATE.visible = new Set([m.key]); }
        }
        renderChart();
      });
      host.appendChild(b);
    });

    var hint = document.createElement("span");
    hint.className = "section-note";
    hint.style.flexBasis = "100%";
    hint.textContent = "Clique para isolar uma série · Ctrl/Shift+clique para combinar · séries ocultas aparecem riscadas.";
    host.appendChild(hint);
  }

  // ---------------------------------------------------------------- summary + table
  function behaviorSentence(m) {
    var s = m.stats;
    if (!s) return "sem dados na janela.";
    var trend = s.trend === "flat" ? "estável" : (s.trend === "up" ? "em alta" : "em queda");
    var dec = m.decimals == null ? 1 : m.decimals;
    return "atual " + nf(s.cur, dec) + (m.unit ? " " + m.unit : "") +
      ", mín " + nf(s.min, dec) + ", máx " + nf(s.max, dec) + ", média " + nf(s.avg, dec) +
      "; tendência " + trend + (s.gaps ? "; " + s.gaps + " lacuna(s) de dados" : "; sem lacunas") + ".";
  }
  function renderSummary(models) {
    var host = $("#summary");
    host.innerHTML = "";
    if (!models.length) { host.innerHTML = "<li>Nenhuma série selecionada.</li>"; return; }
    models.forEach(function (m) {
      var li = document.createElement("li");
      li.innerHTML = '<span class="s-swatch" style="background:' + m.color + '"></span><span><b>' + m.name + '</b> — ' + behaviorSentence(m) + '</span>';
      host.appendChild(li);
    });
    // atraso de ingestão
    if (dataset.ingestionLagMs > 3 * 60 * 1000) {
      var li2 = document.createElement("li");
      li2.innerHTML = '<span class="s-swatch" style="background:var(--warning)"></span><span><b>Atraso de ingestão</b> — os pontos estão sendo ingeridos ~' + Math.round(dataset.ingestionLagMs / 60000) + ' min após a ocorrência.</span>';
      host.appendChild(li2);
    }
  }
  function renderTable(models) {
    var host = $("#table-wrap");
    host.innerHTML = "";
    if (!models.length) { host.innerHTML = '<p class="section-note" style="padding:12px 14px">Nenhuma série selecionada.</p>'; return; }
    var rows = downsample(sharedTimes.map(function (t) { return { t: t }; }), 48);
    var table = document.createElement("table");
    var cap = document.createElement("caption");
    cap.textContent = "Valores por horário (amostrado) — alternativa textual ao gráfico. Fuso: horário local (" + dataset.tz + ").";
    table.appendChild(cap);
    var thead = document.createElement("thead");
    thead.innerHTML = "<tr><th scope=\"col\">Data e hora</th>" + models.map(function (m) { return '<th scope="col">' + m.name + (m.unit ? " (" + m.unit + ")" : "") + '</th>'; }).join("") + "</tr>";
    table.appendChild(thead);
    var tb = document.createElement("tbody");
    rows.forEach(function (r) {
      var tr = document.createElement("tr");
      var cells = '<th scope="row" class="tabular" style="font-weight:600">' + fmtFull(r.t) + '</th>';
      models.forEach(function (m) {
        var p = valueAt(m, r.t);
        cells += '<td class="tabular">' + (p && p.v != null ? nf(p.v, m.decimals == null ? 1 : m.decimals) : "—") + '</td>';
      });
      tr.innerHTML = cells;
      tb.appendChild(tr);
    });
    table.appendChild(tb);
    host.appendChild(table);
  }

  // ---------------------------------------------------------------- events
  function renderEvents() {
    var host = $("#events");
    host.innerHTML = "";
    var w = windowRange();
    var inWin = dataset.events.filter(function (e) { return e.t >= w[0] && e.t <= w[1]; });
    if (!inWin.length) {
      host.innerHTML = '<div class="banner neutral"><span><b>Nenhum evento correlacionado na janela</b>Amplie o período para ver eventos anteriores.</span></div>';
      return;
    }
    inWin.forEach(function (ev) {
      var d = document.createElement("article");
      d.className = "event " + ev.severity;
      d.innerHTML =
        '<time datetime="' + new Date(ev.t).toISOString() + '">' + fmtFull(ev.t) + ' · horário local</time>' +
        '<b>' + ev.title + '</b>' +
        '<p>' + ev.detail + '</p>' +
        '<div class="chips">' + ev.metrics.map(function (k) { return '<span>' + k + '</span>'; }).join("") + '</div>';
      host.appendChild(d);
    });
  }

  // ---------------------------------------------------------------- wire up
  function rebuild() {
    dataset = D.build(STATE.scenario);
    STATE.solo = null; STATE.visible = null; cursorIdx = null;
    renderBanner();
    renderStatusHeader();
    renderSignals();
    renderChart();
    renderEvents();
  }

  function initScenario() {
    var sel = $("#scenario");
    D.SCENARIOS.forEach(function (s) {
      var o = document.createElement("option");
      o.value = s.id; o.textContent = s.label;
      sel.appendChild(o);
    });
    sel.value = STATE.scenario;
    sel.addEventListener("change", function () { STATE.scenario = sel.value; rebuild(); });
  }
  function initRange() {
    $$(".seg [data-range]").forEach(function (b) {
      b.addEventListener("click", function () {
        STATE.range = b.getAttribute("data-range");
        $$(".seg [data-range]").forEach(function (x) { x.setAttribute("aria-pressed", x === b ? "true" : "false"); });
        cursorIdx = null;
        renderChart(); renderEvents(); renderSignals();
      });
    });
  }
  function initView() {
    $("#view-mult").addEventListener("click", function () { setView("mult"); });
    $("#view-overlay").addEventListener("click", function () { setView("overlay"); });
  }
  function setView(v) {
    STATE.view = v;
    $("#view-mult").setAttribute("aria-pressed", v === "mult" ? "true" : "false");
    $("#view-overlay").setAttribute("aria-pressed", v === "overlay" ? "true" : "false");
    renderChart();
  }
  function initPrefs() {
    var drawer = $("#drawer"), backdrop = $("#drawer-backdrop"), opener = $("#open-prefs");
    function open() { drawer.hidden = false; backdrop.hidden = false; drawer.focus(); }
    function close() { drawer.hidden = true; backdrop.hidden = true; opener.focus(); }
    opener.addEventListener("click", open);
    $("#close-prefs").addEventListener("click", close);
    backdrop.addEventListener("click", close);
    document.addEventListener("keydown", function (e) { if (e.key === "Escape" && !drawer.hidden) close(); });
    $$('[data-pref] input').forEach(function (input) {
      input.addEventListener("change", function () {
        var group = input.closest("[data-pref]").getAttribute("data-pref");
        PREFS[group] = input.value;
        savePrefs(); applyPrefs();
        renderStatusHeader(); renderSignals(); renderChart();
      });
    });
  }
  function initLiveTick() {
    setInterval(function () {
      if (STATE.scenario === "normal") {
        dataset.lastTelemetryTs = D.NOW; // simula chegada contínua
      }
      renderStatusHeader();
    }, 15000);
  }

  document.addEventListener("DOMContentLoaded", function () {
    $("#tz-note").textContent = "horário local (" + "UTC−03:00" + ")";
    loadPrefs();
    applyPrefs();
    initScenario();
    initRange();
    initView();
    initPrefs();
    rebuild();
    initLiveTick();
  });
})();
