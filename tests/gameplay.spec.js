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
    // Override the beforeEach: load fresh, then start an all-AI game so
    // the Reveal phase runs autonomously with no human-click block.
    await page.goto('/index.html');
    await page.evaluate(() => { try { localStorage.clear(); } catch (_) {} });
    await page.waitForFunction(() => typeof window.startGame === 'function');
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });

    // One Shown hand should grow within a few seconds (~600ms of AI delay
    // before the card is revealed).
    await page.waitForFunction(() => {
      const total = window.gameState.players.reduce((s, p) => s + p.shownHand.length, 0);
      return total >= 1;
    }, null, { timeout: 5_000 });

    // The human-input prompt should NOT have been shown.
    const prompt = await page.locator('#prompt-overlay').innerText().catch(() => '');
    expect(prompt).not.toMatch(/Choose an opponent/i);
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
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });

    // Wait for the prompt overlay to show a trick result. The new Determine
    // sequence runs ~2s of animations, so allow a generous timeout.
    await expect(page.locator('#prompt-overlay')).toContainText(
      /(wins the trick|Tie!|nobody wins|no prize)/i, { timeout: 30_000 });
  });

  test('winner receives the prize card into their scoring zone', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      void window.startGame({ humanId: -1 });
      // The new Determine sequence blocks on a click before pushing the prize.
      // Auto-dismiss whenever a click-wait is pending.
      window.__auto = setInterval(() => {
        const r = window.gameState?.resolveInput;
        if (typeof r === 'function') {
          window.gameState.resolveInput = null;
          try { r(); } catch (_) {}
        }
      }, 50);
    });

    // Wait until at least one trick has resolved into a scoringZone, OR
    // the prompt indicates a tied/void trick.
    await page.waitForFunction(() =>
      window.gameState.players.some(p => p.scoringZone.length > 0) ||
      /(tie|void)/i.test(document.getElementById('prompt-overlay')?.innerText ?? ''),
      null, { timeout: 30_000 });

    const ok = await page.evaluate(() => {
      clearInterval(window.__auto);
      const rounds = window.gameState.round;
      const totalScored = window.gameState.players
        .reduce((s, p) => s + p.scoringZone.length, 0);
      return totalScored <= rounds;
    });
    expect(ok).toBe(true);
  });

  test('a REAL user click dismisses the trick-result wait and the round advances', async ({ page }) => {
    // Every other test auto-resolves resolveInput from script — this one
    // exercises the actual document-level click listener users rely on.
    await loadApp(page);
    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
    });

    // Wait until round 1's Determine sequence is parked on its click-wait.
    await page.waitForFunction(() =>
      window.gameState?.phase === 'Determine' &&
      typeof window.gameState.resolveInput === 'function',
      null, { timeout: 15_000 });

    const roundBefore = await page.evaluate(() => window.gameState.round);
    await page.locator('#play-zone').click({ force: true });

    await page.waitForFunction((rb) => window.gameState.round > rb, roundBefore, {
      timeout: 10_000,
    });
    expect(await page.evaluate(() => window.gameState.round)).toBe(roundBefore + 1);
  });

  test('pressing Enter also dismisses the trick-result wait (keyboard access)', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
    });

    await page.waitForFunction(() =>
      window.gameState?.phase === 'Determine' &&
      typeof window.gameState.resolveInput === 'function',
      null, { timeout: 15_000 });

    const roundBefore = await page.evaluate(() => window.gameState.round);
    await page.keyboard.press('Enter');

    await page.waitForFunction((rb) => window.gameState.round > rb, roundBefore, {
      timeout: 10_000,
    });
  });
});

test.describe('Keyboard play (accessibility)', () => {
  test('human cards are focusable buttons; Enter plays the focused card', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    // Resolve the reveal phase via keyboard: opponent badges become buttons.
    await page.waitForFunction(() =>
      document.querySelectorAll('.selectable-player').length >= 3);
    const badgeRole = await page.locator('#info-1').getAttribute('role');
    expect(badgeRole).toBe('button');
    await page.locator('#info-1').focus();
    await page.keyboard.press('Enter');

    // Now it's the human's turn to play: cards must be focusable buttons.
    await expect(page.locator('#prompt-overlay')).toContainText(/select a card/i, {
      timeout: 5_000,
    });
    const card = page.locator('#hidden-0 .card.clickable').first();
    expect(await card.getAttribute('role')).toBe('button');
    expect(await card.getAttribute('tabindex')).toBe('0');
    expect(await card.getAttribute('aria-label')).toMatch(/^Play /);

    const before = await page.evaluate(() => window.gameState.playZone.length);
    await card.focus();
    await page.keyboard.press('Enter');

    await page.waitForFunction((b) =>
      window.gameState.playZone.length > b, before, { timeout: 5_000 });
    expect(await page.evaluate(() => window.gameState.playZone.length)).toBe(before + 1);
  });
});

