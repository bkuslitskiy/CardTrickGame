// Responsive layout regression tests.
//
// The layout used to be authored as fixed pixel sizes inside four media
// queries, so any viewport between the authored tiers was untested — and one
// of them (phones in landscape) shipped with `display: none` on the human's
// own hand, making the game unplayable. These tests pin the invariants that
// have to hold at EVERY viewport, so a future tweak to one breakpoint cannot
// quietly break another device.
//
// Rather than adding Playwright projects (which would multiply the whole
// 100-test suite by the number of devices), this file drives the viewport
// itself. Same coverage, roughly a quarter of the CI time.

const { test, expect } = require('@playwright/test');
const { loadApp, startGame } = require('./helpers');

const VIEWPORTS = [
  { name: 'desktop 1280x800',        w: 1280, h: 800 },
  { name: 'laptop 1024x640',         w: 1024, h: 640 },
  { name: 'tablet portrait 820x1180',w: 820,  h: 1180 },
  { name: 'tablet landscape 1180x820', w: 1180, h: 820 },
  { name: 'phone portrait 390x844',  w: 390,  h: 844 },
  { name: 'phone portrait 360x640',  w: 360,  h: 640 },
  { name: 'phone landscape 844x390', w: 844,  h: 390 },
  { name: 'small phone 320x568',     w: 320,  h: 568 },
];

/** Geometry of every element the board layout has to keep disjoint / on-screen. */
const TRACKED = [
  'play-zone', 'prompt-overlay',
  'info-0', 'info-1', 'info-2', 'info-3',
  'hidden-0', 'shown-0', 'hidden-1', 'shown-1',
  'hidden-2', 'shown-2', 'hidden-3', 'shown-3',
];

async function measure(page) {
  return page.evaluate((ids) => {
    const rect = (el) => {
      const b = el.getBoundingClientRect();
      return { x: b.x, y: b.y, w: b.width, h: b.height, right: b.right, bottom: b.bottom };
    };
    const boxes = {};
    for (const id of ids) {
      const el = document.getElementById(id);
      if (!el) continue;
      const b = rect(el);
      // Skip zero-size boxes: an empty hand legitimately collapses to nothing.
      if (b.w === 0 || b.h === 0) continue;
      boxes[id] = b;
    }

    // A card is "clipped" when an ancestor's overflow cuts it off — i.e. it
    // falls outside its own hand container's box.
    const clipped = {};
    for (const id of ids) {
      const el = document.getElementById(id);
      if (!el || !el.classList.contains('hand-container')) continue;
      const pb = el.getBoundingClientRect();
      clipped[id] = [...el.querySelectorAll('.card')].filter((c) => {
        const b = c.getBoundingClientRect();
        return b.top < pb.top - 1 || b.bottom > pb.bottom + 1
            || b.left < pb.left - 1 || b.right > pb.right + 1;
      }).length;
    }

    const doc = document.documentElement;
    return {
      vw: window.innerWidth,
      vh: window.innerHeight,
      scrollsX: doc.scrollWidth > doc.clientWidth,
      scrollsY: doc.scrollHeight > doc.clientHeight,
      boxes,
      clipped,
      humanHandDisplay: getComputedStyle(document.getElementById('hidden-0')).display,
      cardW: parseFloat(getComputedStyle(document.querySelector('#hidden-0 .card')).width),
    };
  }, TRACKED);
}

const intersects = (a, b) =>
  a.x < b.right && b.x < a.right && a.y < b.bottom && b.y < a.bottom;

