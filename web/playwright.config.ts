import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests against the whole bank: Keycloak, gateway, three services, RabbitMQ, PostgreSQL and Angular.
 * Locally: start the AppHost, then `npm run e2e`. In CI: see the e2e job of ci.yml.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  timeout: 90_000,
  // A transfer crosses three services and a broker: allow for cold starts.
  expect: { timeout: 20_000 },
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'fr-FR',
    // Keycloak runs with the Aspire development certificate.
    ignoreHTTPSErrors: true,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
