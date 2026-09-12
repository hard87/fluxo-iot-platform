import { test, expect } from "@playwright/test";

/**
 * Exercises the same flow a human walked through manually on 2026-09-12 to find the
 * DeviceCategory 400 and confirm the auth-session-persistence fix: register -> login ->
 * create workspace -> provision device -> reload keeps the session -> rejections page loads
 * -> an unknown route renders 404 inside the authenticated layout.
 *
 * This is the only layer in the repo that drives the real browser against the real API/DB/MQTT
 * stack, so it is the guard rail for wire-format and full-stack regressions that unit/integration
 * tests (which never leave the C# process) cannot see. See tests/Fluxo.IntegrationTests/Api/
 * JsonContractTests.cs for the narrower, faster contract-level guard on the same class of bug.
 */
test("register, create workspace, provision device, and survive a reload", async ({ page }) => {
  const unique = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `e2e-${unique}@fluxo.local`;
  const password = "E2eTest!23456";

  await page.goto("/");
  await page.getByRole("link", { name: "Criar conta inicial" }).click();

  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Senha", { exact: true }).fill(password);
  await page.getByLabel("Confirmar senha").fill(password);
  await page.getByRole("button", { name: "Criar conta" }).click();

  await expect(page.getByRole("heading", { name: "Workspaces", exact: true })).toBeVisible();

  const workspaceName = `E2E Workspace ${unique}`;
  await page.getByLabel("Nome").fill(workspaceName);
  await page.getByRole("button", { name: "Criar workspace" }).click();

  await expect(page.getByText(workspaceName)).toBeVisible();
  await page.getByRole("link", { name: "Dispositivos", exact: true }).click();

  await page.getByRole("link", { name: "Cadastrar novo dispositivo" }).click();
  await page.getByLabel("Nome").fill("E2E Sensor");
  await page.getByLabel("Identificador").fill(`e2e-sensor-${unique}`);
  await page.getByRole("button", { name: "Salvar dispositivo" }).click();

  // The exact bug this suite guards: provisioning a device from the real browser form used to
  // fail with a 400 because the API rejected the category string the form sends.
  await expect(page.getByRole("heading", { name: /Credencial gerada/ })).toBeVisible();

  await page.reload();
  await expect(page.getByRole("button", { name: "Sair" })).toBeVisible();

  await page.getByRole("link", { name: "Rejeições" }).click();
  await expect(page.getByRole("heading", { name: "Mensagens rejeitadas" })).toBeVisible();

  await page.goto("/rota-que-nao-existe");
  await expect(page.getByRole("heading", { name: "Página não encontrada" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Sair" })).toBeVisible();
});
