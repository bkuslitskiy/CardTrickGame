// Engine / pure-logic tests.
//
// These exercise the data classes (Card, Deck, Player) and the trick-
// resolution algorithm in isolation. They run in the browser context, but
// touch no DOM — they're effectively unit tests delivered through Playwright
// so the harness is uniform with the UI specs.
//
// When engine.js is rewritten, the contract these tests describe is the
// minimum surface the rewrite must preserve.

const { test, expect } = require('@playwright/test');
const { loadApp, SUITS } = require('./helpers');

test.describe('Card scoring tables', () => {
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('Hidden values map to face value 2..14', async ({ page }) => {
    const got = await page.evaluate(() => {
      const out = {};
      for (let r = 2; r <= 14; r++) out[r] = new window.Card('♣', r).hiddenScore;
      return out;
    });
    expect(got).toEqual({
      2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7, 8: 8, 9: 9, 10: 10,
      11: 11, 12: 12, 13: 13, 14: 14,
    });
  });

  test('Shown values: 2-10 get +2, J=13, Q=14, K=5, A=10', async ({ page }) => {
    const got = await page.evaluate(() => {
      const out = {};
      for (let r = 2; r <= 14; r++) out[r] = new window.Card('♠', r).shownScore;
      return out;
    });
    expect(got).toEqual({
      2: 4, 3: 5, 4: 6, 5: 7, 6: 8, 7: 9, 8: 10, 9: 11, 10: 12,
      11: 13, 12: 14, 13: 5, 14: 10,
    });
  });

  test('faceValue always equals the rank (2..14)', async ({ page }) => {
    const got = await page.evaluate(() => {
      const ranks = [];
      for (let r = 2; r <= 14; r++) ranks.push(new window.Card('♥', r).faceValue);
      return ranks;
    });
    expect(got).toEqual([2,3,4,5,6,7,8,9,10,11,12,13,14]);
  });

  test('colorClass: red for hearts/diamonds, black for clubs/spades', async ({ page }) => {
    const got = await page.evaluate((suits) =>
      suits.map(s => new window.Card(s, 7).colorClass), SUITS);
    expect(got).toEqual(['black', 'red', 'red', 'black']);
  });
});

test.describe('Deck', () => {
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('contains 52 unique (suit, rank) pairs', async ({ page }) => {
    const { count, uniques } = await page.evaluate(() => {
      const d = new window.Deck();
      const keys = d.cards.map(c => `${c.suit}-${c.rank}`);
      return { count: keys.length, uniques: new Set(keys).size };
    });
    expect(count).toBe(52);
    expect(uniques).toBe(52);
  });

  test('shuffle randomises order (two decks rarely match)', async ({ page }) => {
    // Statistical check: with 52! permutations the chance of two shuffles
    // matching the same prefix of length 10 is astronomically small.
    const samePrefix = await page.evaluate(() => {
      const a = new window.Deck().cards.slice(0, 10).map(c => `${c.suit}${c.rank}`).join(',');
      const b = new window.Deck().cards.slice(0, 10).map(c => `${c.suit}${c.rank}`).join(',');
      return a === b;
    });
    expect(samePrefix).toBe(false);
  });

  test('deal(13) removes 13 cards from the deck', async ({ page }) => {
    const { handSize, remaining } = await page.evaluate(() => {
      const d = new window.Deck();
      const h = d.deal(13);
      return { handSize: h.length, remaining: d.cards.length };
    });
    expect(handSize).toBe(13);
    expect(remaining).toBe(39);
  });
});

test.describe('Player', () => {
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('score sums faceValue of scoringZone', async ({ page }) => {
    const score = await page.evaluate(() => {
      const p = new window.Player(0, 'South', true);
      p.scoringZone.push(new window.Card('♣', 14)); // A = 14
      p.scoringZone.push(new window.Card('♦', 11)); // J = 11
      p.scoringZone.push(new window.Card('♥', 5));  // 5
      return p.score;
    });
    expect(score).toBe(30);
  });

  test('removeCard returns "Hidden" or "Shown" by section', async ({ page }) => {
    const result = await page.evaluate(() => {
      const p = new window.Player(0, 'South', true);
      const hCard = new window.Card('♣', 7);
      const sCard = new window.Card('♥', 11);
      p.hiddenHand.push(hCard);
      p.shownHand.push(sCard);
      return {
        h: p.removeCard(hCard),
        s: p.removeCard(sCard),
        missing: p.removeCard(new window.Card('♠', 2)),
        hLeft: p.hiddenHand.length,
        sLeft: p.shownHand.length,
      };
    });
    expect(result).toEqual({ h: 'Hidden', s: 'Shown', missing: null, hLeft: 0, sLeft: 0 });
  });

  test('hasHidden reflects hiddenHand size', async ({ page }) => {
    const result = await page.evaluate(() => {
      const p = new window.Player(1, 'West', false);
      const out = [p.hasHidden];
      p.hiddenHand.push(new window.Card('♣', 2));
      out.push(p.hasHidden);
      p.hiddenHand.pop();
      out.push(p.hasHidden);
      return out;
    });
    expect(result).toEqual([false, true, false]);
  });
});

