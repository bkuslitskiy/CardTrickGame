// Headless AI-vs-AI simulation harness.
//
// Loads engine.js into a Node vm context with the DOM/UI surface stubbed to
// no-ops, then runs complete 13-round games at full speed (window.SPEED = 0,
// setTimeout mapped to setImmediate). A full game takes ~1ms, so calibration
// batches of thousands of games finish in seconds — pure local CPU, no
// browser, no network.
//
// Usage:
//   node tools/simulate.js --table Hard,Easy,Easy,Easy --games 2000
//   node tools/simulate.js --benchmark Medium --games 4000
//   node tools/simulate.js --calibrate            # full benchmark matrix
//
// The calibration benchmark (--benchmark D) seats ONE candidate of
// difficulty D against THREE pure-random opponents ('Random'), rotating the
// candidate through all four seats to cancel first-leader bias, and reports
// the candidate's win rate. Targets (see Documentation/AI_STRATEGY.md):
//   Easy 33%  ·  Medium 50%  ·  Hard 67%  ·  Master = maximum achievable
//
// Reproducibility: every game is seeded (base seed + game index) through the
// engine's own setSeed(), so a given (--seed, --games, table) triple always
// produces identical results.

'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');

// ---------------------------------------------------------------------------
// Build the sandboxed engine context
// ---------------------------------------------------------------------------

function createEngineContext() {
  const sandbox = {};

  // Self-referential window, like a browser global scope.
  sandbox.window = sandbox;
  sandbox.globalThis = sandbox;

  // Timers: map setTimeout to setImmediate so SPEED=0 delays cost an event
  // loop turn (~µs) instead of a 1ms+ timer tick.
  sandbox.setTimeout = (fn, _ms, ...args) => setImmediate(fn, ...args);
  sandbox.clearTimeout = () => {};
  sandbox.setInterval = setInterval;
  sandbox.clearInterval = clearInterval;

  // Document stub. The engine's Determine click-wait registers a 'click'
  // listener — auto-fire it on the next tick so the wait self-dismisses.
  sandbox.document = {
    addEventListener(type, handler) {
      if (type === 'click') setImmediate(() => handler({ type: 'click' }));
    },
    removeEventListener() {},
    getElementById() { return null; },
    querySelector() { return null; },
    querySelectorAll() { return []; },
  };

  sandbox.localStorage = {
    getItem() { return null; },
    setItem() {},
    removeItem() {},
    clear() {},
  };

  // UI functions engine.js calls by bare identifier (defined in ui.js in the
  // browser). All no-ops here.
  sandbox.renderAll = () => {};
  sandbox.renderPlayZone = () => {};
  sandbox.renderHand = () => {};
  sandbox.updateStatus = () => {};
  sandbox.setPrompt = () => {};
  sandbox.decorateCard = () => {};
  sandbox.animatePrizeToWinner = async () => {};
  sandbox.endGame = () => { sandbox.gameState.isRunning = false; };

  sandbox.console = console;

  vm.createContext(sandbox);
  const engineSource = fs.readFileSync(path.join(__dirname, '..', 'engine.js'), 'utf8');
  vm.runInContext(engineSource, sandbox, { filename: 'engine.js' });

  sandbox.SPEED = 0;
  return sandbox;
}

// ---------------------------------------------------------------------------
// Run a single game
// ---------------------------------------------------------------------------

async function runGame(ctx, difficulties, seed) {
  ctx.setSeed(seed);

  const players = difficulties.map((d, i) => new ctx.Player(i, `P${i}`, false, d));
  const gs = {
    players,
    activePlayerId: null,
    round: 1, phase: 'Reveal', starterId: 0,
    playZone: [], discards: [],
    isRunning: true, resolveInput: null,
  };
  ctx.gameState = gs;

  const deck = new ctx.Deck();
  players.forEach(p => { p.hiddenHand = deck.deal(13); });

  await vm.runInContext('gameLoop()', ctx);

  // Winner: highest score, first seat wins ties (same rule as ui.endGame).
  let winner = 0; let max = -1;
  const scores = players.map(p => p.score);
  scores.forEach((s, i) => { if (s > max) { max = s; winner = i; } });
  return { winner, scores };
}

// ---------------------------------------------------------------------------
// Batches
// ---------------------------------------------------------------------------

async function runTable(ctx, table, games, baseSeed) {
  const wins = [0, 0, 0, 0];
  const scoreSums = [0, 0, 0, 0];
  for (let g = 0; g < games; g++) {
    const { winner, scores } = await runGame(ctx, table, baseSeed + g);
    wins[winner]++;
    scores.forEach((s, i) => { scoreSums[i] += s; });
  }
  return {
    wins,
    winRates: wins.map(w => w / games),
    avgScores: scoreSums.map(s => s / games),
  };
}

