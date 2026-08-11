# Recommendations & Next Steps

*Updated August 11, 2026. The browser webapp at the repo root is the canonical
implementation (see the top of the root README).*

## Verification of the June 2026 work

Every item previously listed under "Recently completed" was checked against the
tree at `61ee8d4` and confirmed present:

| Claim | Verified where |
| --- | --- |
| Versioned, schema-validated saves | `SAVE_VERSION` in `engine.js`; `isValidSave()` in `ui.js` |
| Crash-proof stats parsing | `readStats()` in `ui.js` |
| Test speed / determinism hooks | `delay()` + `SPEED`, `setSeed()`/`rng()` in `engine.js` |
| Keyboard play + ARIA | `role="button"`/`tabIndex` on cards and opponent badges in `ui.js`; `aria-live` regions in `index.html` |
| Canonical tie rules | `determineTrick()` in `engine.js`; leader rotates only when a prize is taken (`executeDeterminePhase`) |
| Dev-server hardening | `tools/static-server.js` |
| Repo cleanup | stray `.cs` drafts and `webapp/` are gone |
| CI | `.github/workflows/tests.yml` |

**Test suite: 100/100 passing.** `main`, `origin/main` and this branch all sit
at `61ee8d4` — there is no unmerged or unpushed work anywhere in the repo.

(Local runs need `npm ci` first; if the sandbox ships a Chromium build that
doesn't match the pinned `@playwright/test`, point `launchOptions.executablePath`
at the installed one. CI installs its own browser and is unaffected.)

---

## Priority 1 — UI flexibility across devices — **done**

All seven items below were measured at eight viewports (desktop, laptop, tablet
portrait/landscape, phone portrait ×2, phone landscape, 320×568), fixed, and
pinned by `tests/responsive.spec.js`. Sixteen of those fifty tests fail against
the pre-fix stylesheet, so they are real regression cover rather than
after-the-fact description.

- [x] **P1.1 Landscape phones were unplayable.** `@media (max-height: 480px)`
  set `display: none` on the South hand row — the human seat. Every rule that
  hid a hand is gone; the fluid sizing below means nothing has to be hidden.
- [x] **P1.2 Shown cards were clipped.** The `overflow: hidden` cap on the
  East/West columns left 5 of 13 cards on a phone, and applied to `#shown-1` /
  `#shown-3` too — public information, silently discarded. The stride formula
  replaces the cap: hands compress to fit rather than being cut off.
- [x] **P1.3 Prompt collided with the North hand.** `#prompt-overlay` now lives
  in the board grid's centre cell (`.center-area`) and is anchored to it, so
  the grid itself guarantees it cannot reach a hand. The four hand-tuned
  `top: calc(50% - Npx)` offsets are gone.
- [x] **P1.4 ~20px tap targets.** Stride is derived from hand size, so a short
  hand spreads back out; coarse pointers also get a larger `--stride-max`.
- [x] **P1.5 Touch drag never fired.** `attachPointerDrag()` in `ui.js` adds the
  gesture on Pointer Events for coarse pointers; mouse input keeps native HTML5
  drag-and-drop unchanged. The prompt now says "Tap" on touch devices.
- [x] **P1.6 Hand-tuned sizing (root cause).** Replaced four breakpoint tiers
  with one expression — `--card-w: clamp(24px, min(13vw, 7.4vh), 64px)` — and
  made every other dimension a multiple of it (hands, play zone, badges, card
  interiors, decorations). The board is a 3×3 CSS grid instead of four
  absolutely-positioned areas. Two media queries remain, and both switch layout
  *mode* only; neither restates a size.
- [x] **P1.7 Device-matrix tests.** `tests/responsive.spec.js` drives the
  viewport itself rather than adding Playwright projects, which would have
  multiplied the whole suite per device. Suite: 150 passing.

### Follow-ups this did not cover

- [ ] **Accessibility (second pass)**: focus management between phases, arrow-key
  navigation within a hand, screen-reader announcements for AI plays.
- [ ] Real-device verification. Everything above is Chromium at a set viewport
  size; iOS Safari's dynamic toolbar and `100dvh` behaviour deserve a look on
  hardware.

---

## Priority 2 — AI difficulty tuning

Measured with the real engine: 60 seeded games per matchup, `setSeed` +
`SPEED = 0` + `startGame({ humanId: -1, diffs })`, seats rotated between
matchups to separate skill from seat position.

