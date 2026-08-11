// Touch input coverage.
//
// The desktop card-play path uses HTML5 drag-and-drop (dragstart/drop), which
// browsers never fire for touch input — so on a phone dragging silently did
// nothing while the prompt told the player to drag. attachPointerDrag() in
// ui.js adds the equivalent gesture on Pointer Events for coarse pointers.
//
// These tests drive real PointerEvents rather than Playwright's mouse helpers,
// because the branch under test keys off event.pointerType.

const { test, expect } = require('@playwright/test');
const { loadApp, startGame } = require('./helpers');

/** Centre of an element, in viewport coordinates. */
async function centreOf(page, selector) {
  return page.evaluate((sel) => {
    const b = document.querySelector(sel).getBoundingClientRect();
    return { x: b.left + b.width / 2, y: b.top + b.height / 2 };
  }, selector);
}

/**
 * Dispatch a pointer drag from `selector` to `to`.
 * Two moves: one short (inside the slop threshold) and one to the target, so
 * the slop logic is exercised rather than skipped.
 */
async function pointerDrag(page, selector, to, pointerType) {
  const from = await centreOf(page, selector);
  const send = (type, x, y) => page.dispatchEvent(selector, type, {
    pointerType, pointerId: 1, isPrimary: true, bubbles: true,
    clientX: x, clientY: y,
  });
  await send('pointerdown', from.x, from.y);
  await send('pointermove', from.x + 2, from.y + 2);   // under DRAG_SLOP
  await send('pointermove', to.x, to.y);
  await send('pointerup', to.x, to.y);
}

/**
 * Get to the point where the human is being asked to play a card: resolve the
 * Reveal phase first by picking West, same as gameplay.spec.js does.
 */
async function reachCardPrompt(page) {
  await page.waitForFunction(() =>
    document.querySelectorAll('.selectable-player').length >= 3);
  await page.locator('#info-1').click();
  await expect(page.locator('#prompt-overlay')).toContainText(/select a card/i, {
    timeout: 10_000,
  });
}

