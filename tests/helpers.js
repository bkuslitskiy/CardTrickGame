// Shared test helpers.
//
// These tests treat the JS rewrite as a behavioural spec: the suite is
// expected to fail until the rebuild is complete. Each helper is documented
// so failures point at a specific contract the rewrite has to honour.
//
// Most helpers run inside the browser via page.evaluate() and rely on
// engine.js / ai.js / ui.js attaching their classes and `gameState` to the
// global scope (which they do today, via plain <script> tags).

const SUITS = ['♣', '♦', '♥', '♠']; // ♣ ♦ ♥ ♠

/**
 * Navigate to the app and wait for engine.js to finish parsing.
 * Clears localStorage first so every test starts from a clean slate.
 */
async function loadApp(page) {
  await page.goto('/index.html');
  await page.evaluate(() => {
    try { localStorage.clear(); } catch (_) {}
  });
  // Bare existence check — fast-fails if engine.js failed to load.
  await page.waitForFunction(() => typeof window.Card === 'function');
}

/**
 * Click "Start New Game" with the current difficulty selections.
 * Resolves once the deal is complete (each player has 13 hidden cards).
 */
async function startGame(page, opts = {}) {
  if (opts.diff1) await page.selectOption('#diff-1', opts.diff1);
  if (opts.diff2) await page.selectOption('#diff-2', opts.diff2);
  if (opts.diff3) await page.selectOption('#diff-3', opts.diff3);

  await page.evaluate(() => {
    // Drive startGame() directly: clicking the button works too, but going
    // through the function bypasses any CSS issues with overlay z-index.
    return window.startGame();
  }).catch(() => {});
  // Wait for the deal to take effect on the gameState object.
  await page.waitForFunction(() =>
    window.gameState &&
    window.gameState.isRunning &&
    window.gameState.players?.length === 4 &&
    window.gameState.players.every(p => p.hiddenHand.length === 13)
  );
}

/** Read a structural snapshot of the current gameState (no functions). */
async function readState(page) {
  return page.evaluate(() => {
    const gs = window.gameState;
    return {
      phase: gs.phase,
      round: gs.round,
      starterId: gs.starterId,
      isRunning: gs.isRunning,
      activePlayerId: gs.activePlayerId,
      playZoneSize: gs.playZone.length,
      players: gs.players.map(p => ({
        id: p.id,
        name: p.name,
        isHuman: p.isHuman,
        difficulty: p.difficulty,
        hiddenCount: p.hiddenHand.length,
        shownCount: p.shownHand.length,
        scoringCount: p.scoringZone.length,
        score: p.score,
      })),
    };
  });
}

/**
 * Force the gameState into a known shape for deterministic tests.
 * Accepts an object of overrides applied after the standard deal.
 * The test must already have called startGame() once.
 */
async function rigState(page, overrides) {
  await page.evaluate((ov) => {
    Object.assign(window.gameState, ov);
  }, overrides);
}

module.exports = {
  SUITS,
  loadApp,
  startGame,
  readState,
  rigState,
};
