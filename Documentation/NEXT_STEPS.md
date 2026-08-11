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

## Priority 1 — UI flexibility across devices

Measured by rendering a real mid-game board at eight viewports and reading back
element geometry. Desktop (1280×800), laptop (1024×640) and tablet (820×1180)
render correctly with all 13 cards of every hand unclipped. The defects below
are all real and reproducible.

### P1.1 — Landscape phones are unplayable *(critical)*

`styles.css` `@media (max-height: 480px)` sets:

```css
.player-area.north .hand-container,
.player-area.south .hand-container { display: none; }
```

South is the human seat. At 844×390 and 740×360 the player's own hand computes
to `display: none` — the cards cannot be seen, clicked, dragged or tabbed to.
The board shows four score badges, two clipped columns of card backs, and an
empty play zone. Any phone held sideways lands here.

Fix: never hide the human's hand. Fall back to a single compressed row (or a
horizontally scrollable strip) pinned under the play zone, and hide only the
*opponent* rows when vertical space runs out.

### P1.2 — Shown cards are clipped away on phones

`.hand-container.vertical-hand { max-height: 108px; overflow: hidden }` caps the
East/West columns. At 390×844 only 5 of 13 cards survive; at 844×390 only 3. The
CSS comment justifies this as "AI hands are just card-backs, clipping a few
isn't lossy" — but the same rule applies to `#shown-1` / `#shown-3`, and Shown
cards are public information the player needs to reason about. Losing them
silently changes the game, not just the picture.

Fix: clip Hidden columns only. Shown cards must always be reachable — wrap them
into a second column, shrink the stride to fit, or make the Shown strip scroll.

### P1.3 — Prompt overlay collides with the North hand

`#prompt-overlay { top: calc(50% - 175px) }` is a magic offset derived from the
play zone's height, not from where the North hand actually is. At 1280×800 the
prompt sits on top of North's card row and completely covers the North info
badge. It is re-tuned by hand in three separate media queries (`-150px`,
`-138px`, `-120px`, then `top: 8px`) and still collides.

Fix: position the prompt relative to the play zone (or place it in flow between
the North area and the play zone) so it can't be knocked out of alignment by a
viewport the breakpoints don't anticipate.

### P1.4 — Tap targets are ~20px on phones

At `max-width: 600px` the card is 38px wide with a 20px stride. Because each
card overlaps its predecessor, every card except the last exposes only ~20px of
tappable width — well under the 44px minimum. The 54px card height helps, but
horizontally these are hard to hit accurately.

Fix: scale the stride to the *remaining* hand size rather than fixing it per
breakpoint (13 cards need a tight stride, 4 cards do not), and consider a
tap-to-expand fan for the human hand.

### P1.5 — Drag-and-drop does not work on touch at all

`createCardElement()` wires `dragstart`/`dragend` and the play zone listens for
`dragover`/`drop`. These are HTML5 drag-and-drop events, which browsers do not
fire for touch input. There is no `pointerdown`/`touchstart` path anywhere in
`ui.js`. Tapping still works, so the game is playable — but the prompt reads
"Select a card to play. Click or Drag to Center," advertising an interaction
that cannot happen on a phone.

Fix: add a pointer-event drag path (or accept tap-only and reword the prompt
behind a `(pointer: coarse)` check).

### P1.6 — The layout is hand-tuned, not fluid *(root cause)*

Every player area is absolutely positioned, and sizing lives in four media
queries that each restate `--card-w` / `--card-h` / `--card-stride` as fixed
pixels, plus hard-coded `calc(100vh - 320px)` caps and per-breakpoint prompt
offsets. Each of P1.1–P1.4 is a symptom: any viewport that falls between the
authored tiers gets a layout nobody tested.

Fix: derive sizing from available space instead of enumerating devices.

- `--card-w: clamp(32px, 4.5vmin, 64px)` and let `--card-h` follow the aspect ratio.
- Compute the stride from the hand's allotted band and its card count, so 13
  cards always fit whatever space exists (a CSS var set by `renderHand()` is
  enough — no layout engine needed).
- Keep media queries for layout *mode* changes only (E/W row vs column, which
  rows collapse), never for sizing.

### P1.7 — Add device-matrix regression tests

`playwright.config.js` declares a single `Desktop Chrome` project, so none of
the above is covered. `tests/menu.spec.js` has two responsive assertions and
they only check `flex-direction` on the E/W areas at 900px.

Add projects for phone portrait, phone landscape and tablet, and assert the
invariants that actually matter:

1. The human's hand is visible and its cards are clickable in every project.
2. No tracked element extends past the viewport, in either axis.
3. `#prompt-overlay` does not intersect any hand container.
4. Every card in every **Shown** hand is unclipped by its container.

These are cheap to write — the geometry probe used for this review is just
`getBoundingClientRect()` over a fixed id list.

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
  engine-emitted events the UI subscribes to. Worth doing, but it should follow
  the responsive rework — otherwise the layout work gets done twice.
- **Accessibility (second pass)**: focus management between phases, arrow-key
  navigation within a hand, screen-reader announcements for AI plays.
- **Unity track**: still paused. Decide whether the scaffold is the Android-port
  base or gets archived. If kept, port the canonical tie rules into
  `GameRules.cs`, re-enable `DisabledTests`, and work through `MANUAL_TODO.md`.
- **Documentation**: keep `GAME_LOGIC.md` / `DESIGN_DECISIONS.md` in sync with
  the webapp when rules or architecture change.
