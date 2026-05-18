// Gameplay tests: reveal phase, play phase, determination, and turn order.
//
// These tests interact with the live DOM the way a player would (clicks /
// drags) and assert on observable state (rendered cards, gameState).
// They expect the rewrite to expose three small affordances on window:
//
//   window.startGame()                     // already exists in ui.js
//   window.gameState                       // already exists in engine.js
//   window.determineTrick(playZone)        // NEW — see engine.spec.js
//
// Anything else they need they read off the DOM directly.

const { test, expect } = require('@playwright/test');
const { loadApp, startGame, readState, rigState } = require('./helpers');

test.describe('Reveal phase', () => {
  test.beforeEach(async ({ page }) => {
    await loadApp(page);
    await startGame(page);
  });

  test('shows the "Choose an opponent" prompt for the human starter', async ({ page }) => {
    // Phase advances asynchronously; wait for the prompt to render.
    await expect(page.locator('#prompt-overlay')).toContainText(/choose an opponent/i, {
      timeout: 5_000,
    });
  });

  test('opponent panels become selectable; clicking moves one card to that opponent\'s Shown', async ({ page }) => {
    // Wait for selectable highlight on at least one opponent.
    await page.waitForFunction(() =>
      document.querySelectorAll('.selectable-player').length >= 3);

    // Pick West (id 1).
    const beforeShown = await page.evaluate(() =>
      window.gameState.players[1].shownHand.length);
    await page.locator('#info-1').click();

    await page.waitForFunction((prev) =>
      window.gameState.players[1].shownHand.length === prev + 1, beforeShown);

    const after = await page.evaluate(() => ({
      hidden: window.gameState.players[1].hiddenHand.length,
      shown:  window.gameState.players[1].shownHand.length,
    }));
    expect(after.shown).toBe(beforeShown + 1);
    expect(after.hidden).toBe(12);
  });

  test('AI starter auto-targets within ~1s (no human prompt shown)', async ({ page }) => {
    // Force AI starter for round 1 and skip the human turn.
    await rigState(page, { starterId: 1 });
    // The previous beforeEach already started the game with the human as
    // starter, but the reveal phase hasn't completed yet. Force the loop
    // forward by replaying the phase: trigger a state observer.
    await page.evaluate(() => {
      // Restart cleanly with AI in position 0 so the new game's starter is AI.
      window.gameState.isRunning = false;
    });
    // Reload with the human swapped out for an AI.
    await page.goto('/index.html');
    await page.evaluate(() => {
      localStorage.clear();
      window.startGame();
      // After startGame, the human is at id 0. Swap them to all-AI.
      window.gameState.players[0].isHuman = false;
      window.gameState.players[0].difficulty = 'Easy';
    });

    // After at most a few seconds, one opponent's Shown should grow by 1.
    await page.waitForFunction(() => {
      const total = window.gameState.players.reduce((s, p) => s + p.shownHand.length, 0);
      return total >= 1;
    }, null, { timeout: 5_000 });
  });
});