for (const v of VIEWPORTS) {
  test.describe(`@ ${v.name}`, () => {
    test.beforeEach(async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await loadApp(page);
      await startGame(page);
      // Round 1 with full 13-card hands is the worst case for fitting.
      await page.waitForFunction(() => typeof window.gameState?.resolveInput === 'function');
    });

    test('the human can see and play their own hand', async ({ page }) => {
      const m = await measure(page);
      // The regression that motivated this file: a media query hid the South
      // hand row, so on a phone in landscape the player had no cards at all.
      expect(m.humanHandDisplay).not.toBe('none');
      expect(m.boxes['hidden-0']).toBeTruthy();

      const cards = page.locator('#hidden-0 .card');
      await expect(cards).toHaveCount(13);
      // Every card must be genuinely hittable, not just present in the DOM.
      await expect(cards.first()).toBeVisible();
      await expect(cards.last()).toBeVisible();
    });

    test('nothing overflows the viewport and the page never scrolls', async ({ page }) => {
      const m = await measure(page);
      expect(m.scrollsX).toBe(false);
      expect(m.scrollsY).toBe(false);

      const escaped = Object.entries(m.boxes)
        .filter(([, b]) => b.x < -1 || b.right > m.vw + 1 || b.y < -1 || b.bottom > m.vh + 1)
        .map(([id]) => id);
      expect(escaped).toEqual([]);
    });

    test('no hand is clipped by its container', async ({ page }) => {
      const m = await measure(page);
      // Shown hands especially: they are public information, so silently
      // cutting them off changes the game, not just the picture.
      const losses = Object.entries(m.clipped).filter(([, n]) => n > 0);
      expect(losses).toEqual([]);
    });

    test('the prompt never lands on top of a hand', async ({ page }) => {
      const m = await measure(page);
      const prompt = m.boxes['prompt-overlay'];
      expect(prompt).toBeTruthy();   // the reveal prompt is showing

      const hit = Object.entries(m.boxes)
        .filter(([id]) => id.startsWith('hidden-') || id.startsWith('shown-') || id.startsWith('info-'))
        .filter(([, b]) => intersects(prompt, b))
        .map(([id]) => id);
      expect(hit).toEqual([]);
    });

    test('the play zone does not overlap any hand', async ({ page }) => {
      const m = await measure(page);
      const zone = m.boxes['play-zone'];
      const hit = Object.entries(m.boxes)
        .filter(([id]) => id.startsWith('hidden-') || id.startsWith('shown-'))
        .filter(([, b]) => intersects(zone, b))
        .map(([id]) => id);
      expect(hit).toEqual([]);
    });

    test('the play zone stays centred as cards are revealed', async ({ page }) => {
      // The east/west grid tracks are content-sized, so an asymmetric reveal
      // (East has a shown card, West does not) used to slide the whole play
      // zone sideways mid-game.
      const centreOffset = async () => page.evaluate(() => {
        const z = document.getElementById('play-zone').getBoundingClientRect();
        return Math.abs((z.left + z.right) / 2 - window.innerWidth / 2);
      });

      const before = await centreOffset();
      await page.evaluate(() => {
        // Reveal two cards for East and none for West — the worst asymmetry.
        const gs = window.gameState;
        gs.players[3].shownHand = gs.players[3].hiddenHand.splice(0, 2);
        window.renderAll();
      });
      const after = await centreOffset();

      // The invariant that matters is that the zone does not MOVE. A constant
      // ~2.5px bias remains from "West" and "East" rendering to slightly
      // different badge widths, which is imperceptible and not drift.
      expect(Math.abs(after - before)).toBeLessThanOrEqual(1);
      expect(after).toBeLessThanOrEqual(6);
    });

    test('cards keep a usable hit target even with a full hand', async ({ page }) => {
      // Overlapping cards expose only `stride` px each. With 13 cards this is
      // the tightest the layout ever gets; it must stay wide enough to hit.
      const stride = await page.evaluate(() => {
        const cards = [...document.querySelectorAll('#hidden-0 .card')];
        return cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left;
      });
      expect(stride).toBeGreaterThanOrEqual(12);
    });
  });
}

