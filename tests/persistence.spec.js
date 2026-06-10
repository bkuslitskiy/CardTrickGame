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

  test('starting a new game from the menu replaces any prior save', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    // Make the old save unmistakable: stamp round 5 and persist it.
    await page.evaluate(() => { window.gameState.round = 5; window.saveGame(); });
    const oldSave = await page.evaluate(() =>
      JSON.parse(localStorage.getItem('cardGameState')));
    expect(oldSave.round).toBe(5);

    // Abandon, then start fresh. startGame's synchronous prefix runs
    // clearSavedGame() and gameLoop's first tick re-saves the new game, all
    // before this evaluate returns — so the read is deterministic.
    await page.evaluate(() => window.showMenu());
    const newSave = await page.evaluate(() => {
      void window.startGame();
      return JSON.parse(localStorage.getItem('cardGameState'));
    });
    expect(newSave.round).toBe(1);
    expect(newSave.version).toBe(1);
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
    test.setTimeout(120_000);
    await loadApp(page);

    await page.evaluate(() => {
      // SPEED=0 collapses every delay() — a full 13-round game completes in
      // seconds instead of ~100s of real-time animation waits.
      window.SPEED = 0;

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
      }, 10);
    });

    await page.waitForFunction(
      () => document.getElementById('end-screen')?.style.display !== 'none',
      null,
      { timeout: 90_000 }
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

test.describe('Save schema validation', () => {
  test('corrupted cardGameStats does not break page init (stats read as zeros)', async ({ page }) => {
    // A garbage stats blob used to throw inside DOMContentLoaded, killing the
    // drag-drop setup and resume-button logic along with it.
    await page.goto('/index.html');
    await page.evaluate(() => {
      try {
        localStorage.clear();
        localStorage.setItem('cardGameStats', '{ not json at all');
      } catch (_) {}
    });
    await page.reload();
    await page.waitForFunction(() => typeof window.Card === 'function');

    await expect(page.locator('#stats-display')).toContainText(
      /Games Played:\s*0\s*\|\s*Human Wins:\s*0\s*\|\s*High Score:\s*0/);

    // The rest of the DOMContentLoaded handler must have run too: the play
    // zone drop handler is the last thing it wires up.
    const dropWired = await page.evaluate(() => {
      // dragover listener adds the class; simulate the event to detect it.
      const pz = document.getElementById('play-zone');
      pz.dispatchEvent(new Event('dragover', { bubbles: true, cancelable: true }));
      const wired = pz.classList.contains('drag-over');
      pz.classList.remove('drag-over');
      return wired;
    });
    expect(dropWired).toBe(true);
  });

  test('a save with the wrong version is rejected — resume falls back to the menu', async ({ page }) => {
    await loadApp(page);

    // Build a structurally-plausible save with a bogus version.
    await page.evaluate(() => {
      const save = {
        version: 999,
        players: [0, 1, 2, 3].map(id => ({
          id, name: 'P' + id, isHuman: id === 0, difficulty: 'Easy',
          hiddenHand: [{ suit: '♣', rank: 2 + id }], shownHand: [], scoringZone: [],
        })),
        round: 4, phase: 'Reveal', starterId: 1, playZone: [], discards: [],
      };
      localStorage.setItem('cardGameState', JSON.stringify(save));
      void window.resumeGame();
    });

    // resumeGame must refuse the save: menu stays visible, game not running.
    const state = await page.evaluate(() => ({
      menuVisible: document.getElementById('menu-screen').style.display !== 'none',
      isRunning: window.gameState.isRunning === true,
    }));
    expect(state.menuVisible).toBe(true);
    expect(state.isRunning).toBe(false);
  });

  test('a legacy save without a version field is rejected', async ({ page }) => {
    await loadApp(page);

    await page.evaluate(() => {
      const save = {
        players: [0, 1, 2, 3].map(id => ({
          id, name: 'P' + id, isHuman: id === 0, difficulty: 'Easy',
          hiddenHand: [{ suit: '♣', rank: 2 + id }], shownHand: [], scoringZone: [],
        })),
        round: 2, phase: 'Play', starterId: 0, playZone: [], discards: [],
      };
      localStorage.setItem('cardGameState', JSON.stringify(save));
      void window.resumeGame();
    });

    const running = await page.evaluate(() => window.gameState.isRunning === true);
    expect(running).toBe(false);
  });

  test('a save whose playZone references a non-existent player is rejected', async ({ page }) => {
    await loadApp(page);

    await page.evaluate(() => {
      const save = {
        version: window.SAVE_VERSION,
        players: [0, 1, 2, 3].map(id => ({
          id, name: 'P' + id, isHuman: id === 0, difficulty: 'Easy',
          hiddenHand: [{ suit: '♣', rank: 2 + id }], shownHand: [], scoringZone: [],
        })),
        round: 2, phase: 'Determine', starterId: 0,
        playZone: [{ card: { suit: '♦', rank: 9 }, player: { id: 7 }, section: 'Hidden' }],
        discards: [],
      };
      localStorage.setItem('cardGameState', JSON.stringify(save));
      void window.resumeGame();
    });

    const running = await page.evaluate(() => window.gameState.isRunning === true);
    expect(running).toBe(false);
  });
});

test.describe('Mid-trick save / resume', () => {
  test('a game saved with cards in the play zone resumes with playZone intact', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    // Move real cards from two hands into the play zone (conserving cards),
    // then persist that mid-trick state.
    await page.evaluate(() => {
      const gs = window.gameState;
      const c0 = gs.players[0].hiddenHand[0];
      const c1 = gs.players[1].hiddenHand[0];
      gs.players[0].removeCard(c0);
      gs.players[1].removeCard(c1);
      gs.playZone = [
        { card: c0, player: gs.players[0], section: 'Hidden' },
        { card: c1, player: gs.players[1], section: 'Hidden' },
      ];
      gs.phase = 'Play';
      window.saveGame();
    });

    const saved = await page.evaluate(() => {
      const gs = window.gameState;
      return {
        zone: gs.playZone.map(pz => ({ suit: pz.card.suit, rank: pz.card.rank, playerId: pz.player.id, section: pz.section })),
        hidden: gs.players.map(p => p.hiddenHand.length),
      };
    });

    // Abandon and resume.
    await page.evaluate(() => window.showMenu());
    await page.evaluate(() => { void window.resumeGame(); });
    await page.waitForFunction(() => window.gameState.isRunning === true);

    const restored = await page.evaluate(() => {
      const gs = window.gameState;
      return {
        zone: gs.playZone.map(pz => ({ suit: pz.card.suit, rank: pz.card.rank, playerId: pz.player.id, section: pz.section })),
        hidden: gs.players.map(p => p.hiddenHand.length),
        // playZone entries must reference the SAME player objects as
        // gameState.players, not detached copies.
        identityOk: gs.playZone.every(pz => pz.player === gs.players[pz.player.id]),
        cardsAreCards: gs.playZone.every(pz => typeof pz.card.shownScore === 'number'),
      };
    });

    expect(restored.zone).toEqual(saved.zone);
    expect(restored.hidden).toEqual(saved.hidden);
    expect(restored.identityOk).toBe(true);
    expect(restored.cardsAreCards).toBe(true);
  });
});
