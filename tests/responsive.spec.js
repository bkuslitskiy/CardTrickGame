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