test.describe('Trick resolution', () => {
  // We don't have a free function to call — the resolver is defined as a
  // local closure inside executeDeterminePhase. Rig the playZone manually
  // and assert on the final state once Determine completes. The rewrite
  // SHOULD expose this as a callable pure function; tests for that are
  // included below and will start passing once it exists.

  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('public determineTrick(playZone) helper exists', async ({ page }) => {
    // Spec: expose a side-effect-free function for the algorithm so it
    // can be unit-tested. Until the rewrite adds it, this test fails.
    const exists = await page.evaluate(() =>
      typeof window.determineTrick === 'function');
    expect(exists, 'engine.js should expose window.determineTrick(playZone) -> { winnerId, prizeCard }').toBe(true);
  });

  test('clear winner: highest unique Value wins, highest unique Score is prize', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣',  9), player: { id: 0 }, section: 'Hidden' }, //  9
        { card: C('♦', 13), player: { id: 1 }, section: 'Hidden' }, // 13 -- winner
        { card: C('♥',  7), player: { id: 2 }, section: 'Shown'  }, //  9
        { card: C('♠',  3), player: { id: 3 }, section: 'Hidden' }, //  3
      ];
      return window.determineTrick(playZone);
    });
    // Winner is player 1 (K Hidden, value 13).
    // Survivors after value-tie discard: only the 13 and the 3 (the two 9s tie and are discarded).
    // Prize pool excludes winner: just the 3 of spades.
    expect(result.winnerId).toBe(1);
    expect(result.prizeCard?.rank).toBe(3);
    expect(result.prizeCard?.suit).toBe('♠');
  });

  test('two-card value tie: tied cards discarded, highest remaining wins', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' }, // 10
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' }, // 10 -- tied with above
        { card: C('♥',  8), player: { id: 2 }, section: 'Hidden' }, //  8 -- winner after discard
        { card: C('♠',  3), player: { id: 3 }, section: 'Hidden' }, //  3
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBe(2);
    expect(result.prizeCard?.rank).toBe(3);
  });

  test('three-card value tie resolution', async ({ page }) => {
    // Basic rules.txt: "If three cards tie, all cards are discarded and
    // nobody wins." Note: the original Basic rules also has a contradictory
    // line about the 4th card winning with no prize. Pick ONE behaviour and
    // assert it. This test encodes "all cards discarded, no winner". If you
    // intend the alternate rule, flip the expectation.
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' }, // 10
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' }, // 10
        { card: C('♥', 10), player: { id: 2 }, section: 'Hidden' }, // 10  three-way tie
        { card: C('♠',  4), player: { id: 3 }, section: 'Hidden' }, //  4  lone survivor
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBeNull();
    expect(result.prizeCard).toBeNull();
  });

  test('four-card value tie: nobody wins, no prize', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 7), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 7), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥', 7), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠', 7), player: { id: 3 }, section: 'Hidden' },
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBeNull();
    expect(result.prizeCard).toBeNull();
  });

  test('prize Score tie: tied prize candidates discarded, next-highest taken', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      // Values:  Hidden 14, Hidden 12, Hidden 12, Hidden 4
      // Player 0 wins (Ace from Hidden). Prize pool faceValue: 12, 12, 4.
      // 12s tie -> discarded -> prize = 4.
      const playZone = [
        { card: C('♣', 14), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 12), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥', 12), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠',  4), player: { id: 3 }, section: 'Hidden' },
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBe(0);
    expect(result.prizeCard?.rank).toBe(4);
  });

  test('Hidden vs Shown values interact correctly: K(Shown)=5 loses to 6(Hidden)=6', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 13), player: { id: 0 }, section: 'Shown'  }, // K Shown =  5
        { card: C('♦',  6), player: { id: 1 }, section: 'Hidden' }, // 6 Hidden=  6  WINNER
        { card: C('♥',  2), player: { id: 2 }, section: 'Hidden' }, // 2 Hidden=  2
        { card: C('♠',  3), player: { id: 3 }, section: 'Hidden' }, // 3 Hidden=  3
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBe(1);
    expect(result.prizeCard?.rank).toBe(13); // K is highest faceValue in prize pool
  });
});

