// Playwright config for the card-game JS webapp.
// The webServer entry boots a tiny static file server (tools/static-server.js)
// on port 4173 and serves the repository root. Tests then load
// http://localhost:4173/index.html.
//
// We deliberately use a real HTTP origin (not file://) so that localStorage
// behaves the same as in production and tests can safely share an origin.

const { defineConfig, devices } = require('@playwright/test');

const PORT = process.env.PLAYWRIGHT_PORT ? Number(process.env.PLAYWRIGHT_PORT) : 4173;

module.exports = defineConfig({
  testDir: './tests',
  fullyParallel: false,        // Tests share localStorage; keep them serial.
  workers: 1,
  retries: 0,
  reporter: process.env.CI ? 'line' : 'list',
  timeout: 30_000,
  expect: { timeout: 5_000 },

  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    headless: true,
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],

  webServer: {
    command: `node tools/static-server.js ${PORT}`,
    url: `http://localhost:${PORT}/index.html`,
    reuseExistingServer: !process.env.CI,
    timeout: 20_000,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
