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

- [ ] **AI strategy calibration** *(design decision needed)*: Easy/Medium/Hard
  are implemented but untuned. Hard's only leading heuristic is "play the
  2nd-best face value", which tests currently pin. Decide target win-rates
  per difficulty, then run seeded AI-vs-AI batches (`setSeed` + `SPEED=0`
  make this cheap) and adjust. Update `tests/ai.spec.js` alongside any
  behaviour change.
- [ ] **Event-driven UI** *(architecture change)*: replace whole-board
  `renderAll()` repaints with engine-emitted events (cardPlayed,
  trickResolved, …) the UI subscribes to. Prerequisite for smoother
  animations and a cleaner engine/UI seam.
- [ ] **Accessibility (second pass)**: focus management between phases (move
  focus to the first playable card), arrow-key navigation within a hand,
  screen-reader announcements for AI plays.

## Unity track (paused)

- [ ] Decide whether to keep the Unity scaffold as the Android-port base or
  archive it. If kept: port the canonical 3-card-tie rule (void trick) into
  `GameRules.cs`, re-enable the `DisabledTests`, and work through the
  remaining Unity-Editor items in `MANUAL_TODO.md` (scene cleanup, Tips
  button, Test Framework install).

## Documentation

- [ ] Keep `GAME_LOGIC.md` / `DESIGN_DECISIONS.md` in sync with the webapp
  when rules or architecture change.
