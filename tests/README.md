# Card Game — Webapp Test Suite

These tests are a behavioural **spec** for the JS rewrite of the card game. They
are expected to fail until the rewrite is complete — read each failing test as
a contract the rebuilt code has to honour.

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
- **`window.determineTrick(playZone)`** — *NEW*. Pure function returning
  `{ winnerId: number | null, prizeCard: Card | null }`. The current
  algorithm is buried inside `executeDeterminePhase` as a closure; the
  rewrite should hoist it so it's testable in isolation.

## Known-ambiguous rules pinned by these tests

- **Three-card value tie**: tests assume "all cards discarded, nobody wins"
  (matches the current `engine.js` behaviour). `Basic rules.txt` also
  mentions a "fourth card wins, no prize" variant — pick one and flip the
  matching test if you choose the alternate.
- **Round starter advancement**: tests pin the current formula
  `nextStarter = (winnerId + 3) % 4` (counter-clockwise from the winner).
