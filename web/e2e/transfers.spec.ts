import { expect, test } from '@playwright/test';
import { badge, loginAs, sendTransfer } from './support';

test('a large transfer waits for an analyst, then completes live on the customer screen', async ({
  browser,
}) => {
  const alice = await loginAs(browser, 'alice');
  const label = `E2E review ${Date.now()}`;
  await sendTransfer(alice, { from: /Livret/, beneficiary: 'Bob Durand', amount: 3500, label });

  await expect(badge(alice)).toContainText('En revue par un analyste');
  await expect(alice.locator('[data-status="PendingReview"]')).toContainText('Large amount');

  const olivia = await loginAs(browser, 'olivia');
  const card = olivia.locator('.review', { hasText: /3\s500,00/ }).first();
  await expect(card).toContainText('Montant élevé');
  await card.getByRole('button', { name: 'Approuver' }).click();

  // No reload: SignalR pushes each saga step to Alice's open page.
  await expect(badge(alice)).toContainText('Exécuté');
  await expect(alice.locator('.stages li[data-state="done"]')).toHaveCount(5);

  await olivia.goto('/ops/journal');
  await expect(olivia.locator('.entry', { hasText: label })).toContainText('Équilibrée');
});

test('a transfer above the available balance fails and the hold is never taken', async ({
  browser,
}) => {
  const chloe = await loginAs(browser, 'chloe');
  const before = await chloe.locator('.account .balance').first().innerText();

  await sendTransfer(chloe, { beneficiary: 'Bob Durand', amount: 99_000, label: 'E2E overdraft' });

  await expect(badge(chloe)).toContainText('Échoué');
  await expect(chloe.locator('[data-status="Failed"]')).toContainText('Insufficient');
  await chloe.goto('/accounts');
  await expect(chloe.locator('.account .balance').first()).toHaveText(before);
});

test('a payment to a blocklisted IBAN is rejected and the funds are released', async ({
  browser,
}) => {
  const bob = await loginAs(browser, 'bob');
  await sendTransfer(bob, {
    beneficiary: 'Société Écran SARL',
    amount: 50,
    label: 'E2E blocklist',
  });

  await expect(badge(bob)).toContainText('Refusé');
  await expect(bob.locator('.stages')).toContainText('Libération des fonds');
  await bob.goto('/accounts');
  await expect(bob.locator('.held')).toHaveCount(0);
});

test('whatever happened, the books still sum to zero', async ({ browser }) => {
  const olivia = await loginAs(browser, 'olivia');
  await olivia.goto('/ops/trial-balance');
  await expect(olivia.locator('.proof.ok')).toContainText('Les livres sont équilibrés');
});
