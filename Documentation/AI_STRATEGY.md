# AI Decision Logic, Difficulty Calibration & Simulation

*Updated June 10, 2026. The AI layer was rebuilt around pure strategies plus
a calibrated blend layer, tuned with the headless simulator
(`tools/simulate.js`). This document is the reference for how the AIs think,
how the difficulty targets are defined, and how to re-run the calibration.*

## Architecture

The AI layer in `engine.js` has three parts:

1. **`AI_STRATEGIES`** — pure, individually-testable play strategies
   (`random`, `naive`, `greedy`, `hard`, `master`).
2. **`AI_TARGETERS`** — reveal-target strategies
   (`random`, `fewestHidden`, `leader`, `weakest`).
3. **`AI_PROFILES`** — the calibration layer. Each difficulty maps to a
   targeter plus a play blend: with probability `p` the AI uses `playAlt`,
   otherwise `play`. The blend probability is the tuning knob that hits the
   difficulty targets.

```
Random: target random        play random                       (internal benchmark baseline)
Easy:   target random        70% naive  / 30% random
Medium: target fewestHidden  25% master / 75% random
Hard:   target leader        17% master / 83% hard
Master: target weakest       100% master                       (no blend — maximum strength)
```

## Difficulty targets and how they're measured

**Benchmark:** one **Master-grade reference seat** (a proxy for a skilled
human player) plays a table of **three candidate-difficulty AIs**, with the
reference seat rotated each game to cancel first-leader bias. The target is
the candidate tier's **collective win rate** against that reference:

| Difficulty | Tier win rate target | Measured (10k games) | Player experience |
|---|---|---|---|
| Easy | 33% | 33.3% ±0.9 | A skilled player beats an Easy table 2 games in 3 |
| Medium | 50% | 50.5% ±1.0 | Even odds against a skilled player |
| Hard | 67% | 66.8% ±0.9 | A skilled player loses to a Hard table 2 games in 3 |
| Master | maximum | — | The reference itself; beats a 3-Hard table 33.2% (25% = par) |

**Why this framing?** The naive framing ("candidate seat vs 3 random bots")
caps out near 60% — the deal's randomness dominates, and no strategy we
found exceeds ~60% there — making a 67% target unreachable. Defining targets
as *how often the tier beats a skilled player* is both achievable and matches
what a difficulty label means to the person playing.

## The strategies

### `random`
Uniform random card. The internal `Random` baseline plays this (25% by
symmetry at a uniform table).

### `naive` (beginner)
Always plays the highest **face-value** card — slams the Ace because it's
big. Wasteful: burns strength early and hands opponents high-face prize
material. Measurably *weaker* than random; Easy blends toward this.

### `greedy`
Always plays the highest **play-score** card. Modestly better than naive,
still wasteful.

### `hard` (heuristic core)
- **Leading**: 2nd-best face value (don't telegraph the strongest card).
- **Following**: the *cheapest* card (by play score) that still beats the
  zone; a Shown King (play 5, face 13) is the cheapest possible winner. If
  nothing wins, dump the lowest face value to minimise the prize given away.

### `master` (strongest found)
- **Last to act**: evaluates every candidate card **exactly** by running the
  real trick resolver (`determineTrick`) on the hypothetical play zone —
  weighing the prize it would take, whether its own card would be gifted to
  another winner, and a per-face spend cost (`MASTER_PARAMS`). This seat is
  where Master's edge is sharpest: it never gifts a high card avoidably and
  always finds the cheapest exact winner.
- **Leading**: dumps the **lowest face value**. Leading is structurally weak
  (your card must survive all three opponents), so Master sheds junk and
  fights from the later seats. Measured best of four lead styles.
- **Following mid-trick**: Hard's cheapest-winner logic, optionally gated by
  a card-counting hold probability (`masterHoldProbability`, public
  information only — shown hands, scoring zones, discards, hand sizes).
  The gate (`MASTER_PARAMS.holdMin`) measured neutral and ships at 0.
- **Reveal target**: the *weakest* opponent. Revealing **helps** the target
  on average (a random hidden card gains expected play value: +2 for 2..Q,
  −8/−4 for K/A → mean +10/13), so boosting the weakest beats boosting the
  leader. (Hard still targets the leader — thematically "disruptive," and
  the measured difference is within noise.)

### Negative results (tried, measured, rejected)
- **Expected-value mid-trick play** (hold-probability × prize EV): ~4–9
  points worse than cheapest-winner across weight sweeps. Winning cheap and
  preserving strength across 13 tricks beats per-trick EV maximisation.
- **Tie-dumping** (deliberately value-tying the zone's best card to
  eliminate it when unable to win): ~6 points worse — it trades your card
  1:1 against an opponent's, a losing trade when your cards are stronger on
  average.
- **Lead styles** `diff` (play-vs-face differential), `secondFace`,
  `maxValue`: all 1–3 points behind `lowFace`.
- **Mid-trick hold-probability gating** at thresholds 0.2–0.65: neutral to
  −2 points vs threshold 0.

## The simulator (`tools/simulate.js`)

Headless Node harness — loads `engine.js` into a vm context with the DOM
stubbed out, maps `setTimeout` → `setImmediate`, auto-dismisses the
Determine click-wait, and seeds every game (`setSeed(baseSeed + gameIndex)`)
for reproducibility. ~1.5 ms per 13-round game; a 10k-game benchmark takes
about a minute. Pure local CPU — no browser, no network.

```powershell
npm run simulate                                  # full calibration matrix
node tools/simulate.js --benchmark Hard --games 10000
node tools/simulate.js --table Master,Hard,Medium,Easy --games 2000
node tools/simulate.js --benchmark Easy --set "Easy.p=0.5"     # experiment
node tools/simulate.js --set "MASTER.leadStyle=secondFace" --benchmark Hard
```

`--set` overrides `AI_PROFILES.<Difficulty>.<key>` or `MASTER_PARAMS.<key>`
(scope `MASTER`) for experiments without touching engine.js.

## Recalibrating

If strategies or game rules change, re-run `npm run simulate` and adjust the
`p` values in `AI_PROFILES` (engine.js). The blend response is close to
linear in `p`, so two anchor measurements (`p=0`, `p=1`) and one
interpolation step usually land within the confidence interval.