test.describe('Determine-phase decorations scale with the card', () => {
  // gameplay.spec.js covers that decorateCard attaches the SVGs. This covers
  // their GEOMETRY: they are sized in em against the card's own width, so a
  // change to the card-size expression (or a slip back to fixed pixels) must
  // not leave them tiny on a phone or overflowing on a desktop.

  /** Rig a resolved trick: K♦ wins, 9♠ is the prize, two 7s tie away. */
  async function rigDecoratedTrick(page) {
    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
      const gs = window.gameState;
      const C = (s, r) => new window.Card(s, r);
      const cards = [C('♦', 13), C('♠', 9), C('♥', 7), C('♣', 7)];
      gs.playZone = cards.map((card, i) => ({ card, player: gs.players[i], section: 'Hidden' }));
      gs.isRunning = false;
      window.renderAll();
      window.decorateCard(cards[2], 'cut');
      window.decorateCard(cards[3], 'cut');
      window.decorateCard(cards[0], 'crown');
      window.decorateCard(cards[1], 'coins');
    });
    // The entrance keyframes animate scale(), and getBoundingClientRect()
    // reports the TRANSFORMED box — measuring early reads ~30% of the real
    // size. Wait for them to settle.
    await page.waitForTimeout(800);
  }

  const readRatios = (page) => page.evaluate(() => {
    const box = (sel) => {
      const el = document.querySelector(sel);
      if (!el) return null;
      const b = el.getBoundingClientRect();
      return { w: b.width, h: b.height, top: b.top, left: b.left, right: b.right, bottom: b.bottom };
    };
    const crownCard = box('#play-slot-0 .card');
    const coinsCard = box('#play-slot-1 .card');
    const crown = box('#play-slot-0 .card-crown');
    const coins = box('#play-slot-1 .card-coins');
    const inside = (d, c) => d && c
      && d.top >= c.top - 0.5 && d.bottom <= c.bottom + 0.5
      && d.left >= c.left - 0.5 && d.right <= c.right + 0.5;
    return {
      cardW: crownCard.w,
      crownRatio: crown && +(crown.w / crownCard.w).toFixed(3),
      coinsRatio: coins && +(coins.w / coinsCard.w).toFixed(3),
      crownInside: inside(crown, crownCard),
      coinsInside: inside(coins, coinsCard),
      cutCount: document.querySelectorAll('.play-slot .card.cut').length,
    };
  });

  for (const v of [VIEWPORTS[0], VIEWPORTS[4], VIEWPORTS[6], VIEWPORTS[7]]) {
    test(`crown, coins and cut all render @ ${v.name}`, async ({ page }) => {
      await page.setViewportSize({ width: v.w, height: v.h });
      await loadApp(page);
      await rigDecoratedTrick(page);

      const r = await readRatios(page);
      expect(r.crownRatio, 'crown rendered').not.toBeNull();
      expect(r.coinsRatio, 'coins rendered').not.toBeNull();
      expect(r.cutCount).toBe(2);
      // .card has overflow:hidden — a decoration that escapes its frame gets
      // silently sliced (this is what once cut the crown's top half off).
      expect(r.crownInside, 'crown inside the card frame').toBe(true);
      expect(r.coinsInside, 'coins inside the card frame').toBe(true);
    });
  }

  test('decoration size stays proportional to the card across viewports', async ({ page }) => {
    const seen = [];
    for (const v of [VIEWPORTS[0], VIEWPORTS[4], VIEWPORTS[6]]) {
      await page.setViewportSize({ width: v.w, height: v.h });
      await loadApp(page);
      await rigDecoratedTrick(page);
      seen.push(await readRatios(page));
    }

    // Card widths genuinely differ across these viewports...
    const widths = seen.map(s => s.cardW);
    expect(Math.max(...widths) - Math.min(...widths)).toBeGreaterThan(10);

    // ...but the decorations occupy the same fraction of the card at each.
    // Fixed-pixel decorations would fail here: 20px is 0.34 of a 59px card
    // and 0.69 of a 29px one.
    for (const key of ['crownRatio', 'coinsRatio']) {
      const ratios = seen.map(s => s[key]);
      expect(Math.max(...ratios) - Math.min(...ratios),
        `${key} varies across viewports`).toBeLessThan(0.02);
    }
  });
});

test.describe('Reduced motion', () => {
  test('entrance and flip animations are suppressed', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await loadApp(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    expect(await page.evaluate(() =>
      matchMedia('(prefers-reduced-motion: reduce)').matches)).toBe(true);

    await page.evaluate(() => {
      window.SPEED = 0;
      void window.startGame({ humanId: -1 });
      const gs = window.gameState;
      const k = new window.Card('♦', 13);
      const q = new window.Card('♠', 9);
      gs.playZone = [
        { card: k, player: gs.players[0], section: 'Hidden' },
        { card: q, player: gs.players[1], section: 'Hidden' },
      ];
      gs.isRunning = false;
      window.renderAll();
      window.decorateCard(k, 'crown');
      window.decorateCard(q, 'coins');
    });

    const anims = await page.evaluate(() => {
      const name = (sel) => {
        const el = document.querySelector(sel);
        return el ? getComputedStyle(el).animationName : 'missing';
      };
      return { crown: name('.card-crown'), coins: name('.card-coins') };
    });
    // The decorations still render — they just arrive without the bounce.
    expect(anims.crown).toBe('none');
    expect(anims.coins).toBe('none');
  });
});

test.describe('Fluid sizing', () => {
  test('card size tracks the viewport instead of snapping at breakpoints', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await loadApp(page);
    await startGame(page);

    const widths = [];
    for (const h of [800, 760, 720, 680, 640]) {
      await page.setViewportSize({ width: 1280, height: h });
      widths.push(await page.evaluate(() =>
        parseFloat(getComputedStyle(document.querySelector('#hidden-0 .card')).width)));
    }
    // Strictly decreasing: proof the size is a continuous function of the
    // viewport, not a step function with untested gaps between the steps.
    for (let i = 1; i < widths.length; i++) {
      expect(widths[i]).toBeLessThan(widths[i - 1]);
    }
  });

  test('a short hand spreads out again as cards are played', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loadApp(page);
    await startGame(page);

    const strideOf = () => page.evaluate(() => {
      const cards = [...document.querySelectorAll('#hidden-0 .card')];
      return cards[1].getBoundingClientRect().left - cards[0].getBoundingClientRect().left;
    });

    const full = await strideOf();
    await page.evaluate(() => {
      window.gameState.players[0].hiddenHand = window.gameState.players[0].hiddenHand.slice(0, 4);
      window.renderAll();
    });
    const short = await strideOf();

    // Four cards get more room each than thirteen do — the stride is computed
    // from the hand size, not fixed per breakpoint.
    expect(short).toBeGreaterThan(full);
  });
});
