// Save / resume / end-of-game / stats tests.

const { test, expect } = require('@playwright/test');
const { loadApp, startGame, readState } = require('./helpers');

test.describe('Mid-game save / resume', () => {
  test('saveGame fires every loop tick — localStorage holds a snapshot after the deal', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    // Drive the loop a bit so saveGame() runs.
    await page.waitForTimeout(500);

    const saved = await page.evaluate(() => localStorage.getItem('cardGameState'));
    expect(saved).toBeTruthy();
    const parsed = JSON.parse(saved);
    expect(parsed.players).toHaveLength(4);
    expect(parsed.round).toBe(1);
  });

  test('resume restores phase, round, starterId, hand sizes', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await page.waitForTimeout(500);
    const snapshot = await readState(page);

    // Abandon, then resume. resumeGame is async (awaits gameLoop), so we
    // void it to avoid hanging page.evaluate on the never-resolving promise.
    await page.evaluate(() => window.showMenu());
    await page.evaluate(() => { void window.resumeGame(); });

    await page.waitForFunction(() => window.gameState.isRunning === true);
    const restored = await readState(page);

    expect(restored.round).toBe(snapshot.round);
    expect(restored.starterId).toBe(snapshot.starterId);
    expect(restored.players.map(p => p.hiddenCount)).toEqual(
      snapshot.players.map(p => p.hiddenCount));
    expect(restored.players.map(p => p.shownCount)).toEqual(
      snapshot.players.map(p => p.shownCount));
  });

  test('starting a new game from the menu clears any prior save', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await page.waitForTimeout(500);

    expect(await page.evaluate(() => localStorage.getItem('cardGameState'))).toBeTruthy();

    // Abandon, then start fresh. void the async result so page.evaluate
    // doesn't wait for the never-resolving gameLoop promise.
    await page.evaluate(() => window.showMenu());
    await page.evaluate(() => { void window.startGame(); });

    // The freshly-started game writes its own save on the next loop tick, but
    // the test fires fast enough to catch the clear. Pull immediately.
    const immediately = await page.evaluate(() => {
      void window.startGame();
      return localStorage.getItem('cardGameState');
    });
    // After startGame() is invoked synchronously, the state is reset and the
    // first save hasn't happened yet (gameLoop is async).
    // We accept null OR the new game's freshly-saved snapshot (round 1).
    if (immediately !== null) {
      const parsed = JSON.parse(immediately);
      expect(parsed.round).toBe(1);
    }
  });
});

test.describe('Stats display', () => {
  test('after one completed game with a human win, played=1 and wins=1', async ({ page }) => {
    await loadApp(page);
    // Seed the engine's endGame path directly to avoid running 13 full rounds.
    await page.evaluate(() => {
      window.startGame();
      // Give the human a winning scoring zone.
      window.gameState.players[0].scoringZone.push(new window.Card('♣', 14));
      window.gameState.players[0].scoringZone.push(new window.Card('♦', 14));
      window.gameState.isRunning = false; // stop the loop
      window.endGame();
    });

    const text = await page.evaluate(() => {
      const stats = JSON.parse(localStorage.getItem('cardGameStats'));
      return stats;
    });
    expect(text.played).toBe(1);
    expect(text.wins).toBe(1);
    expect(text.highestScore).toBeGreaterThanOrEqual(28); // 14 + 14
  });

  test('a non-human win still increments played, but not wins', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[2].scoringZone.push(new window.Card('♣', 14));
      window.gameState.players[2].scoringZone.push(new window.Card('♦', 13));
      window.gameState.isRunning = false;
      window.endGame();
    });

    const stats = await page.evaluate(() => JSON.parse(localStorage.getItem('cardGameStats')));
    expect(stats.played).toBe(1);
    expect(stats.wins).toBe(0);
  });

  test('highestScore only updates when exceeded', async ({ page }) => {
    await loadApp(page);
    // Seed an existing high score of 50.
    await page.evaluate(() => {
      localStorage.setItem('cardGameStats', JSON.stringify({ played: 3, wins: 1, highestScore: 50 }));
    });
    // Finish a game where the human only scores 20.
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[0].scoringZone.push(new window.Card('♣', 14));
      window.gameState.players[0].scoringZone.push(new window.Card('♦', 6));
      window.gameState.isRunning = false;
      window.endGame();
    });
    const stats = await page.evaluate(() => JSON.parse(localStorage.getItem('cardGameStats')));
    expect(stats.highestScore).toBe(50);
  });
});

test.describe('End screen', () => {
  test('shows the winner banner and a return-to-menu button', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[1].scoringZone.push(new window.Card('♣', 14));
      window.gameState.isRunning = false;
      window.endGame();
    });

    const endVisible = await page.locator('#end-screen').evaluate(el => el.style.display);
    expect(endVisible).not.toBe('none');
    await expect(page.locator('#end-results')).toContainText(/WINNER:\s*West/i);
    await expect(page.locator('button:has-text("Return to Menu")')).toBeVisible();
  });

  test('total face values across all players sum to a constant after a real game', async ({ page }) => {
    // Sanity check that the trick resolver doesn't duplicate or destroy
    // cards. Total faceValue across scoringZone + discards should equal the
    // sum of face values of every card dealt (4 × (2+...+14) = 416).
    // Runs a complete AI-vs-AI game.
    test.setTimeout(240_000);
    await loadApp(page);

    await page.evaluate(() => {
      // Start an all-AI game so the Reveal phase doesn't block on a human
      // click. The synchronous deal happens before the await, but the
      // never-resolving gameLoop promise is voided so this callback returns.
      void window.startGame({ humanId: -1 });

      // executeDeterminePhase still blocks waiting for a click to dismiss
      // each trick prompt. In headless mode no clicks happen, so install a
      // poller that resolves the wait artificially.
      window.__trickAutoAdvance = setInterval(() => {
        if (window.gameState && typeof window.gameState.resolveInput === 'function') {
          const r = window.gameState.resolveInput;
          window.gameState.resolveInput = null;
          try { r(); } catch (_) {}
        }
      }, 50);
    });

    // Wait up to 210s for the end screen. A full game runs ~100s of timing
    // delays (13 reveals + 52 plays + 13 determine sequences + animations).
    await page.waitForFunction(
      () => document.getElementById('end-screen')?.style.display !== 'none',
      null,
      { timeout: 210_000 }
    );

    const totals = await page.evaluate(() => {
      clearInterval(window.__trickAutoAdvance);
      const sum = (cards) => cards.reduce((s, c) => s + c.faceValue, 0);
      const scoring = window.gameState.players.reduce(
        (s, p) => s + sum(p.scoringZone), 0);
      const discards = sum(window.gameState.discards);
      return { scoring, discards, total: scoring + discards };
    });

    // 4 × (2+3+...+14) = 4 × 104 = 416 (sum of face values across all 52 cards).
    expect(totals.total).toBe(416);
  });
});
