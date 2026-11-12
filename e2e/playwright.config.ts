import { defineConfig } from '@playwright/test'

/**
 * The whole app, as it runs in production: start it first with `docker compose --profile app up -d --build`.
 * Locally the tests use the installed Microsoft Edge (no browser download); in CI, Playwright's Chromium.
 */
export default defineConfig({
  testDir: './tests',
  globalSetup: './tests/global-setup.ts',
  // Each test creates its own pharmacy, so they can run in parallel.
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  timeout: 60_000,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    channel: process.env.CI ? undefined : 'msedge',
    locale: 'es-CO',
    timezoneId: 'America/Bogota',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
})
