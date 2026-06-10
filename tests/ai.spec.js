// AI behaviour tests: target selection and card-play strategy for all three
// difficulty levels.
//
// selectAITarget and selectAICardToPlay are defined in engine.js and exported
// as window.selectAITarget / window.selectAICardToPlay from the same file.
// ai.js is now a stub — no AI logic lives there.
//
// Prerequisites (window globals from engine.js / ui.js):
//   window.Card, window.Player, window.Deck, window.gameState
//   window.startGame({ humanId: -1 })   — starts an all-AI game
//   window.selectAITarget               — exported from engine.js
//   window.selectAICardToPlay           — exported from engine.js

const { test, expect } = require('@playwright/test');
const { loadApp, startGame } = require('./helpers');

// ── Unit tests: selectAITarget ───────────────────────────────────────────────

test.describe('selectAITarget — Easy AI', () => {
  test('always returns one of the valid target IDs', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    for (let trial = 0; trial < 20; trial++) {
      const ok = await page.evaluate(() => {
        const ai = window.gameState.players[0];
        const targets = window.gameState.players.filter(p => p.id !== ai.id);
        const chosen = window.selectAITarget(
          { ...ai, difficulty: 'Easy' }, targets
        );
        return targets.some(t => t.id === chosen);
      });
      expect(ok).toBe(true);
    }
  });
});

test.describe('selectAITarget — Medium AI', () => {
  test('targets the opponent with the fewest hidden cards', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      // Rig hidden hand sizes: player 1=3, player 2=7, player 3=11
      window.gameState.players[1].hiddenHand =
        window.gameState.players[1].hiddenHand.slice(0, 3);
      window.gameState.players[2].hiddenHand =
        window.gameState.players[2].hiddenHand.slice(0, 7);
      window.gameState.players[3].hiddenHand =
        window.gameState.players[3].hiddenHand.slice(0, 11);

      const ai = window.gameState.players[0];
      const targets = window.gameState.players.filter(p => p.id !== ai.id);
      const chosen = window.selectAITarget(
        { ...ai, difficulty: 'Medium' }, targets
      );
      return chosen;
    });
    // Should pick player 1 (fewest hidden cards = 3)
    expect(result).toBe(1);
  });

  test('breaks ties by picking the first in list order (stable)', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      // Players 1 and 2 both have 5 hidden cards; player 3 has 9.
      window.gameState.players[1].hiddenHand =
        window.gameState.players[1].hiddenHand.slice(0, 5);
      window.gameState.players[2].hiddenHand =
        window.gameState.players[2].hiddenHand.slice(0, 5);
      window.gameState.players[3].hiddenHand =
        window.gameState.players[3].hiddenHand.slice(0, 9);

      const ai = window.gameState.players[0];
      const targets = window.gameState.players.filter(p => p.id !== ai.id);
      const chosen = window.selectAITarget(
        { ...ai, difficulty: 'Medium' }, targets
      );
      return chosen;
    });
    // Sort is stable; player 1 appears first in the targets array.
    expect(result).toBe(1);
  });
});

test.describe('selectAITarget — Hard AI', () => {
  test('targets the opponent with the highest current score', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      // Rig scores via scoringZone face values
      const C = (s, r) => new window.Card(s, r);
      // Player 1: score = 5, Player 2: score = 20, Player 3: score = 9
      window.gameState.players[1].scoringZone = [C('♣', 5)];
      window.gameState.players[2].scoringZone = [C('♦', 14), C('♥', 6)];
      window.gameState.players[3].scoringZone = [C('♠', 9)];

      const ai = window.gameState.players[0];
      const targets = window.gameState.players.filter(p => p.id !== ai.id);
      const chosen = window.selectAITarget(
        { ...ai, difficulty: 'Hard' }, targets
      );
      return chosen;
    });
    // Player 2 has highest score (20)
    expect(result).toBe(2);
  });
});

