import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'

// Config E2E POR DEFECTO: entorno dockerizado y aislado (CI / runs reproducibles).
// globalSetup levanta frontend+backend en contenedores y la BD bigschool_e2e;
// globalTeardown lo limpia todo. Para depurar en local usa playwright.local.config.ts.
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: 'html',
  globalSetup: path.resolve(__dirname, './tests/e2e/global-setup.ts'),
  globalTeardown: path.resolve(__dirname, './tests/e2e/global-teardown.ts'),
  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