test.describe('Touch drag to play', () => {
  test.use({ hasTouch: true, isMobile: true, viewport: { width: 390, height: 844 } });

  test('the play prompt says "Tap", not "Click", on a coarse pointer', async ({ page }) => {
    await loadApp(page);
    // Guard the premise: if the emulated context does not actually report a
    // coarse pointer, this test would pass for the wrong reason.
    expect(await page.evaluate(() => matchMedia('(pointer: coarse)').matches)).toBe(true);

    await startGame(page);
    await reachCardPrompt(page);
    await expect(page.locator('#prompt-overlay')).toContainText(/tap or drag/i);
  });

  test('dragging a card onto the play zone with touch plays it', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    const before = await page.evaluate(() => window.gameState.players[0].hiddenHand.length);
    const zone = await centreOf(page, '#play-zone');
    await pointerDrag(page, '#hidden-0 .card', zone, 'touch');

    // The card left the hand and landed in the play zone.
    await page.waitForFunction(
      (n) => window.gameState.players[0].hiddenHand.length === n - 1,
      before, { timeout: 5_000 });
    const played = await page.evaluate(() =>
      window.gameState.playZone.some(p => p.player.id === 0));
    expect(played).toBe(true);
  });

  test('a touch drag that ends outside the play zone does not play the card', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    const before = await page.evaluate(() => window.gameState.players[0].hiddenHand.length);
    // Drop on the West seat instead — a miss, not a play.
    const elsewhere = await centreOf(page, '#info-1');
    await pointerDrag(page, '#hidden-0 .card', elsewhere, 'touch');

    await page.waitForTimeout(300);
    const after = await page.evaluate(() => window.gameState.players[0].hiddenHand.length);
    expect(after).toBe(before);
    expect(await page.evaluate(() => window.gameState.playZone.length)).toBe(0);
  });

  test('a tap (press with no movement) still plays the card', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    const before = await page.evaluate(() => window.gameState.players[0].hiddenHand.length);
    // The drag path must leave a stationary press alone so the click handler
    // gets it — otherwise adding drag support would have broken tapping.
    // .last() is the card on top of the fan; see the strip test below for the
    // overlapped case.
    await page.locator('#hidden-0 .card').last().tap();

    await page.waitForFunction(
      (n) => window.gameState.players[0].hiddenHand.length === n - 1,
      before, { timeout: 5_000 });
  });

  test('tapping the exposed strip of an overlapped card plays that card', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    // In a fanned hand every card except the last is covered by its
    // neighbour, so only a sliver on its left is hittable. That sliver is the
    // whole reason the stride has a minimum — this asserts the sliver really
    // does belong to the card it looks like it belongs to.
    const target = await page.evaluate(() => {
      const c = window.gameState.players[0].hiddenHand[5];  // a middle card
      return { rank: c.rank, suit: c.suit };
    });

    const point = await page.evaluate(() => {
      const cards = [...document.querySelectorAll('#hidden-0 .card')];
      const a = cards[5].getBoundingClientRect();
      const stride = cards[6].getBoundingClientRect().left - a.left;
      return { x: a.left + stride / 2, y: a.top + a.height / 2 };
    });
    await page.touchscreen.tap(point.x, point.y);

    await page.waitForFunction(() => window.gameState.playZone.length > 0,
      null, { timeout: 5_000 });
    const played = await page.evaluate(() => {
      const c = window.gameState.playZone[0].card;
      return { rank: c.rank, suit: c.suit };
    });
    expect(played).toEqual(target);
  });

  test('the play zone highlights while a card is dragged over it', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    // Asserted mid-gesture, so this fails outright if the pointer handler is
    // not wired up — unlike the negative tests below, which would pass for a
    // card that simply never moved.
    const from = await centreOf(page, '#hidden-0 .card');
    const zone = await centreOf(page, '#play-zone');
    const send = (type, x, y) => page.dispatchEvent('#hidden-0 .card', type, {
      pointerType: 'touch', pointerId: 1, isPrimary: true, bubbles: true,
      clientX: x, clientY: y,
    });

    await send('pointerdown', from.x, from.y);
    await send('pointermove', from.x + 2, from.y + 2);     // under the slop
    const beforeSlop = await page.evaluate(() =>
      document.getElementById('play-zone').className);
    expect(beforeSlop, 'no drag state until the press moves past the slop')
      .not.toMatch(/active-drop/);

    await send('pointermove', zone.x, zone.y);             // past the slop
    const overZone = await page.evaluate(() =>
      document.getElementById('play-zone').className);
    expect(overZone).toMatch(/active-drop/);
    expect(overZone, 'zone shows it is a valid drop target').toMatch(/drag-over/);

    await send('pointerup', zone.x, zone.y);
    const afterDrop = await page.evaluate(() =>
      document.getElementById('play-zone').className);
    expect(afterDrop).not.toMatch(/active-drop|drag-over/);
  });

  test('the drag leaves no transform or z-index behind on the card', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    const elsewhere = await centreOf(page, '#info-1');
    await pointerDrag(page, '#hidden-0 .card', elsewhere, 'touch');
    await page.waitForTimeout(200);

    // A cancelled drag must not strand the card mid-flight.
    const stranded = await page.evaluate(() =>
      [...document.querySelectorAll('#hidden-0 .card')]
        .filter(c => c.style.zIndex === '900').length);
    expect(stranded).toBe(0);
  });
});

test.describe('Mouse input is unaffected by the touch path', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  test('a pointer drag with pointerType "mouse" does not play the card', async ({ page }) => {
    // Mouse keeps using native HTML5 drag-and-drop (covered in gameplay.spec).
    // The pointer handler must bail out for it, or the two paths would both
    // fire and play two cards for one gesture.
    await loadApp(page);
    await startGame(page);
    await reachCardPrompt(page);

    const before = await page.evaluate(() => window.gameState.players[0].hiddenHand.length);
    const zone = await centreOf(page, '#play-zone');
    await pointerDrag(page, '#hidden-0 .card', zone, 'mouse');

    await page.waitForTimeout(300);
    expect(await page.evaluate(() => window.gameState.players[0].hiddenHand.length))
      .toBe(before);
    expect(await page.evaluate(() => window.gameState.playZone.length)).toBe(0);
  });
});
