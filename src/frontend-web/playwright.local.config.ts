import { defineConfig, devices } from '@playwright/test'

// Config E2E de DEBUG LOCAL: frontend con `pnpm dev` (:3000) y backend ejecutado a mano
// desde el IDE en :5285 (NEXT_PUBLIC_API_URL=http://localhost:5285/api/v1).
// No dockeriza nada ni toca bigschool_e2e: pensada para iteración rápida con breakpoints.
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
  },
})