| Seating (S / W / N / E) | Win rate % |
| --- | --- |
| Easy / Easy / Easy / Easy | 35.0 / 25.0 / 18.3 / 21.7 |
| Medium / Medium / Medium / Medium | 30.0 / 40.0 / 16.7 / 13.3 |
| Hard / Hard / Hard / Hard | 23.3 / 26.7 / 30.0 / 20.0 |
| **Hard / Medium / Hard / Medium** | **46.7 / 3.3 / 45.0 / 5.0** |
| **Medium / Easy / Medium / Easy** | **18.3 / 40.0 / 16.7 / 25.0** |
| Easy / Medium / Hard / Easy | 15.0 / 8.3 / 55.0 / 21.7 |
| Hard / Easy / Medium / Easy | 48.3 / 20.0 / 13.3 / 18.3 |
| Easy / Hard / Easy / Medium | 18.3 / 58.3 / 18.3 / 5.0 |
| Hard / Easy / Hard / Easy | 35.0 / 10.0 / 53.3 / 1.7 |

### P2.1 — The difficulty ladder is inverted *(bug, not tuning)*

Across every mixed matchup the ordering is **Hard ≫ Easy > Medium**:

- Hard vs Medium, two of each: Hard takes **91.7%** of games (46.7 + 45.0),
  mean score 39 vs 10.
- Medium vs Easy, two of each: **Easy takes 65%** (40.0 + 25.0). Medium loses to
  the random player.

Choosing "Medium" currently hands the player a *weaker* opponent than "Easy".
This is a correctness problem in the ladder, not a calibration nudge.

The likely cause is in `selectAICardToPlay()` (`engine.js`): Medium's whole rule
is "play the highest play-score card." Since the trick winner's own card is
always discarded, Medium spends its highest-Score cards to win tricks whose
prize pool is — by construction — the leftovers. Hard's "cheapest card that
still wins" takes the same tricks while keeping high-face-value cards alive for
the prize pool. Random play (Easy) accidentally preserves more scoring material
than Medium's greed does.

Fix: rebuild Medium as a *handicapped Hard* rather than a separate, worse
heuristic — Hard's logic with a probability of playing a random legal card
instead (ε-greedy), or Hard's follow logic without the prize-preservation term.
That guarantees monotonicity by construction, and the ε knob is the tuning dial.

### P2.2 — Decide the target win rates

Still the open design decision from the last pass, now with a baseline. Against
three identical opponents the neutral rate is 25%; suggested targets for a human
facing three of one difficulty: Easy ≈ 15%, Medium ≈ 25%, Hard ≈ 40% for the AI.
Write the decision into `DESIGN_DECISIONS.md` so future changes have a target to
regress against.

### P2.3 — Land the batch harness in the repo

The measurement above needs no new engine hooks — `setSeed`, `SPEED` and
`startGame({ humanId: -1, diffs })` already exist. Commit the batch runner under
`tools/` (or as a tagged, skipped-by-default spec) so tuning is repeatable
rather than re-derived each time. Budget: ~60 games per matchup runs in about
40 seconds.

### P2.4 — Fix seat bias before trusting any target

In the all-Easy control, seat 0 wins 35% against seat 2's 18.3% — with four
identical random players. Two contributors:

- `endGame()` in `ui.js` uses a strict `>` while iterating seats 0→3, so the
  lowest seat wins every final-score tie. That seat is always the human.
- The first round always starts at `starterId = 0`, and leadership rotates only
  when a prize is taken, so the opening advantage persists longer than one round.

Resolve the tie explicitly (share the win, or break on a stated rule) and
re-measure before setting win-rate targets, otherwise the targets bake the bias in.

### P2.5 — Give Hard real depth

Hard's leading move is "play the 2nd-best card by face value," a placeholder
that `tests/ai.spec.js:177` currently pins. All Shown hands and the discard pile
are public, so Hard could reasonably track what's gone, avoid handing itself a
value tie, and choose a lead by expected prize value. Update the pinned test
alongside any behaviour change.

---

## Deferred

- **Event-driven UI**: replace whole-board `renderAll()` repaints with
  engine-emitted events the UI subscribes to. Unblocked now that the responsive
  rework has landed — the layout no longer changes underneath it.
- **Unity track**: still paused. Decide whether the scaffold is the Android-port
  base or gets archived. If kept, port the canonical tie rules into
  `GameRules.cs`, re-enable `DisabledTests`, and work through `MANUAL_TODO.md`.
- **Documentation**: keep `GAME_LOGIC.md` / `DESIGN_DECISIONS.md` in sync with
  the webapp when rules or architecture change.
