// Menu screen, rules modal, and lifecycle transitions.
//
// These tests target the *intent* of the menu, not the current CSS — many
// assertions check JS state (e.g. gameState.isRunning) so they remain
// meaningful even while styles.css is being rebuilt. Where DOM visibility
// matters (rules modal in/out), we use inline style assertions rather than
// relying on class-based CSS rules.

const { test, expect } = require('@playwright/test');
const { loadApp, startGame, readState } = require('./helpers');

test.describe('Initial menu state', () => {
  test('on a clean load the game is not running', async ({ page }) => {
    await loadApp(page);
    const running = await page.evaluate(() =>
      window.gameState && window.gameState.isRunning === true);
    expect(running).toBe(false);
  });

  test('difficulty dropdowns have the documented defaults', async ({ page }) => {
    await loadApp(page);
    expect(await page.locator('#diff-1').inputValue()).toBe('Medium');
    expect(await page.locator('#diff-2').inputValue()).toBe('Hard');
    expect(await page.locator('#diff-3').inputValue()).toBe('Easy');
  });

  test('stats display reads "0 / 0 / 0" for a fresh user', async ({ page }) => {
    await loadApp(page);
    const text = await page.locator('#stats-display').innerText();
    expect(text).toMatch(/Games Played:\s*0/);
    expect(text).toMatch(/Human Wins:\s*0/);
    expect(text).toMatch(/High Score:\s*0/);
  });

  test('resume button is hidden when no saved game exists', async ({ page }) => {
    await loadApp(page);
    const display = await page.locator('#resume-btn').evaluate(el => el.style.display);
    // The element starts with inline display:none until localStorage has a save.
    expect(display === 'none' || display === '').toBe(true);
  });
});

test.describe('Rules modal', () => {
  // Exact-name role queries — `button:has-text("Rules")` also matches the
  // "Close Rules" button as a substring, tripping Playwright's strict mode.
  const rulesBtn = (page) => page.getByRole('button', { name: 'Rules',       exact: true });
  const closeBtn = (page) => page.getByRole('button', { name: 'Close Rules', exact: true });

  test('opens and closes via its own buttons', async ({ page }) => {
    await loadApp(page);

    // Closed by default (inline style="display: none;" on #rules-screen).
    expect(await page.locator('#rules-screen').evaluate(el => el.style.display)).toBe('none');

    await rulesBtn(page).click();
    expect(await page.locator('#rules-screen').evaluate(el => el.style.display)).not.toBe('none');

    await closeBtn(page).click();
    expect(await page.locator('#rules-screen').evaluate(el => el.style.display)).toBe('none');
  });

  test('rules text mentions the Hidden/Shown value tables', async ({ page }) => {
    await loadApp(page);
    await rulesBtn(page).click();
    const body = await page.locator('#rules-screen').innerText();
    expect(body).toMatch(/Hidden/);
    expect(body).toMatch(/Shown/);
    expect(body).toMatch(/face value/i);
  });
});

test.describe('Start New Game', () => {
  test('initializes 4 players and deals 13 cards each into Hidden', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    const s = await readState(page);

    expect(s.isRunning).toBe(true);
    expect(s.players).toHaveLength(4);
    expect(s.round).toBe(1);
    expect(s.playZoneSize).toBe(0);
    expect(s.players.map(p => p.hiddenCount)).toEqual([13, 13, 13, 13]);
    expect(s.players.map(p => p.shownCount)).toEqual([0, 0, 0, 0]);
    expect(s.players.map(p => p.scoringCount)).toEqual([0, 0, 0, 0]);
  });

  test('player 0 is the human, players 1..3 are AI with the picked difficulties', async ({ page }) => {
    await loadApp(page);
    await startGame(page, { diff1: 'Easy', diff2: 'Medium', diff3: 'Hard' });
    const s = await readState(page);
    expect(s.players[0]).toMatchObject({ id: 0, isHuman: true });
    expect(s.players[1]).toMatchObject({ id: 1, isHuman: false, difficulty: 'Easy' });
    expect(s.players[2]).toMatchObject({ id: 2, isHuman: false, difficulty: 'Medium' });
    expect(s.players[3]).toMatchObject({ id: 3, isHuman: false, difficulty: 'Hard' });
  });

  test('the human (south) starts the first round', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    const s = await readState(page);
    expect(s.starterId).toBe(0);
  });

  test('all 52 cards are dealt and unique across the four hidden hands', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    const summary = await page.evaluate(() => {
      const all = window.gameState.players.flatMap(p => p.hiddenHand);
      const keys = all.map(c => `${c.suit}-${c.rank}`);
      return { total: all.length, unique: new Set(keys).size };
    });
    expect(summary.total).toBe(52);
    expect(summary.unique).toBe(52);
  });
});

test.describe('startGame options', () => {
  test('{ humanId: -1 } produces an all-AI game', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: -1 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const flags = await page.evaluate(() =>
      window.gameState.players.map(p => p.isHuman));
    expect(flags).toEqual([false, false, false, false]);
  });

  test('{ humanId: 2 } puts the human at the North seat', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame({ humanId: 2 }); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const summary = await page.evaluate(() =>
      window.gameState.players.map(p => ({
        id: p.id, name: p.name, isHuman: p.isHuman,
      })));
    expect(summary).toEqual([
      { id: 0, name: 'South', isHuman: false },
      { id: 1, name: 'West',  isHuman: false },
      { id: 2, name: 'North', isHuman: true  },
      { id: 3, name: 'East',  isHuman: false },
    ]);
  });

  test('default startGame() (no opts) puts the human at seat 0', async ({ page }) => {
    await loadApp(page);
    await page.evaluate(() => { void window.startGame(); });
    await page.waitForFunction(() => window.gameState?.isRunning);

    const flags = await page.evaluate(() =>
      window.gameState.players.map(p => p.isHuman));
    expect(flags).toEqual([true, false, false, false]);
  });
});

test.describe('Responsive layout', () => {
  test('E/W player areas use column layout at ≤900px width', async ({ page }) => {
    await page.setViewportSize({ width: 800, height: 800 });
    await loadApp(page);
    await startGame(page);

    const dir = await page.locator('.player-area.west').evaluate(el =>
      window.getComputedStyle(el).flexDirection);
    expect(dir).toBe('column');
  });

  test('E/W player areas use row layout at >900px width', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await loadApp(page);
    await startGame(page);

    const dir = await page.locator('.player-area.west').evaluate(el =>
      window.getComputedStyle(el).flexDirection);
    expect(dir).toBe('row');
  });
});

test.describe('Abandon Game', () => {
  test('returns to the menu and stops the game loop', async ({ page }) => {
    await loadApp(page);
    await startGame(page);

    await page.evaluate(() => window.showMenu());

    const stopped = await page.evaluate(() => window.gameState.isRunning === false);
    expect(stopped).toBe(true);

    const menuDisp = await page.locator('#menu-screen').evaluate(el => el.style.display);
    expect(menuDisp).toBe('flex');
    const boardDisp = await page.locator('#game-board').evaluate(el => el.style.display);
    expect(boardDisp).toBe('none');
  });

  test('after abandon, resume button is visible (game is saved)', async ({ page }) => {
    await loadApp(page);
    await startGame(page);
    await page.evaluate(() => window.showMenu());
    const display = await page.locator('#resume-btn').evaluate(el => el.style.display);
    expect(display).not.toBe('none');
  });
});
