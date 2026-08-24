# Card Game — Webapp Test Suite

These tests are the behavioural **spec** for the JS card game. The rewrite is
complete and the suite is expected to pass in full — a failing test means a
contract regression. They also run in CI (`.github/workflows/tests.yml`).

## Setup

```powershell
# From the project root
npm install
npx playwright install chromium
```

## Run

```powershell
npm test                 # full suite, headless
npm run test:headed      # see the browser
npm run test:ui          # Playwright UI mode (great for debugging)

# Single file
npm run test:engine
npm run test:menu
npm run test:gameplay
npm run test:persistence
```

The harness boots a tiny static server (`tools/static-server.js`) on
`http://localhost:4173` and serves the repo root, so the tests load
`index.html` with a real HTTP origin (matching production localStorage
behaviour).

## What's covered

| File                       | Focus                                                        |
|----------------------------|--------------------------------------------------------------|
| `tests/engine.spec.js`     | Card scoring tables, Deck/Player invariants, trick resolver  |
| `tests/menu.spec.js`       | Menu defaults, rules modal, Start / Abandon lifecycle        |
| `tests/gameplay.spec.js`   | Reveal, click-to-play, drag-and-drop, turn rotation, edges   |
| `tests/persistence.spec.js`| Save / resume, stats, end screen, conservation of cards      |

## Required surface (what engine.js must expose)

The tests rely on these being attached to `window`:

- `window.Card`, `window.Deck`, `window.Player`            (data classes)
- `window.gameState`                                       (live state object)
- `window.startGame()`, `window.resumeGame()`, `window.showMenu()`, `window.endGame()`
- `window.determineTrick(playZone)` — pure trick resolver returning
  `{ winnerId, prizeCard, valueTiedCards, scoreTiedCards }`
- `window.selectAITarget`, `window.selectAICardToPlay`     (AI strategy)
- `window.SAVE_VERSION` — current save-payload schema version

## Test hooks

- **`window.SPEED`** — multiplier applied to every `delay()` in engine.js.
  Default `1` (real-time animations). Tests that drive whole rounds or full
  games set `window.SPEED = 0` *before* calling `startGame()` so the loop
  runs at full speed. Leave it at 1 in tests that observe animation timing
  (e.g. the active-turn highlight sweep).
- **`window.setSeed(n)`** — installs a deterministic PRNG (mulberry32) behind
  the deck shuffle, the reveal pick, and Easy-AI choices, so deals are
  reproducible. `setSeed(null)` restores `Math.random`.

## Canonical rules pinned by these tests

Ties are resolved **twice** per round, at two different points and on two
different numbers:

- **Value ties** — checked *before* the winner is determined. ALL cards
  involved in a value tie are discarded, whatever the tie size. A 3-way tie
  leaves one survivor who **wins the round but takes no prize** (nothing
  remains after their own card is discarded). A 4-way tie, or two 2-way ties,
  eliminates every card — nobody wins.
- **Score ties** — checked *after* the winner is determined and *before* the
  prize is awarded, among the non-winning survivors (the prize candidates) and
  on Score (face value), not Value. Candidates that tie each other on Score are
  discarded and the next highest is taken; if all of them are tied away, the
  winner takes no prize.
- **Round starter advancement**: the leader rotates to
  `(starterId + 1) % 4` — one seat clockwise from the **previous leader** —
  **every round, regardless of the outcome**. Who won the trick and whether a
  prize was taken have no effect on who leads next. `executeDeterminePhase`
  never touches `starterId`; `executeScorePhase` owns the rotation.