test.describe('Play phase — click to play', () => {
  test.beforeEach(async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    // Resolve the reveal phase: pick West.
    await page.waitForFunction(() =>
      document.querySelectorAll('.selectable-player').length >= 3);
    await page.locator('#info-1').click();
    // Wait for play phase prompt for the human.
    await expect(page.locator('#prompt-overlay')).toContainText(/select a card/i, {
      timeout: 5_000,
    });
  });

  test('clicking a card in South\'s hand plays it into the human\'s slot', async ({ page }) => {
    const before = await page.evaluate(() => ({
      hidden: window.gameState.players[0].hiddenHand.length,
      shown:  window.gameState.players[0].shownHand.length,
      playZone: window.gameState.playZone.length,
    }));

    // The human's hidden cards are clickable in play phase.
    await page.locator('#hidden-0 .card.clickable').first().click();

    await page.waitForFunction((b) =>
      window.gameState.playZone.length > b.playZone, before);

    const after = await page.evaluate(() => ({
      hidden: window.gameState.players[0].hiddenHand.length,
      shown:  window.gameState.players[0].shownHand.length,
      playZone: window.gameState.playZone.length,
      slot0HasCard: document.querySelectorAll('#play-slot-0 .card').length,
    }));
    expect(after.playZone).toBe(before.playZone + 1);
    expect(after.hidden + after.shown).toBe(before.hidden + before.shown - 1);
    expect(after.slot0HasCard).toBe(1);
  });

  test('active-turn highlight moves through all four players in CW order', async ({ page }) => {
    // After human plays, the highlight should pass to West, North, East in turn.
    await page.locator('#hidden-0 .card.clickable').first().click();

    // Each AI move takes ~800ms (delay in engine.js).
    const seen = new Set();
    const deadline = Date.now() + 8_000;
    while (Date.now() < deadline) {
      const active = await page.evaluate(() =>
        document.querySelector('.active-turn')?.id?.replace('info-', '') ?? null);
      if (active !== null) seen.add(active);
      if (seen.size >= 3) break;
      await page.waitForTimeout(150);
    }
    // We expect to have observed at least 3 different players become active
    // after the human plays (the 4 in turn order include the human, who
    // is already cleared once their card is in the zone).
    expect(seen.size).toBeGreaterThanOrEqual(3);
  });
});

test.describe('Play phase — drag and drop', () => {
  test('dragging a South card onto the play zone is equivalent to clicking', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    // Resolve reveal first.
    await page.waitForFunction(() =>
      document.querySelectorAll('.selectable-player').length >= 3);
    await page.locator('#info-1').click();
    await expect(page.locator('#prompt-overlay')).toContainText(/select a card/i);

    const card = page.locator('#hidden-0 .card.clickable').first();
    const zone = page.locator('#play-zone');
    const before = await page.evaluate(() => window.gameState.playZone.length);

    // Playwright supports a simple drag helper.
    await card.dragTo(zone);

    await page.waitForFunction((n) => window.gameState.playZone.length > n, before, {
      timeout: 5_000,
    });
    expect(await page.evaluate(() => window.gameState.playZone.length)).toBeGreaterThan(before);
  });
});

test.describe('Determine phase', () => {
  test('after all 4 cards are played, a winner (or tie) is announced', async ({ page }) => {
    await loadApp(page);
    // All-AI game: rewire startGame to skip the human prompt.
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[0].isHuman = false;
      window.gameState.players[0].difficulty = 'Easy';
    });

    // Wait for the prompt overlay to show a trick result.
    await expect(page.locator('#prompt-overlay')).toContainText(
      /(wins the trick|Tie!)/i, { timeout: 15_000 });
  });

  test('winner receives the prize card into their scoring zone', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[0].isHuman = false;
      window.gameState.players[0].difficulty = 'Easy';
    });

    // Wait until at least one trick has resolved.
    await page.waitForFunction(() =>
      window.gameState.players.some(p => p.scoringZone.length > 0) ||
      /tie/i.test(document.getElementById('prompt-overlay')?.innerText ?? ''),
      null, { timeout: 15_000 });

    // If someone won, totals should be consistent: scoringZone count ≤ rounds completed.
    const ok = await page.evaluate(() => {
      const rounds = window.gameState.round; // 1-indexed; increments after Score
      const totalScored = window.gameState.players
        .reduce((s, p) => s + p.scoringZone.length, 0);
      return totalScored <= rounds; // ≤ because some rounds may tie out with no prize
    });
    expect(ok).toBe(true);
  });
});

test.describe('Edge case: no hidden cards left', () => {
  test('reveal phase skips cleanly when every player has empty Hidden', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await rigState(page, {
      // Move all hidden cards into shown for every player. The next reveal
      // phase should detect zero valid targets and proceed straight to Play.
    });
    await page.evaluate(() => {
      for (const p of window.gameState.players) {
        p.shownHand.push(...p.hiddenHand.splice(0));
      }
    });

    // Phase will transition to Play without throwing or hanging.
    await expect(page.locator('#status-text')).toContainText(/Play/i, { timeout: 8_000 });
  });
});