// ── Unit tests: selectAICardToPlay ───────────────────────────────────────────

test.describe('selectAICardToPlay — Easy AI', () => {
  test('returns one of the valid cards', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    for (let i = 0; i < 10; i++) {
      const ok = await page.evaluate(() => {
        const ai = { ...window.gameState.players[0], difficulty: 'Easy' };
        const cards = ai.hiddenHand.slice(0, 4);
        const chosen = window.selectAICardToPlay(ai, cards);
        return cards.includes(chosen);
      });
      expect(ok).toBe(true);
    }
  });
});

test.describe('selectAICardToPlay — Medium AI', () => {
  test('plays the card with the highest play-score from the available hand', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // hiddenScore: rank value; so 9 > 7 > 5
      const cards = [C('♣', 5), C('♦', 9), C('♥', 7)];
      const ai = { ...window.gameState.players[0], difficulty: 'Medium',
                   hiddenHand: cards, shownHand: [] };
      const chosen = window.selectAICardToPlay(ai, cards);
      return chosen.rank;
    });
    // Medium always plays highest play-score → rank 9 (hiddenScore = 9)
    expect(result).toBe(9);
  });

  test('prefers highest shownScore when cards are in the shown hand', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Shown Queen = shownScore 14, Shown King = shownScore 5
      const qn = C('♣', 12); // Queen shown → score 14
      const kg = C('♦', 13); // King shown  → score 5
      const ai = { ...window.gameState.players[0], difficulty: 'Medium',
                   hiddenHand: [], shownHand: [qn, kg] };
      const chosen = window.selectAICardToPlay(ai, [qn, kg]);
      return chosen.rank;
    });
    // Queen Shown (14) beats King Shown (5)
    expect(result).toBe(12); // rank 12 = Queen
  });
});

test.describe('selectAICardToPlay — Hard AI, leading', () => {
  test('leads with the 2nd-best card by face value (not the strongest)', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Face values: 14 > 10 > 7 > 4; 2nd-best = rank 10
      const cards = [C('♣', 14), C('♦', 10), C('♥', 7), C('♠', 4)];
      const ai = { ...window.gameState.players[0], difficulty: 'Hard',
                   hiddenHand: cards, shownHand: [] };
      window.gameState.playZone = []; // empty = leading
      const chosen = window.selectAICardToPlay(ai, cards);
      return chosen.rank;
    });
    expect(result).toBe(10); // 2nd-best face value
  });

  test('plays the only card when hand size is 1', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const only = C('♣', 14);
      const ai = { ...window.gameState.players[0], difficulty: 'Hard',
                   hiddenHand: [only], shownHand: [] };
      window.gameState.playZone = [];
      const chosen = window.selectAICardToPlay(ai, [only]);
      return chosen.rank;
    });
    expect(result).toBe(14);
  });
});

