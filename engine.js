const Suits = { CLUBS: '♣', DIAMONDS: '♦', HEARTS: '♥', SPADES: '♠' };
const Ranks = { 2: '2', 3: '3', 4: '4', 5: '5', 6: '6', 7: '7', 8: '8', 9: '9', 10: '10', 11: 'J', 12: 'Q', 13: 'K', 14: 'A' };
const delay = ms => new Promise(res => setTimeout(res, ms));

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
            const j = Math.floor(Math.random() * (i + 1));
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

let gameState = { players: [], round: 1, phase: '', starterId: 0, playZone: [], discards: [], isRunning: false, resolveInput: null, activePlayerId: null };

/** AI Logic Helpers **/
function selectAITarget(player, validTargets) {
    // Medium/Hard AI: Target the player with the fewest cards in Hidden hand (more info known)
    if (player.difficulty === 'Easy') {
        return validTargets[Math.floor(Math.random() * validTargets.length)].id;
    }
    return [...validTargets].sort((a, b) => a.hiddenHand.length - b.hiddenHand.length)[0].id;
}

function selectAICardToPlay(player, validCards) {
    // Strategy based on Value (power) vs Score (points)
    if (player.difficulty === 'Easy') {
        return validCards[Math.floor(Math.random() * validCards.length)];
    }

    const evaluated = validCards.map(c => ({
        card: c,
        value: player.hiddenHand.includes(c) ? c.hiddenScore : c.shownScore,
        score: c.faceValue
    }));

    if (player.difficulty === 'Medium') {
        // Medium: Simply play the highest Value card
        evaluated.sort((a, b) => b.value - a.value);
    } else {
        // Hard: Prioritize winning tricks with high Values, but preserve high Scores 
        // if they don't think they can win the trick.
        evaluated.sort((a, b) => {
            if (b.value !== a.value) return b.value - a.value;
            return a.score - b.score;
        });
    }
    return evaluated[0].card;
}

function saveGame() {
    if (!gameState.isRunning) return;
    localStorage.setItem('cardGameState', JSON.stringify({
        players: gameState.players, round: gameState.round, phase: gameState.phase,
        starterId: gameState.starterId, playZone: gameState.playZone, discards: gameState.discards
    }));
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
        let rIndex = Math.floor(Math.random() * targetPlayer.hiddenHand.length);
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
    
    // Correct Trick Resolution Logic
    const determineTrickWinner = () => {
        if (gameState.playZone.length < 4) return { winner: null, prizeCard: null };

        // 1. Evaluate Value (for winner) and Score (for prize)
        const evaluated = gameState.playZone.map(p => ({
            ...p,
            value: p.section === 'Hidden' ? p.card.hiddenScore : p.card.shownScore,
            score: p.card.faceValue
        }));

        // 2. Identify Value Ties
        const valueCounts = {};
        evaluated.forEach(p => valueCounts[p.value] = (valueCounts[p.value] || 0) + 1);
        const survivors = evaluated.filter(p => valueCounts[p.value] === 1);

        if (survivors.length === 0) return { winner: null, prizeCard: null };

        // 3. Determine Winner (Highest remaining Value)
        survivors.sort((a, b) => b.value - a.value);
        const winnerEntry = survivors[0];

        // 4. Determine Prize (Remaining survivors excluding winner's card)
        const prizePool = survivors.filter(p => p !== winnerEntry);
        const scoreCounts = {};
        prizePool.forEach(p => scoreCounts[p.score] = (scoreCounts[p.score] || 0) + 1);
        const prizeCandidates = prizePool.filter(p => scoreCounts[p.score] === 1);

        prizeCandidates.sort((a, b) => b.score - a.score);
        return { winner: winnerEntry.player, prizeCard: prizeCandidates[0]?.card || null };
    };

    let { winner, prizeCard } = determineTrickWinner();

    if (winner && prizeCard) {
        winner.scoringZone.push(prizeCard);
        gameState.starterId = winner.id; 
        setPrompt(`${winner.name} wins the trick and claims ${prizeCard.name}!`);
        
        // Animate the prize card flying to the winner
        const prizeEntry = gameState.playZone.find(p => p.card === prizeCard);
        const cardEl = document.querySelector(`#play-slot-${prizeEntry.player.id} .card`);
        if (cardEl) await animatePrizeToWinner(winner.id, cardEl);
    } else {
        setPrompt(`Tie! No one wins the trick.`);
    }
    renderAll();
    
    await new Promise(resolve => {
        gameState.resolveInput = resolve;
        document.addEventListener('click', function ack() { document.removeEventListener('click', ack); if(gameState.resolveInput) { gameState.resolveInput(); gameState.resolveInput = null; } }, { once: true });
    });
    if(!gameState.isRunning) return;
    
    setPrompt(null);
    gameState.playZone.forEach(p => { if(p.card !== prizeCard) gameState.discards.push(p.card); });
    gameState.playZone = [];
    renderAll();
    gameState.phase = 'Score';
}

function executeScorePhase() {
    gameState.round++;
    gameState.starterId = (gameState.starterId + 3) % 4; // Counter-clockwise
    gameState.phase = 'Reveal';
}