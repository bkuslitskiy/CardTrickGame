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

  test('three-card value tie: the lone survivor wins the round but takes no prize', async ({ page }) => {
    // Canonical rule (clarified June 2026): the three tied cards are
    // discarded; the remaining player wins the round, discards their own
    // card, and takes no prize — nothing remains to take.
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' }, // 10
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' }, // 10
        { card: C('♥', 10), player: { id: 2 }, section: 'Hidden' }, // 10  three-way tie
        { card: C('♠',  4), player: { id: 3 }, section: 'Hidden' }, //  4  lone survivor — WINNER
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBe(3);
    expect(result.prizeCard).toBeNull();
  });

  test('two 2-way ties: all four cards eliminated, nobody wins', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 10), player: { id: 0 }, section: 'Hidden' }, // 10 ┐ tie
        { card: C('♦', 10), player: { id: 1 }, section: 'Hidden' }, // 10 ┘
        { card: C('♥',  6), player: { id: 2 }, section: 'Hidden' }, //  6 ┐ tie
        { card: C('♠',  6), player: { id: 3 }, section: 'Hidden' }, //  6 ┘
      ];
      return window.determineTrick(playZone);
    });
    expect(result.winnerId).toBeNull();
    expect(result.prizeCard).toBeNull();
    expect(result.valueTiedCards).toHaveLength(4);
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
  // Canonical rule: the leader moves ONE SEAT CLOCKWISE from the previous
  // leader every round — who won the trick, and whether a prize was taken,
  // are irrelevant. executeDeterminePhase must not touch starterId at all;
  // executeScorePhase owns the rotation.
  //
  // These drive the real engine phases against a hand-built gameState (no
  // gameLoop running, so there is nothing to race against) and assert on the
  // starterId the engine actually writes.
  test.beforeEach(async ({ page }) => { await loadApp(page); });

  // Installs a gameState with the given starterId and play zone, runs the
  // Determine phase to completion (auto-acking the click-wait), then runs the
  // Score phase. Returns starterId after each phase. SPEED=0 collapses the
  // animation delays.
  async function runRound(page, { starterId, cards }) {
    return page.evaluate(async ({ starterId, cards }) => {
      window.SPEED = 0;
      const players = [
        new window.Player(0, 'South', false, 'Easy'),
        new window.Player(1, 'West',  false, 'Easy'),
        new window.Player(2, 'North', false, 'Easy'),
        new window.Player(3, 'East',  false, 'Easy'),
      ];
      window.gameState = {
        players,
        activePlayerId: null,
        round: 1, phase: 'Determine', starterId,
        playZone: cards.map((c, i) => ({
          card: new window.Card(c.suit, c.rank),
          player: players[i],
          section: c.section || 'Hidden',
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
      const afterDetermine = window.gameState.starterId;
      window.executeScorePhase();
      return {
        afterDetermine,
        afterScore: window.gameState.starterId,
        round: window.gameState.round,
        scored: players.map(p => p.scoringZone.length),
        discards: window.gameState.discards.length,
      };
    }, { starterId, cards });
  }

  // One clear winner (the lone Ace) and a prize to take — the leader still
  // rotates off the previous LEADER, not off the winner.
  const suits = ['\u2663', '\u2666', '\u2665', '\u2660'];
  const loserRanks = [5, 7, 9];

  function oneWinnerAt(seat) {
    let k = 0;
    return [0, 1, 2, 3].map(id => ({
      suit: suits[id],
      rank: id === seat ? 14 : loserRanks[k++],
    }));
  }

  for (let starter = 0; starter < 4; starter++) {
    for (let winner = 0; winner < 4; winner++) {
      test(`leader ${starter}, winner ${winner} takes a prize \u2192 next leader is ${(starter + 1) % 4}`, async ({ page }) => {
        const r = await runRound(page, { starterId: starter, cards: oneWinnerAt(winner) });
        expect(r.afterDetermine).toBe(starter);        // Determine must not rotate
        expect(r.afterScore).toBe((starter + 1) % 4);  // Score rotates clockwise
        expect(r.scored[winner]).toBe(1);              // winner did take a prize
      });
    }
  }

  test('3-way-tie survivor wins WITHOUT a prize \u2192 leader still rotates clockwise', async ({ page }) => {
    // The survivor wins the round but takes nothing. Under the old rule the
    // same player led again; now the rotation is unconditional.
    const r = await runRound(page, {
      starterId: 1,
      cards: [
        { suit: '\u2663', rank: 10 }, // tied
        { suit: '\u2666', rank: 10 }, // tied
        { suit: '\u2665', rank: 10 }, // tied
        { suit: '\u2660', rank:  4 }, // survivor — wins, no prize
      ],
    });
    expect(r.afterDetermine).toBe(1);   // Determine must not rotate
    expect(r.afterScore).toBe(2);       // rotates anyway, despite no prize
    expect(r.scored[3]).toBe(0);        // winner took nothing
    expect(r.discards).toBe(4);         // all four cards discarded
  });

  test('no winner (4-way tie) \u2192 leader still rotates clockwise', async ({ page }) => {
    const r = await runRound(page, {
      starterId: 2,
      cards: [0, 1, 2, 3].map(i => ({ suit: suits[i], rank: 7 })),
    });
    expect(r.afterDetermine).toBe(2);   // Determine must not rotate
    expect(r.afterScore).toBe(3);       // rotates anyway, despite no winner
    expect(r.scored).toEqual([0, 0, 0, 0]);
  });

  test('winner with every prize candidate score-tied away \u2192 leader still rotates clockwise', async ({ page }) => {
    // A\u2665 Hidden (Value 14) wins. The candidates K\u2660 Hidden (Value 13, Score 13)
    // and K\u2666 Shown (Value 5, Score 13) did NOT tie in Value, so both survive
    // the value step — but they tie on Score, so both are discarded and the
    // winner takes no prize. The leader rotates all the same.
    const r = await runRound(page, {
      starterId: 3,
      cards: [
        { suit: '\u2665', rank: 14, section: 'Hidden' }, // Value 14 \u2192 winner
        { suit: '\u2660', rank: 13, section: 'Hidden' }, // Value 13, Score 13
        { suit: '\u2666', rank: 13, section: 'Shown'  }, // Value  5, Score 13
        { suit: '\u2663', rank:  3, section: 'Hidden' }, // Value  3, Score  3
      ],
    });
    expect(r.afterDetermine).toBe(3);
    expect(r.afterScore).toBe(0);       // wraps 3 \u2192 0
    expect(r.scored[0]).toBe(1);        // winner (seat 0) took the surviving 3\u2663
  });

  test('leadership cycles through all four seats over four rounds', async ({ page }) => {
    const seats = await page.evaluate(() => {
      window.gameState = {
        players: [], round: 1, phase: 'Score', starterId: 0,
        playZone: [], discards: [], isRunning: true, resolveInput: null,
      };
      const seen = [window.gameState.starterId];
      for (let i = 0; i < 4; i++) {
        window.gameState.phase = 'Score';
        window.executeScorePhase();
        seen.push(window.gameState.starterId);
      }
      return { seen, round: window.gameState.round };
    });
    expect(seats.seen).toEqual([0, 1, 2, 3, 0]);
    expect(seats.round).toBe(5);
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

  test('3-card tie: the three tied cards land in valueTiedCards, survivor wins', async ({ page }) => {
    // Canonical rule: only the tied cards are cut; the lone survivor wins
    // the round (their card is the winner card, not a tie casualty) and
    // takes no prize.
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
    expect(result.winnerId).toBe(3);
    expect(result.prizeCard).toBeNull();
    expect(result.valueTiedCards).toHaveLength(3);
    expect(result.valueTiedCards.map(c => c.rank)).toEqual([10, 10, 10]);
    expect(result.scoreTiedCards).toHaveLength(0);
  });

  test('4-way tie: ALL four cards land in valueTiedCards, nobody wins', async ({ page }) => {
    const result = await page.evaluate(() => {
      const C = (s, r) => new window.Card(s, r);
      const playZone = [
        { card: C('♣', 9), player: { id: 0 }, section: 'Hidden' },
        { card: C('♦', 9), player: { id: 1 }, section: 'Hidden' },
        { card: C('♥', 9), player: { id: 2 }, section: 'Hidden' },
        { card: C('♠', 9), player: { id: 3 }, section: 'Hidden' },
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