// Difficulty benchmark: a Master-grade reference seat (the "skilled player"
// proxy) vs THREE candidate-difficulty AIs, reference seat rotated to cancel
// first-leader bias. The reported tierWinRate is how often the candidate
// tier collectively beats the skilled player — the number the difficulty
// targets are defined on (Easy 33% / Medium 50% / Hard 67%).
async function runBenchmark(ctx, candidate, games, baseSeed) {
  let tierWins = 0;
  let refScore = 0;
  for (let g = 0; g < games; g++) {
    const seat = g % 4;
    const table = [candidate, candidate, candidate, candidate];
    table[seat] = 'Master';
    const { winner, scores } = await runGame(ctx, table, baseSeed + g);
    if (winner !== seat) tierWins++;
    refScore += scores[seat];
  }
  const rate = tierWins / games;
  return {
    tierWinRate: rate,
    refWinRate: 1 - rate,
    refAvgScore: refScore / games,
    // 95% CI half-width for a binomial proportion.
    ci95: 1.96 * Math.sqrt(rate * (1 - rate) / games),
  };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function arg(name, fallback) {
  const i = process.argv.indexOf(`--${name}`);
  return i > -1 && process.argv[i + 1] !== undefined ? process.argv[i + 1] : fallback;
}

const pct = x => `${(100 * x).toFixed(1)}%`;

async function main() {
  const games = Number(arg('games', 2000));
  const baseSeed = Number(arg('seed', 1));
  const ctx = createEngineContext();
  const t0 = Date.now();

  // --set Medium.p=0.3 --set MASTER.giftRisk=0.2 ... — profile / Master-param
  // overrides for calibration experiments without touching engine.js.
  for (let i = 0; i < process.argv.length; i++) {
    if (process.argv[i] === '--set' && process.argv[i + 1]) {
      const [pathStr, raw] = process.argv[i + 1].split('=');
      const [scope, key] = pathStr.split('.');
      const num = Number(raw);
      const value = Number.isNaN(num) ? raw : num;
      if (scope === 'MASTER') {
        ctx.MASTER_PARAMS[key] = value;
        console.log(`override: MASTER_PARAMS.${key} = ${value}`);
      } else {
        ctx.AI_PROFILES[scope][key] = value;
        console.log(`override: AI_PROFILES.${scope}.${key} = ${value}`);
      }
    }
  }

  if (process.argv.includes('--calibrate')) {
    const targets = { Easy: 0.33, Medium: 0.50, Hard: 0.67 };
    console.log(`Benchmark: 1 Master reference seat vs 3 candidate AIs, seat rotated.`);
    console.log(`tier win rate = candidate tier collectively beats the reference. ${games} games each, seed ${baseSeed}.\n`);
    console.log('difficulty    tier win rate   ±95% CI   target   ref win rate');
    for (const d of Object.keys(targets)) {
      const r = await runBenchmark(ctx, d, games, baseSeed);
      console.log(
        `${d.padEnd(12)} ${pct(r.tierWinRate).padStart(12)}   ±${pct(r.ci95).padStart(5)}   ${pct(targets[d]).padStart(6)}   ${pct(r.refWinRate).padStart(10)}`
      );
    }
    // Master headline: strongest tier — reference vs 3 Master is 25% by
    // symmetry; report Master vs 3 Hard-tier tables as its strength metric.
    const m = await runBenchmark(ctx, 'Hard', games, baseSeed); // ref IS Master
    console.log(`\nMaster (reference) beats a 3-Hard table ${pct(m.refWinRate)} of the time (25% = par).`);
  } else if (arg('benchmark', null)) {
    const d = arg('benchmark');
    const r = await runBenchmark(ctx, d, games, baseSeed);
    console.log(`Master vs 3 ${d} over ${games} games: tier win rate ${pct(r.tierWinRate)} (±${pct(r.ci95)}), ref win rate ${pct(r.refWinRate)}, ref avg score ${r.refAvgScore.toFixed(1)}`);
  } else {
    const table = arg('table', 'Easy,Medium,Hard,Easy').split(',').map(s => s.trim());
    if (table.length !== 4) { console.error('--table needs exactly 4 comma-separated difficulties'); process.exit(1); }
    const r = await runTable(ctx, table, games, baseSeed);
    console.log(`Table [${table.join(', ')}] over ${games} games (seed ${baseSeed}):`);
    table.forEach((d, i) => {
      console.log(`  seat ${i} ${d.padEnd(8)} wins ${pct(r.winRates[i]).padStart(7)}   avg score ${r.avgScores[i].toFixed(1)}`);
    });
  }

  console.log(`\n(${((Date.now() - t0) / 1000).toFixed(1)}s)`);
}

main().catch(e => { console.error(e); process.exit(1); });