test.describe('Round starter rotation', () => {
  // These drive the real executeDeterminePhase against a hand-built gameState
  // (no gameLoop running, so there is nothing to race against) and assert on
  // the starterId the engine actually writes.
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  // Builds a gameState where `winnerSeat` plays the lone Ace, runs the
  // Determine phase to completion (auto-acking the click-wait), and returns
  // the resulting starterId. SPEED=0 collapses the animation delays.
  async function runDetermineWithWinner(page, winnerSeat) {
    return page.evaluate(async (seat) => {
      window.SPEED = 0;
      const C = (s, r) => new window.Card(s, r);
      const players = [
        new window.Player(0, 'South', false, 'Easy'),
        new window.Player(1, 'West',  false, 'Easy'),
        new window.Player(2, 'North', false, 'Easy'),
        new window.Player(3, 'East',  false, 'Easy'),
      ];
      const suits = ['♣', '♦', '♥', '♠'];
      const loserRanks = [5, 7, 9];
      let k = 0;
      window.gameState = {
        players,
        activePlayerId: null,
        round: 1, phase: 'Determine', starterId: 0,
        playZone: players.map(p => ({
          card: C(suits[p.id], p.id === seat ? 14 : loserRanks[k++]),
          player: p, section: 'Hidden',
        })),
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
    }, winnerSeat);
  }

  for (let winner = 0; winner < 4; winner++) {
    test(`winner at seat ${winner} → engine sets starterId to ${(winner + 3) % 4}`, async ({ page }) => {
      const starterId = await runDetermineWithWinner(page, winner);
      expect(starterId).toBe((winner + 3) % 4);
    });
  }

  test('no winner (4-way tie) → engine leaves starterId unchanged', async ({ page }) => {
    const starterId = await page.evaluate(async () => {
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
        playZone: players.map((p, i) => ({
          card: C(['♣', '♦', '♥', '♠'][i], 7), player: p, section: 'Hidden',
        })),
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
    expect(starterId).toBe(2);
  });
});

test.describe('Deterministic RNG (setSeed)', () => {
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('setSeed(n) makes two shuffles identical; setSeed(null) restores randomness', async ({ page }) => {
    const result = await page.evaluate(() => {
      const order = () => new window.Deck().cards.map(c => `${c.suit}${c.rank}`).join(',');
      window.setSeed(123);
      const a = order();
      window.setSeed(123);
      const b = order();
      window.setSeed(null);
      const c = order();
      const d = order();
      return { seededMatch: a === b, unseededMatch: c === d };
    });
    expect(result.seededMatch).toBe(true);
    expect(result.unseededMatch).toBe(false);
  });
});

test.describe('determineTrick — visual-effect return fields', () => {
  // determineTrick was extended to surface the cards eliminated by value
  // ties and by prize score ties, so the UI can "cut" them visually. Tests
  // below pin that contract.
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('2-card value tie: valueTiedCards has the two tied cards', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥',  7), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠',  3), player: { id: 3 }, section: 'Hidden' },
      ];
      const r = window.determineTrick(playZone);
      return {
        winnerId: r.winnerId,
        valueTiedRanks: r.valueTiedCards.map(c => c.rank).sort(),
        scoreTiedCount: r.scoreTiedCards.length,
      };
    });
    expect(result.winnerId).toBe(2);                  // the 7 wins after the 10s are removed
    expect(result.valueTiedRanks).toEqual([10, 10]);
    expect(result.scoreTiedCount).toBe(0);
  });

  test('3-card void tie: ALL four cards land in valueTiedCards', async ({ page }) => {
    // Per Basic rules: "If three cards tie, all cards are discarded and
    // nobody wins." The UI cuts every card in the play zone.
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥', 10), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠',  4), player: { id: 3 }, section: 'Hidden' },
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBeNull();
    expect(result.prizeCard).toBeNull();
    expect(result.valueTiedCards).toHaveLength(4);
    expect(result.scoreTiedCards).toHaveLength(0);
  });

  test('prize score tie: scoreTiedCards has the tied prize candidates', async ({ page }) => {
    // K from Hidden (value 13) and K from Shown (value 5) survive value tie
    // step, then collide on score (13 each) in the prize pool. The Ace
    // wins on Value; both Ks should appear in scoreTiedCards.
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 14), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 13), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥', 13), player: { id: 2 }, section: 'Shown'  },
        { card: C('♠',  4), player: { id: 3 }, section: 'Hidden' },
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBe(0);
    expect(result.prizeCard?.rank).toBe(4);
    expect(result.scoreTiedCards).toHaveLength(2);
    expect(result.scoreTiedCards.map(c => c.rank).sort()).toEqual([13, 13]);
  });

  test('clean trick (no ties): both tied arrays are empty', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 14), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦',  7), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥',  5), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠',  3), player: { id: 3 }, section: 'Hidden' },
      ];
      return window.determineTrick(playZone);
    });
    expect(result.valueTiedCards).toHaveLength(0);
    expect(result.scoreTiedCards).toHaveLength(0);
  });
});

test.describe('window globals', () => {
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  test('window.gameLoop is exposed for tests to drive directly', async ({ page }) => {
    const ok = await page.evaluate(() => typeof window.gameLoop === 'function');
    expect(ok).toBe(true);
  });

  test('window.decorateCard is exposed', async ({ page }) => {
    const ok = await page.evaluate(() => typeof window.decorateCard === 'function');
    expect(ok).toBe(true);
  });
});
