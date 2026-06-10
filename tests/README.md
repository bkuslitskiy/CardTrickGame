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

## Known-ambiguous rules pinned by these tests

- **Three-card value tie**: tests assume "all cards discarded, nobody wins"
  (matches the current `engine.js` behaviour). `Basic rules.txt` also
  mentions a "fourth card wins, no prize" variant — pick one and flip the
  matching test if you choose the alternate.
- **Round starter advancement**: tests pin the current formula
  `nextStarter = (winnerId + 3) % 4` (counter-clockwise from the winner).
