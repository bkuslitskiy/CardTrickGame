# Recommendations & Next Steps

*Updated June 10, 2026. The browser webapp at the repo root is the canonical
implementation (see the top of the root README). Items below are split by
track.*

## Recently completed (June 2026)

- [x] **Test honesty pass**: rotation tests now drive the real
  `executeDeterminePhase`; conditional/no-op assertions removed; the
  click-to-continue and keyboard paths have real interaction tests.
- [x] **Test speed hook**: `window.SPEED` scales all `delay()` waits (tests
  set it to 0); `window.setSeed(n)` gives a deterministic shuffle/reveal RNG.
- [x] **Save hardening**: versioned save payload (`SAVE_VERSION`), full schema
  validation on resume, crash-proof stats parsing, quota-safe `saveGame`.
- [x] **Dev server hardening**: loopback-only bind, separator-safe path-root
  check, dotfile (`.git`) denial, malformed-URI handling.
- [x] **Accessibility (first pass)**: keyboard play (cards and opponent badges
  are focusable buttons; Enter/Space activates; Enter dismisses trick
  results), ARIA labels on cards, live regions for status/prompt.
- [x] **CI**: GitHub Actions workflow runs the Playwright suite on push/PR.
- [x] **Repo cleanup**: stray root `.cs` drafts, `Assets/Prefabs` placeholder
  scripts, `Assets/Scripts/UI/index.html`, and `webapp/` removed.

## Webapp — next up

- [x] **AI strategy calibration** *(done June 10, 2026)*: difficulties are
  calibrated blends of pure strategies, tuned with the headless simulator
  (`npm run simulate`) to the agreed targets — a Master-grade reference
  player beats an Easy table 67% of the time, splits with Medium, and loses
  to Hard 67% of the time; the new **Master** difficulty is the unblended
  maximum-strength strategy. See `Documentation/AI_STRATEGY.md` for the
  benchmark definition, measured results, and how to recalibrate.
- [ ] **Event-driven UI** *(architecture change)*: replace whole-board
  `renderAll()` repaints with engine-emitted events (cardPlayed,
  trickResolved, …) the UI subscribes to. Prerequisite for smoother
  animations and a cleaner engine/UI seam.
- [ ] **Accessibility (second pass)**: focus management between phases (move
  focus to the first playable card), arrow-key navigation within a hand,
  screen-reader announcements for AI plays.

## Unity track (paused)

- [ ] Decide whether to keep the Unity scaffold as the Android-port base or
  archive it. If kept: port the canonical tie rules into `GameRules.cs`
  (all value-tied cards discarded; 3-way-tie survivor wins with no prize;
  leader rotates only when a prize is taken), re-enable the `DisabledTests`,
  and work through the remaining Unity-Editor items in `MANUAL_TODO.md`
  (scene cleanup, Tips button, Test Framework install).

## Documentation

- [ ] Keep `GAME_LOGIC.md` / `DESIGN_DECISIONS.md` in sync with the webapp
  when rules or architecture change.
