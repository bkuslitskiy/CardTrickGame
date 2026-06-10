const Suits = { CLUBS: '♣', DIAMONDS: '♦', HEARTS: '♥', SPADES: '♠' };
const Ranks = { 2: '2', 3: '3', 4: '4', 5: '5', 6: '6', 7: '7', 8: '8', 9: '9', 10: '10', 11: 'J', 12: 'Q', 13: 'K', 14: 'A' };

// All animation/AI pacing waits route through delay() and scale by
// window.SPEED (default 1). Tests set window.SPEED = 0 to run the game loop
// at full speed instead of real-time.
const delay = ms => new Promise(res => setTimeout(res, ms * (window.SPEED ?? 1)));

// Game randomness (shuffle, reveal pick, Easy AI) routes through rng() so
// tests can install a deterministic sequence. setSeed(n) swaps in a
// mulberry32 PRNG; setSeed(null) restores Math.random.
let _rng = Math.random;
function rng() { return _rng(); }
function setSeed(seed) {
    if (seed === null || seed === undefined) { _rng = Math.random; return; }
    let a = seed >>> 0;
    _rng = function () {
        a |= 0; a = (a + 0x6D2B79F5) | 0;
        let t = Math.imul(a ^ (a >>> 15), 1 | a);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

class Card {
    constructor(suit, rank) { this.suit = suit; this.rank = rank; }
    get faceValue() { return this.rank; }
    get hiddenScore() { return this.rank; }
    get shownScore() {
        if (this.rank >= 2 && this.rank <= 10) return this.rank + 2;
        if (this.rank === 11) return 13;
        if (this.rank === 12) return 14;
        if (this.rank === 13) return 5;
        if (this.rank === 14) return 10;
        return 0;
    }
    get colorClass() { return (this.suit === Suits.HEARTS || this.suit === Suits.DIAMONDS) ? 'red' : 'black'; }
    get name() { return `${Ranks[this.rank]}${this.suit}`; }
}

class Deck {
    constructor() {
        this.cards = [];
        Object.values(Suits).forEach(suit => { Object.keys(Ranks).forEach(rank => { this.cards.push(new Card(suit, parseInt(rank))); }); });
        this.shuffle();
    }
    shuffle() {
        for (let i = this.cards.length - 1; i > 0; i--) {
            const j = Math.floor(rng() * (i + 1));
            [this.cards[i], this.cards[j]] = [this.cards[j], this.cards[i]];
        }
    }
    deal(count) { return this.cards.splice(0, count); }
}

class Player {
    constructor(id, name, isHuman, difficulty = 'Easy') {
        this.id = id; this.name = name; this.isHuman = isHuman; this.difficulty = difficulty;
        this.hiddenHand = []; this.shownHand = []; this.scoringZone = [];
    }
    get score() { return this.scoringZone.reduce((sum, c) => sum + c.faceValue, 0); }
    get hasHidden() { return this.hiddenHand.length > 0; }
    removeCard(card) {
        let idx = this.hiddenHand.indexOf(card);
        if (idx > -1) { this.hiddenHand.splice(idx, 1); return 'Hidden'; }
        idx = this.shownHand.indexOf(card);
        if (idx > -1) { this.shownHand.splice(idx, 1); return 'Shown'; }
        return null;
    }
}

// NOTE: `var` (not `let`) so the binding attaches to window and stays in sync
// when ui.js reassigns it inside startGame(). Tests rely on window.gameState
// always pointing at the live state object.
var gameState = { players: [], round: 1, phase: '', starterId: 0, playZone: [], discards: [], isRunning: false, resolveInput: null, activePlayerId: null };

/**
 * Pure trick-resolution function.
 *
 * @param {Array<{card: Card, player: {id: number}, section: 'Hidden'|'Shown'}>} playZone
 * @returns {{
 *   winnerId: number|null,
 *   prizeCard: Card|null,
 *   valueTiedCards: Card[],   // cards eliminated in the value-tie step
 *   scoreTiedCards: Card[]    // cards eliminated in the prize score-tie step
 * }}
 *
 * Rules (from Basic rules.txt):
 *   - Value ties of 2 cards: those 2 are discarded, remaining cards rescore.
 *   - Value ties of 3 or 4 cards: the trick is void — nobody wins, no prize.
 *   - Among the non-winner survivors, the highest faceValue is the prize.
 *     If 2+ prize candidates tie on faceValue they're discarded and the
 *     next-highest is taken.
 */
function determineTrick(playZone) {
    const empty = { winnerId: null, prizeCard: null, valueTiedCards: [], scoreTiedCards: [] };
    if (!playZone || playZone.length < 4) return empty;

    // Evaluate Value (for the winner) and Score (for the prize).
    const evaluated = playZone.map(p => ({
        card:    p.card,
        player:  p.player,
        section: p.section,
        value:   p.section === 'Hidden' ? p.card.hiddenScore : p.card.shownScore,
        score:   p.card.faceValue,
    }));

    const valueCounts = {};
    evaluated.forEach(p => { valueCounts[p.value] = (valueCounts[p.value] || 0) + 1; });

    // 3- or 4-way value ties void the entire trick — visually, every card is
    // "cut" since per Basic rules every card is discarded and nobody wins.
    if (Object.values(valueCounts).some(n => n >= 3)) {
        return {
            winnerId: null,
            prizeCard: null,
            valueTiedCards: evaluated.map(p => p.card),
            scoreTiedCards: [],
        };
    }

    // Cards eliminated by a 2-way value tie.
    const valueTiedCards = evaluated.filter(p => valueCounts[p.value] === 2).map(p => p.card);

    // Surviving cards are eligible to win or compete for the prize.
    const survivors = evaluated.filter(p => valueCounts[p.value] === 1);
    if (survivors.length === 0) {
        return { winnerId: null, prizeCard: null, valueTiedCards, scoreTiedCards: [] };
    }

    // Winner = highest Value among survivors.
    survivors.sort((a, b) => b.value - a.value);
    const winnerEntry = survivors[0];

    // Prize = highest Score among non-winner survivors, with score-ties discarded.
    const prizePool = survivors.filter(p => p !== winnerEntry);
    const scoreCounts = {};
    prizePool.forEach(p => { scoreCounts[p.score] = (scoreCounts[p.score] || 0) + 1; });
    const scoreTiedCards   = prizePool.filter(p => scoreCounts[p.score] >= 2).map(p => p.card);
    const prizeCandidates  = prizePool.filter(p => scoreCounts[p.score] === 1);
    prizeCandidates.sort((a, b) => b.score - a.score);

    return {
        winnerId:       winnerEntry.player.id,
        prizeCard:      prizeCandidates[0]?.card || null,
        valueTiedCards,
        scoreTiedCards,
    };
}

/** AI Logic Helpers **/

/**
 * Select which opponent to reveal a card from.
 *
 * Easy  — random opponent.
 * Medium — opponent with fewest hidden cards (maximises known information).
 * Hard  — opponent currently leading on score (disrupt the leader).
 */
function selectAITarget(player, validTargets) {
    if (player.difficulty === 'Easy') {
        return validTargets[Math.floor(rng() * validTargets.length)].id;
    }
    if (player.difficulty === 'Medium') {
        return [...validTargets].sort((a, b) => a.hiddenHand.length - b.hiddenHand.length)[0].id;
    }
    // Hard: target the player with the highest score.
    return [...validTargets].sort((a, b) => b.score - a.score)[0].id;
}

/**
 * Select which card to play from validCards.
 *
 * Easy  — random.
 * Medium — highest play-score card (greedy).
 * Hard  —
 *   Leading : 2nd-best by face value (don't telegraph the strongest card).
 *   Following: spend the minimum play-score card that still wins;
 *              if none wins, discard the lowest face-value card
 *              (preserve prize potential).
 *
 * NOTE: winning cards are sorted by play score (hiddenScore / shownScore),
 * NOT face value.  A Shown King has play score 5 but face value 13 — it is
 * the cheapest possible winner and should be preferred over a 6♣ Hidden
 * (play score 6) even though its face value is higher.
 */
function selectAICardToPlay(player, validCards) {
    if (player.difficulty === 'Easy') {
        return validCards[Math.floor(rng() * validCards.length)];
    }

    // play-score helper: cards in hiddenHand use hiddenScore, others use shownScore.
    const getScore = (c) => player.hiddenHand.includes(c) ? c.hiddenScore : c.shownScore;

    if (player.difficulty === 'Medium') {
        return [...validCards].sort((a, b) => getScore(b) - getScore(a))[0];
    }

    // Hard — leading (play zone is empty)
    if (gameState.playZone.length === 0) {
        const sorted = [...validCards].sort((a, b) => b.faceValue - a.faceValue);
        return sorted.length > 1 ? sorted[1] : sorted[0];
    }

    // Hard — following
    const maxInPlay = Math.max(
        ...gameState.playZone.map(p =>
            p.section === 'Hidden' ? p.card.hiddenScore : p.card.shownScore)
    );
    const winningCards = validCards.filter(c => getScore(c) > maxInPlay);
    if (winningCards.length > 0) {
        // Cheapest winner by play score — preserves high face-value cards for prizes.
        return winningCards.sort((a, b) => getScore(a) - getScore(b))[0];
    }
    // Can't win — discard lowest face-value card.
    return [...validCards].sort((a, b) => a.faceValue - b.faceValue)[0];
}

// Bump when the save shape changes; loadSavedGame rejects other versions.
const SAVE_VERSION = 1;

function saveGame() {
    if (!gameState.isRunning) return;
    try {
        localStorage.setItem('cardGameState', JSON.stringify({
            version: SAVE_VERSION,
            players: gameState.players, round: gameState.round, phase: gameState.phase,
            starterId: gameState.starterId, playZone: gameState.playZone, discards: gameState.discards
        }));
    } catch (e) {
        // Quota exceeded or storage disabled — the game continues unsaved.
    }
}

function clearSavedGame() { localStorage.removeItem('cardGameState'); }

async function gameLoop() {
    while (gameState.isRunning && gameState.round <= 13) {
        saveGame();
        if (gameState.phase === 'Reveal') await executeRevealPhase();
        else if (gameState.phase === 'Play') await executePlayPhase();
        else if (gameState.phase === 'Determine') await executeDeterminePhase();
        else if (gameState.phase === 'Score') executeScorePhase();
    }
    if (gameState.isRunning) endGame();
}

async function executeRevealPhase() {
    updateStatus(`Phase: Reveal | Round: ${gameState.round}/13`);
    let starter = gameState.players[gameState.starterId];
    gameState.activePlayerId = starter.id;
    renderAll();
    let validTargets = gameState.players.filter(p => p.id !== starter.id && p.hasHidden);

    if (validTargets.length > 0) {
        let targetId = -1;
        if (starter.isHuman) {
            targetId = await requestHumanRevealTarget(validTargets);
            if(!gameState.isRunning) return;
        } else {
            targetId = selectAITarget(starter, validTargets);
            await delay(600);
        }
        let targetPlayer = gameState.players.find(p => p.id === targetId);
        let rIndex = Math.floor(rng() * targetPlayer.hiddenHand.length);
        let card = targetPlayer.hiddenHand.splice(rIndex, 1)[0];
        
        card.isRevealing = true; // Triggers CSS flip animation
        targetPlayer.shownHand.push(card);
        renderAll();
        await delay(600); 
        card.isRevealing = false;
        renderAll();
    }
    gameState.activePlayerId = null;
    renderAll();
    gameState.phase = 'Play';
}

async function executePlayPhase() {
    updateStatus(`Phase: Play | Round: ${gameState.round}/13`);
    let playOrder = [];
    for(let i=0; i<4; i++) playOrder.push(gameState.players[(gameState.starterId + i) % 4]);

    for(let i=0; i<4; i++) {
        if(!gameState.isRunning) return;
        let currentPlayer = playOrder[i];
        gameState.activePlayerId = currentPlayer.id;
        renderAll();
        let validCards = [...currentPlayer.hiddenHand, ...currentPlayer.shownHand];
        if (validCards.length === 0) continue;

        let playedCard = null;
        if (currentPlayer.isHuman) {
            playedCard = await requestHumanCardPlay(currentPlayer);
            if(!gameState.isRunning) return;
        } else {
            playedCard = selectAICardToPlay(currentPlayer, validCards);
            await delay(800);
        }
        let section = currentPlayer.removeCard(playedCard);
        gameState.playZone.push({ card: playedCard, player: currentPlayer, section: section });
        renderAll();
    }
    gameState.activePlayerId = null;
    renderAll();
    await delay(500);
    gameState.phase = 'Determine';
}

async function executeDeterminePhase() {
    updateStatus(`Phase: Determine | Round: ${gameState.round}/13`);

    const { winnerId, prizeCard, valueTiedCards, scoreTiedCards } = determineTrick(gameState.playZone);
    const winner = winnerId !== null
        ? gameState.players.find(p => p.id === winnerId)
        : null;
    const winnerCard = winner
        ? gameState.playZone.find(p => p.player.id === winnerId)?.card
        : null;

    // -- Step 1: cut cards eliminated by value tie --
    if (valueTiedCards.length > 0) {
        setPrompt(valueTiedCards.length >= 3
            ? `Three-way tie — trick is void.`
            : `Value tie — those cards are eliminated.`);
        valueTiedCards.forEach(c => decorateCard(c, 'cut'));
        await delay(650);
    }

    // -- Step 2: crown the winner --
    if (winner && winnerCard) {
        setPrompt(`${winner.name} wins the trick!`);
        decorateCard(winnerCard, 'crown');
        await delay(650);
    }

    // -- Step 3: cut score-tied prize candidates --
    if (scoreTiedCards.length > 0) {
        setPrompt(`Prize candidates tied — eliminated.`);
        scoreTiedCards.forEach(c => decorateCard(c, 'cut'));
        await delay(650);
    }

    // -- Step 4: coins on the prize card --
    if (prizeCard) {
        setPrompt(`${winner.name} claims ${prizeCard.name}!`);
        decorateCard(prizeCard, 'coins');
        await delay(500);
    } else if (winner) {
        setPrompt(`${winner.name} wins — no prize available.`);
    } else if (valueTiedCards.length < 3) {
        // Edge: two pair, no winner.
        setPrompt(`Tie! No one wins the trick.`);
    }

    // -- Wait for the user to click (or press Enter/Space) to continue --
    // The listeners are removed by finish() no matter HOW the wait ends —
    // real click, keypress, or a test/showMenu calling resolveInput directly.
    // Otherwise stale listeners pile up and could mis-fire into a later
    // resolveInput (e.g. a human card-play prompt).
    await new Promise(resolve => {
        let done = false;
        const finish = () => {
            if (done) return;
            done = true;
            document.removeEventListener('click', ack);
            document.removeEventListener('keydown', ack);
            if (gameState.resolveInput === finish) gameState.resolveInput = null;
            resolve();
        };
        const ack = (e) => {
            if (e.type === 'keydown' && e.key !== 'Enter' && e.key !== ' ') return;
            finish();
        };
        gameState.resolveInput = finish;
        document.addEventListener('click', ack);
        document.addEventListener('keydown', ack);
    });
    if (!gameState.isRunning) return;

    // -- After the click: prize flies to winner, then score increments --
    if (winner && prizeCard) {
        // Pre-rotate to the player who will lead next round (one seat
        // counter-clockwise from the winner). executeScorePhase will NOT
        // rotate again — this is the authoritative assignment for winner rounds.
        gameState.starterId = (winner.id + 3) % 4;
        const prizeEntry = gameState.playZone.find(p => p.card === prizeCard);
        const cardEl = document.querySelector(`#play-slot-${prizeEntry.player.id} .card`);
        if (cardEl) await animatePrizeToWinner(winner.id, cardEl);
        winner.scoringZone.push(prizeCard);
    } else if (winner) {
        // Winner but no prize: same rotation rule.
        gameState.starterId = (winner.id + 3) % 4;
    }
    // No else: no winner (all-tie) — starterId is intentionally left unchanged
    // so the same player leads the next round (house rule: tie = replay).

    setPrompt(null);
    gameState.playZone.forEach(p => { if (p.card !== prizeCard) gameState.discards.push(p.card); });
    gameState.playZone = [];
    renderAll();
    gameState.phase = 'Score';
}

function executeScorePhase() {
    gameState.round++;
    // starterId was already set correctly in executeDeterminePhase:
    //   • Winner present → (winner.id + 3) % 4  (one seat CCW from winner)
    //   • No winner (all tied) → unchanged (same player leads again)
    gameState.phase = 'Reveal';
}

// -------------------------------------------------------------------------
// Window exports for tests and cross-script access.
// Class declarations and `let` bindings don't auto-attach to window in a
// classic <script>, so we expose them explicitly. `gameState` is `var` above
// and is already on window; `window.gameState` and the bare `gameState`
// identifier therefore stay in sync when ui.js reassigns it.
// -------------------------------------------------------------------------
window.Suits              = Suits;
window.Ranks              = Ranks;
window.Card               = Card;
window.Deck               = Deck;
window.Player             = Player;
window.determineTrick     = determineTrick;
window.saveGame           = saveGame;
window.clearSavedGame     = clearSavedGame;
window.gameLoop           = gameLoop;
// AI helpers — exported so Playwright tests (ai.spec.js) can call them directly.
// These are the authoritative implementations; ai.js is now a stub.
window.selectAITarget     = selectAITarget;
window.selectAICardToPlay = selectAICardToPlay;
// Determinism hook for tests: setSeed(n) makes shuffle/reveal/Easy-AI
// reproducible; setSeed(null) restores Math.random.
window.setSeed            = setSeed;
window.SAVE_VERSION       = SAVE_VERSION;