test.describe('selectAICardToPlay — Hard AI, following', () => {
  test('plays the minimum winning card (not the strongest) when a winning card exists', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Leader played hiddenScore 8 (rank 8 hidden). AI holds hidden 9, 11, 14.
      // All three beat the leader. Minimum winner by PLAY score = rank 9 (score 9).
      const leader = C('♥', 8);
      const c9  = C('♣', 9);
      const c11 = C('♦', 11);
      const c14 = C('♠', 14);
      const ai = { ...window.gameState.players[0], difficulty: 'Hard',
                   hiddenHand: [c9, c11, c14], shownHand: [] };
      window.gameState.playZone = [
        { card: leader, player: window.gameState.players[1], section: 'Hidden' },
      ];
      const chosen = window.selectAICardToPlay(ai, [c9, c11, c14]);
      return chosen.rank;
    });
    expect(result).toBe(9); // minimum play-score winner
  });

  test('prefers Shown King (play score 5) over 6♣ Hidden (play score 6) as minimum winner', async ({ page }) => {
    // This is the key regression test for the play-score sort bug.
    // Before the fix, winning cards were sorted by faceValue (K=13 > 6),
    // causing the AI to wrongly discard the 6 and play the King as winner.
    // After the fix, they are sorted by play score (K Shown=5 < 6 Hidden=6),
    // so the Shown King is correctly chosen as the cheaper winner.
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Leader played 4 Hidden (play score 4). Both Shown King (score 5) and
      // 6 Hidden (score 6) beat it. Shown King is the cheaper winner.
      const leader = C('♥', 4);
      const kg = C('♣', 13); // King  — shown, shownScore = 5, faceValue = 13
      const c6 = C('♦', 6);  // Six   — hidden, hiddenScore = 6, faceValue = 6
      const c9 = C('♠', 9);  // Nine  — hidden, hiddenScore = 9, faceValue = 9
      const ai = { ...window.gameState.players[0], difficulty: 'Hard',
                   hiddenHand: [c6, c9], shownHand: [kg] };
      window.gameState.playZone = [
        { card: leader, player: window.gameState.players[1], section: 'Hidden' },
      ];
      const chosen = window.selectAICardToPlay(ai, [kg, c6, c9]);
      return { rank: chosen.rank, score: chosen.shownScore ?? chosen.hiddenScore };
    });
    // Shown King has play score 5 — cheapest winner, should be chosen.
    expect(result.rank).toBe(13);
    expect(result.score).toBe(5);
  });

  test('plays lowest face-value card when it cannot win', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Leader played hiddenScore 14 (Ace hidden). AI holds 3, 6, 9 — all lose.
      const leader = C('♥', 14);
      const c3 = C('♣', 3);
      const c6 = C('♦', 6);
      const c9 = C('♠', 9);
      const ai = { ...window.gameState.players[0], difficulty: 'Hard',
                   hiddenHand: [c3, c6, c9], shownHand: [] };
      window.gameState.playZone = [
        { card: leader, player: window.gameState.players[1], section: 'Hidden' },
      ];
      const chosen = window.selectAICardToPlay(ai, [c3, c6, c9]);
      return chosen.rank;
    });
    expect(result).toBe(3); // discard lowest face-value to minimise prize given away
  });
});

// ── Integration: round-starter advancement ───────────────────────────────────

test.describe('Round starter advancement', () => {
  // These tests run an all-AI game and observe starterId changes between rounds.

  test('starterId moves to (winner + 3) % 4 after a round where a prize is taken', async ({ page }) => {
    // Run a real all-AI round. While round 1's trick is paused on its
    // click-wait, the play zone is still intact — capture determineTrick's
    // verdict for it, dismiss the wait, and check the starterId the engine
    // actually wrote against that verdict.
    await loadApp(page);
    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
    });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const result = await page.evaluate(() => new Promise(resolve => {
      const before = window.gameState.starterId;
      const iv = setInterval(() => {
        const r = window.gameState?.resolveInput;
        if (typeof r === 'function') {
          clearInterval(iv);
          const verdict = window.determineTrick(window.gameState.playZone);
          window.gameState.resolveInput = null;
          r();
          // Round 2 blocks on its own (never-dismissed) click-wait, so by
          // the time this fires, starterId holds round 1's assignment.
          setTimeout(() => resolve({
            before,
            winnerId: verdict.winnerId,
            prizeTaken: verdict.prizeCard !== null,
            after: window.gameState.starterId,
          }), 300);
        }
      }, 20);
    }));

    if (result.winnerId !== null && result.prizeTaken) {
      expect(result.after).toBe((result.winnerId + 3) % 4);
    } else {
      // No prize taken (tie, or winner with an empty prize pool) → same
      // leader again.
      expect(result.after).toBe(result.before);
    }
  });

  test('starterId is unchanged after a round with no winner (all tied)', async ({ page }) => {
    // Drive the REAL executeDeterminePhase against a hand-built 4-way tie
    // (no gameLoop running) and assert the engine leaves starterId alone.
    await loadApp(page);

    const result = await page.evaluate(async () => {
      window.SPEED = 0;
      const C = (s, r) => new window.Card(s, r);
      const players = [
        new window.Player(0, 'South', false, 'Easy'),
        new window.Player(1, 'West',  false, 'Easy'),
        new window.Player(2, 'North', false, 'Easy'),
        new window.Player(3, 'East',  false, 'Easy'),
      ];
      window.gameState = {
        players,
        activePlayerId: null,
        round: 1, phase: 'Determine', starterId: 2,
        playZone: [
          { card: C('♣', 7), player: players[0], section: 'Hidden' },
          { card: C('♦', 7), player: players[1], section: 'Hidden' },
          { card: C('♥', 7), player: players[2], section: 'Hidden' },
          { card: C('♠', 7), player: players[3], section: 'Hidden' },
        ],
        discards: [],
        isRunning: true, resolveInput: null,
      };
      window.renderPlayZone();
      const ack = setInterval(() => {
        const r = window.gameState.resolveInput;
        if (typeof r === 'function') { window.gameState.resolveInput = null; r(); }
      }, 5);
      await window.executeDeterminePhase();
      clearInterval(ack);
      return window.gameState.starterId;
    });
    expect(result).toBe(2); // unchanged — same player leads again
  });
});