test.describe('Determine-phase decorations', () => {
  // Tests for the visual sequence: cut on tied cards → crown on winner →
  // cut on prize-tied → coins on prize. We exercise decorateCard directly
  // (it's the contract createCardElement and the orchestration both rely
  // on) and then run an integration test that drives the full sequence.

  test.beforeEach(async ({ page }) => {
    await loadApp(page);
    await startGame(page);
  });

  test('decorateCard("crown") adds CROWN_SVG inside the card element', async ({ page }) => {
    const result = await page.evaluate(() => {
      const card = new window.Card('♣', 14);
      window.gameState.playZone = [{
        card, player: window.gameState.players[0], section: 'Hidden',
      }];
      window.renderPlayZone();
      window.decorateCard(card, 'crown');
      return {
        flagSet: card.hasCrown === true,
        crownCount: document.querySelectorAll('#play-slot-0 .card-crown').length,
      };
    });
    expect(result.flagSet).toBe(true);
    expect(result.crownCount).toBe(1);
  });

  test('decorateCard("coins") adds COINS_SVG inside the card element', async ({ page }) => {
    const result = await page.evaluate(() => {
      const card = new window.Card('♦', 13);
      window.gameState.playZone = [{
        card, player: window.gameState.players[0], section: 'Hidden',
      }];
      window.renderPlayZone();
      window.decorateCard(card, 'coins');
      return {
        flagSet: card.hasCoins === true,
        coinsCount: document.querySelectorAll('#play-slot-0 .card-coins').length,
      };
    });
    expect(result.flagSet).toBe(true);
    expect(result.coinsCount).toBe(1);
  });

  test('decorateCard("cut") applies .cut class to the card element', async ({ page }) => {
    const result = await page.evaluate(() => {
      const card = new window.Card('♥', 7);
      window.gameState.playZone = [{
        card, player: window.gameState.players[0], section: 'Hidden',
      }];
      window.renderPlayZone();
      window.decorateCard(card, 'cut');
      return {
        flagSet: card.isCut === true,
        hasCutClass: document.querySelector('#play-slot-0 .card')?.classList.contains('cut'),
      };
    });
    expect(result.flagSet).toBe(true);
    expect(result.hasCutClass).toBe(true);
  });

  test('decoration flags survive a re-render of the play zone', async ({ page }) => {
    // The flags live on the Card object so re-renders re-emit the SVGs and
    // re-apply the .cut class. This guards against a refactor that switches
    // to one-shot DOM-only decoration.
    const result = await page.evaluate(() => {
      const card = new window.Card('♣', 14);
      card.hasCrown = true;
      card.hasCoins = true;
      card.isCut    = true;
      window.gameState.playZone = [{
        card, player: window.gameState.players[0], section: 'Hidden',
      }];
      window.renderPlayZone();    // first render
      window.renderPlayZone();    // second render — flags must still produce output
      const cardEl = document.querySelector('#play-slot-0 .card');
      return {
        hasCutClass: cardEl?.classList.contains('cut'),
        crownCount:  document.querySelectorAll('#play-slot-0 .card-crown').length,
        coinsCount:  document.querySelectorAll('#play-slot-0 .card-coins').length,
      };
    });
    expect(result.hasCutClass).toBe(true);
    expect(result.crownCount).toBe(1);
    expect(result.coinsCount).toBe(1);
  });

  test('integration: cut → crown → coins fires across the Determine sequence', async ({ page }) => {
    // Rig a play zone with a 2-card value tie (two 10s discarded), an Ace
    // winner, and a K as prize. Trigger executeDeterminePhase and watch
    // the DOM at each animation beat.
    await page.evaluate(() => {
      window.gameState.isRunning = false;     // stop the running gameLoop
      window.gameState.phase = 'Determine';
      const C = (s, r) => new window.Card(s, r);
      const ps = window.gameState.players;
      window.gameState.playZone = [
        { card: C('♣', 10), player: ps[0], section: 'Hidden' },  // tied
        { card: C('♦', 14), player: ps[1], section: 'Hidden' },  // WINNER
        { card: C('♥', 10), player: ps[2], section: 'Hidden' },  // tied
        { card: C('♠', 13), player: ps[3], section: 'Hidden' },  // PRIZE
      ];
      window.gameState.isRunning = true;
      window.renderPlayZone();
      void window.executeDeterminePhase();
    });

    // Step 1: tied 10s should pick up .cut.
    await expect.poll(
      () => page.locator('.card.cut').count(),
      { timeout: 4_000 }
    ).toBeGreaterThanOrEqual(2);

    // Step 2: crown should appear on the winner's card.
    await expect.poll(
      () => page.locator('#play-slot-1 .card-crown').count(),
      { timeout: 4_000 }
    ).toBe(1);

    // Step 3: coin pile should appear on the prize card.
    await expect.poll(
      () => page.locator('#play-slot-3 .card-coins').count(),
      { timeout: 4_000 }
    ).toBe(1);
  });
});

test.describe('Edge case: no hidden cards left', () => {
  test('reveal phase skips cleanly when every player has empty Hidden', async ({ page }) => {
    // Build a gameState directly with all 52 cards in shown hands (zero in
    // hidden) and drive gameLoop. We deliberately bypass startGame because
    // startGame's synchronous chain enters executeRevealPhase before the
    // test can mutate hands — by the time the mutation lands, validTargets
    // has already been computed against the full deal and the reveal logic
    // tries to splice a card from a now-empty array.
    await loadApp(page);
    await page.evaluate(() => {
      document.getElementById('menu-screen').style.display = 'none';
      document.getElementById('game-board').style.display = 'block';

      const players = [
        new window.Player(0, 'South', false, 'Easy'),
        new window.Player(1, 'West',  false, 'Easy'),
        new window.Player(2, 'North', false, 'Easy'),
        new window.Player(3, 'East',  false, 'Easy'),
      ];
      const deck = new window.Deck();
      // Deal every card into shown so hidden is empty for all four seats.
      players.forEach(p => { p.shownHand = deck.deal(13); });

      window.gameState = {
        players,
        activePlayerId: null,
        round: 1, phase: 'Reveal', starterId: 0,
        playZone: [], discards: [],
        isRunning: true, resolveInput: null,
      };
      window.renderAll();
      void window.gameLoop();
    });

    // executeRevealPhase should detect zero valid targets, skip the block,
    // and advance phase to 'Play'. The status text updates as the next
    // gameLoop iteration enters executePlayPhase.
    await expect(page.locator('#status-text')).toContainText(/Play/i, { timeout: 8_000 });
  });
});
