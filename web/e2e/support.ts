import { Browser, Page, expect } from '@playwright/test';

/** Realm users (deploy/keycloak/ledgerline-realm.json): demo-only credentials, shown on the welcome page. */
const DEMO_PASSWORD = 'ledgerline-demo';

/** Signs in through the real Keycloak login page, each user in an isolated browser context. */
export async function loginAs(
  browser: Browser,
  username: 'alice' | 'bob' | 'chloe' | 'olivia',
): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('/');
  await page.locator(`[data-user="${username}"]`).click();

  await expect(page.locator('#username')).toHaveValue(username);
  await page.locator('#password').fill(DEMO_PASSWORD);
  await page.locator('#kc-login').click();

  await page.waitForURL(username === 'olivia' ? '**/ops/reviews' : '**/accounts');
  return page;
}

export async function sendTransfer(
  page: Page,
  options: { from?: RegExp; beneficiary: string; amount: number; label: string },
): Promise<void> {
  await page.goto('/transfers/new');
  if (options.from) {
    const select = page.locator('select');
    const option = select.locator('option', { hasText: options.from });
    await select.selectOption(await option.getAttribute('value'));
  }
  await page.locator(`[data-beneficiary="${options.beneficiary}"]`).click();
  await page.getByLabel('Montant (€)').fill(String(options.amount));
  await page.getByLabel('Libellé (facultatif)').fill(options.label);
  await page.getByRole('button', { name: 'Envoyer le virement' }).click();
  await page.waitForURL(/\/transfers\/[0-9a-f-]{36}$/);
}

export const badge = (page: Page) => page.locator('.page-header app-status-badge');