// ── Integration: card count / round counter progression ─────────────────────

test.describe('Game progression invariants', () => {
  test('round counter increments by 1 after each trick resolves', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => {
      window.SPEED = 0; // run the loop at full speed — we only check counters
      void window.startGame({ humanId: -1 });
    });
    await page.waitForFunction(() => window.gameState?.isRunning);

    await page.evaluate(() => {
      window.__auto = setInterval(() => {
        const r = window.gameState?.resolveInput;
        if (typeof r === 'function') {
          window.gameState.resolveInput = null;
          try { r(); } catch (_) {}
        }
      }, 30);
    });

    await page.waitForFunction(() => window.gameState.round >= 3, null, { timeout: 60_000 });

    const round = await page.evaluate(() => {
      clearInterval(window.__auto);
      return window.gameState.round;
    });
    expect(round).toBeGreaterThanOrEqual(3);
  });

  test('each player loses exactly one card from their hand per round', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    // Snapshot before round 1 resolves. At normal speed the first AI play is
    // ~1.4s away (600ms reveal + 800ms play), so this lands pre-play; only
    // after the snapshot do we drop to SPEED=0 to finish the round fast.
    const before = await page.evaluate(() =>
      window.gameState.players.map(p => p.hiddenHand.length + p.shownHand.length));

    await page.evaluate(() => { window.SPEED = 0; });

    // Park at round 1's Determine click-wait: all four cards have been played
    // and nothing moves until the wait is dismissed, so the read is stable.
    // (Don't auto-dismiss — with SPEED=0, round 2's plays would race the read.)
    await page.waitForFunction(() =>
      window.gameState.phase === 'Determine' &&
      typeof window.gameState.resolveInput === 'function',
      null, { timeout: 30_000 });

    const after = await page.evaluate(() =>
      window.gameState.players.map(p => p.hiddenHand.length + p.shownHand.length));

    // Each player should have exactly 1 fewer card in hand after one round of
    // play (the played card is out of the hand; the prize goes to the
    // scoring zone, never back to a hand).
    before.forEach((count, i) => {
      expect(after[i]).toBe(count - 1);
    });
  });

  test('play zone is empty at the start of each new round', async ({ page }) => {
    // After each trick resolves, executeDeterminePhase clears playZone before
    // setting phase → Score. During the subsequent Reveal phase (~600ms AI
    // delay) the zone stays empty. We use waitForFunction — which polls
    // continuously — to catch this window rather than a 30ms setInterval, which
    // can miss a brief Reveal phase when the same tick is also busy dismissing
    // resolveInput.
    await loadApp(page);
    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
      window.__auto = setInterval(() => {
        const r = window.gameState?.resolveInput;
        if (typeof r === 'function') {
          window.gameState.resolveInput = null;
          try { r(); } catch (_) {}
        }
      }, 30);
    });

    // Wait until round ≥ 2 AND playZone is empty simultaneously, and capture
    // that observation so the assertion below is on real data (the truthy
    // object is what waitForFunction resolves with).
    const observed = await page.waitForFunction(
      () => window.gameState.round >= 2 && window.gameState.playZone.length === 0
        && { round: window.gameState.round, playZoneSize: window.gameState.playZone.length },
      null,
      { timeout: 30_000 }
    );
    const snapshot = await observed.jsonValue();

    await page.evaluate(() => clearInterval(window.__auto));

    expect(snapshot.playZoneSize).toBe(0);
    expect(snapshot.round).toBeGreaterThanOrEqual(2);
  });
});

