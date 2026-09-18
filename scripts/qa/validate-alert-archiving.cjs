const { chromium } = require('../../e2e/node_modules/playwright');
const fs = require('fs');
const assert = require('assert/strict');
const base = process.env.FLUXO_ARCHIVE_QA_URL || 'http://127.0.0.1:4182';
const rule = { id: 'revision-1', ruleId: 'rule-1', workspaceId: 'workspace-qa', version: 2, name: 'Temperatura alta', metricDefinitionId: 'metric-1', deviceIdentifier: null, valueType: 'Numeric', unit: '°C', operator: 'GreaterThan', threshold: 30, thresholdHigh: null, hysteresis: 1, durationSeconds: 60, cooldownSeconds: 300, expectedIntervalSeconds: 300, severity: 'Warning', enabled: true, activatedAtUtc: '2026-09-17T12:00:00Z', createdAtUtc: '2026-09-17T12:00:00Z', authorId: 'operador-qa' };
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const checks = [];
  try {
    for (const width of [1440, 768, 375, 320]) {
      const page = await browser.newPage({ viewport: { width, height: 950 } });
      let archived = null;
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.addInitScript(() => sessionStorage.setItem('fluxo.auth.session', JSON.stringify({ token: 'qa-token', user: { userId: 'operador-qa', email: 'qa@example.test' }, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() })));
      await page.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        let body = [];
        if (url.pathname.endsWith('/archive')) {
          assert.equal(route.request().method(), 'POST');
          assert.deepEqual(route.request().postDataJSON(), { expectedVersion: 2 });
          archived = { ...rule, id: 'revision-archive', version: 3, enabled: false, archivedAtUtc: '2026-09-17T13:00:00Z', createdAtUtc: '2026-09-17T13:00:00Z' };
          body = archived;
        } else if (url.pathname.endsWith('/revisions')) body = [rule, archived];
        else if (url.pathname.endsWith('/rules')) body = url.searchParams.get('status') === 'archived' ? (archived ? [archived] : []) : (archived ? [] : [rule]);
        else if (url.pathname.endsWith('/metric-definitions')) body = [{ id: 'metric-1', displayName: 'Temperatura' }];
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
      });
      await page.goto(base + '/workspaces/workspace-qa/alerts');
      await page.getByRole('button', { name: 'Arquivar', exact: true }).click();
      await page.getByRole('alertdialog').waitFor();
      await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
      assert.equal(archived, null);
      await page.getByRole('button', { name: 'Arquivar', exact: true }).click();
      if (width === 1440) await page.screenshot({ path: 'artifacts/alert-archive-confirmation.png', fullPage: true });
      await page.getByRole('button', { name: 'Confirmar arquivamento', exact: true }).click();
      await page.getByText(/O histórico foi preservado/).waitFor();
      await page.getByRole('button', { name: 'Arquivadas', exact: true }).click();
      await page.getByRole('button', { name: 'Ver revisões', exact: true }).click();
      await page.getByText(/Versão 3/).waitFor();
      assert.equal(await page.getByRole('link', { name: 'Editar', exact: true }).count(), 0);
      assert.equal(await page.getByRole('button', { name: 'Ativar', exact: true }).count(), 0);
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      assert.ok(scrollWidth <= width, `Overflow ${width}: ${scrollWidth}`);
      assert.deepEqual(errors, []);
      if (width === 1440 || width === 375) await page.screenshot({ path: `artifacts/alert-archive-${width}.png`, fullPage: true });
      checks.push({ width, confirmation: true, cancellation: true, archive: true, revisions: true, overflow: false, errors });
      await page.close();
    }
    fs.writeFileSync('artifacts/alert-archive-qa.json', JSON.stringify({ api: 'mocked; persistence verified separately by PostgreSQL integration tests', checks }, null, 2));
    console.log(JSON.stringify(checks));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