// ── Persistence: resilience and cross-reload stats ───────────────────────────

test.describe('Persistence edge cases', () => {
  test('corrupted save in localStorage does not crash on load', async ({ page }) => {
    // Write garbage JSON to the save key before loading — the app should
    // start cleanly, not throw, and show the menu (not a broken state).
    await page.goto('/index.html');
    await page.evaluate(() => {
      try { localStorage.setItem('cardGameState', '{ this is not valid JSON !!'); } catch (_) {}
    });
    await page.reload();
    await page.waitForFunction(() => typeof window.Card === 'function');

    const menuVisible = await page.evaluate(() => {
      const el = document.getElementById('menu-screen');
      return el && el.style.display !== 'none';
    });
    expect(menuVisible).toBe(true);
    const running = await page.evaluate(() => window.gameState?.isRunning === true);
    expect(running).toBe(false);
  });

  test('stats persist across a full page reload', async ({ page }) => {
    await page.goto('/index.html');
    await page.evaluate(() => { try { localStorage.clear(); } catch (_) {} });
    await page.waitForFunction(() => typeof window.Card === 'function');

    // Seed one completed game.
    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[0].scoringZone.push(new window.Card('♣', 14));
      window.gameState.isRunning = false;
      window.endGame();
    });

    // Reload and check display.
    await page.reload();
    await page.waitForFunction(() => typeof window.Card === 'function');

    const text = await page.locator('#stats-display').innerText();
    expect(text).toMatch(/Games Played:\s*1/);
    expect(text).toMatch(/Human Wins:\s*1/);
    expect(text).toMatch(/High Score:\s*14/);
  });

  test('end-of-game clears the mid-game save so resume button is hidden afterwards', async ({ page }) => {
    await page.goto('/index.html');
    await page.evaluate(() => { try { localStorage.clear(); } catch (_) {} });
    await page.waitForFunction(() => typeof window.Card === 'function');

    await page.evaluate(() => {
      window.startGame();
      window.gameState.players[0].scoringZone.push(new window.Card('♦', 10));
      window.gameState.isRunning = false;
      window.endGame(); // should call clearSavedGame() internally
    });

    // Return to menu.
    await page.evaluate(() => window.showMenu());

    const display = await page.locator('#resume-btn').evaluate(el => el.style.display);
    // After a completed game there is no save to resume, so button must be hidden.
    expect(display).toBe('none');
  });

  test('abandoning an unfinished game does NOT increment games-played', async ({ page }) => {
    await page.goto('/index.html');
    await page.evaluate(() => { try { localStorage.clear(); } catch (_) {} });
    await page.waitForFunction(() => typeof window.Card === 'function');

    await page.evaluate(() => {
      window.startGame();
      // Abandon without finishing
      window.showMenu();
    });

    const stats = await page.evaluate(() => {
      const raw = localStorage.getItem('cardGameStats');
      return raw ? JSON.parse(raw) : null;
    });
    // Either no stats written at all, or played is still 0
    expect(stats === null || stats.played === 0).toBe(true);
  });
});
