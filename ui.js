document.addEventListener("DOMContentLoaded", () => {
    updateStatsUI();
    if (localStorage.getItem('cardGameState')) document.getElementById('resume-btn').style.display = 'inline-block';
    
    // Drag and Drop Play Zone Setup
    const pz = document.getElementById('play-zone');
    pz.addEventListener('dragover', (e) => { e.preventDefault(); pz.classList.add('drag-over'); });
    pz.addEventListener('dragleave', (e) => { pz.classList.remove('drag-over'); });
    pz.addEventListener('drop', (e) => {
        e.preventDefault(); pz.classList.remove('drag-over'); pz.classList.remove('active-drop');
        const data = e.dataTransfer.getData('application/json');
        if(!data) return;
        const cardData = JSON.parse(data);
        if (gameState.resolveInput) {
            let p = gameState.players[0]; // Human is always 0
            let matched = [...p.hiddenHand, ...p.shownHand].find(x => x.suit === cardData.suit && x.rank === cardData.rank);
            if (matched) { let r = gameState.resolveInput; gameState.resolveInput = null; r(matched); }
        }
    });
});

function updateStatsUI() {
    let stats = JSON.parse(localStorage.getItem('cardGameStats')) || { played: 0, wins: 0, highestScore: 0 };
    document.getElementById('stats-display').innerText = `Games Played: ${stats.played} | Human Wins: ${stats.wins} | High Score: ${stats.highestScore}`;
}

function recordStats(winner, humanScore) {
    let stats = JSON.parse(localStorage.getItem('cardGameStats')) || { played: 0, wins: 0, highestScore: 0 };
    stats.played++;
    if (winner.id === 0) stats.wins++;
    if (humanScore > stats.highestScore) stats.highestScore = humanScore;
    localStorage.setItem('cardGameStats', JSON.stringify(stats));
    updateStatsUI();
}

function hydrateCard(c) { return new Card(c.suit, c.rank); }
function hydratePlayer(p) {
    let np = new Player(p.id, p.name, p.isHuman, p.difficulty);
    np.hiddenHand = p.hiddenHand.map(hydrateCard);
    np.shownHand = p.shownHand.map(hydrateCard);
    np.scoringZone = p.scoringZone.map(hydrateCard);
    return np;
}

function loadSavedGame() {
    let saved = localStorage.getItem('cardGameState');
    if (!saved) return false;
    try {
        let p = JSON.parse(saved);
        gameState.players = p.players.map(hydratePlayer);
        gameState.round = p.round; gameState.phase = p.phase; gameState.starterId = p.starterId;
        gameState.discards = p.discards.map(hydrateCard);
        gameState.playZone = p.playZone.map(pz => ({ card: hydrateCard(pz.card), player: gameState.players.find(pl => pl.id === pz.player.id), section: pz.section }));
        gameState.isRunning = true; gameState.resolveInput = null;
        return true;
    } catch (e) { return false; }
}

function showMenu() {
    gameState.isRunning = false;
    if(gameState.resolveInput) { gameState.resolveInput(null); gameState.resolveInput = null; }
    document.getElementById('menu-screen').style.display = 'flex';
    document.getElementById('game-board').style.display = 'none';
    document.getElementById('end-screen').style.display = 'none';
    if (localStorage.getItem('cardGameState')) document.getElementById('resume-btn').style.display = 'inline-block';
}

function showRules() { document.getElementById('rules-screen').style.display = 'flex'; }
function hideRules() { document.getElementById('rules-screen').style.display = 'none'; }

async function startGame() {
    clearSavedGame();
    document.getElementById('menu-screen').style.display = 'none'; document.getElementById('end-screen').style.display = 'none'; document.getElementById('game-board').style.display = 'block';
    let diffs = [ 'Human', document.getElementById('diff-1').value, document.getElementById('diff-2').value, document.getElementById('diff-3').value ];
    gameState = {
        players: [ new Player(0, 'South', true), new Player(1, 'West', false, diffs[1]), new Player(2, 'North', false, diffs[2]), new Player(3, 'East', false, diffs[3]) ], activePlayerId: null,
        round: 1, phase: 'Reveal', starterId: 0, playZone: [], discards: [], isRunning: true, resolveInput: null
    };
    let deck = new Deck();
    gameState.players.forEach(p => p.hiddenHand = deck.deal(13));
    renderAll();
    await gameLoop();
}

async function resumeGame() {
    if (loadSavedGame()) {
        document.getElementById('menu-screen').style.display = 'none'; document.getElementById('end-screen').style.display = 'none'; document.getElementById('game-board').style.display = 'block';
        renderAll();
        await gameLoop();
    }
}

function endGame() {
    gameState.isRunning = false; let resultsHTML = ''; let maxScore = -1; let winner = null;
    gameState.players.forEach(p => { if(p.score > maxScore) { maxScore = p.score; winner = p; } resultsHTML += `<div>${p.name}: ${p.score} pts</div>`; });
    recordStats(winner, gameState.players[0].score); clearSavedGame();
    resultsHTML = `<div style="font-size:32px; font-weight:bold; color:var(--accent); margin-bottom:20px;">WINNER: ${winner.name}</div>` + resultsHTML;
    document.getElementById('end-results').innerHTML = resultsHTML; document.getElementById('end-screen').style.display = 'flex';
}

function updateStatus(text) { document.getElementById('status-text').innerText = text; }
function setPrompt(text) { const p = document.getElementById('prompt-overlay'); if(text) { p.innerText = text; p.style.display = 'block'; } else p.style.display = 'none'; }

function requestHumanRevealTarget(validTargets) {
    return new Promise(resolve => {
        setPrompt("Choose an opponent to reveal a card from."); gameState.resolveInput = resolve;
        validTargets.forEach(target => {
            let infoDiv = document.getElementById(`info-${target.id}`); infoDiv.classList.add('selectable-player');
            infoDiv.onclick = () => {
                validTargets.forEach(t => { let el = document.getElementById(`info-${t.id}`); el.classList.remove('selectable-player'); el.onclick = null; });
                setPrompt(null); let r = gameState.resolveInput; gameState.resolveInput = null; if(r) r(target.id);
            };
        });
    });
}

function requestHumanCardPlay(player) {
    return new Promise(resolve => {
        setPrompt("Select a card to play. Click or Drag to Center."); gameState.resolveInput = resolve;
        renderHand(player, true, (card) => { renderHand(player, false, null); setPrompt(null); let r = gameState.resolveInput; gameState.resolveInput = null; if(r) r(card); });
    });
}

function renderAll() {
    gameState.players.forEach(p => {
        const infoDiv = document.getElementById(`info-${p.id}`);
        infoDiv.innerText = `${p.name} | Score: ${p.score}`;
        
        // Highlight active turn
        if (gameState.activePlayerId === p.id) infoDiv.classList.add('active-turn');
        else infoDiv.classList.remove('active-turn');

        renderHand(p, gameState.resolveInput !== null && p.id === 0 && gameState.phase === 'Play', (card) => { if(gameState.resolveInput) { let r = gameState.resolveInput; gameState.resolveInput = null; r(card); } });
    });
    renderPlayZone();
}

function createCardElement(c, isInteractive, clickCallback, isHidden, section) {
    let el = document.createElement('div');
    if (isHidden) { el.className = 'card back'; return el; }

    let isRevealing = c.isRevealing ? 'revealing' : '';
    let isShownClass = section === 'Shown' ? 'shown' : '';
    let interactiveClass = isInteractive ? 'clickable' : '';
    el.className = `card ${c.colorClass} ${isShownClass} ${interactiveClass} ${isRevealing}`;
    
    let contentHTML = `
        <div class="card-top">${Ranks[c.rank]}<br>${c.suit}</div>
        <div class="card-center">${c.suit}</div>
        ${section === 'Shown' ? '<div class="card-modifier">SHOWN</div>' : ''}
    `;
    
    if (isRevealing) { setTimeout(() => { el.innerHTML = contentHTML; }, 300); } else { el.innerHTML = contentHTML; }

    if (isInteractive) {
        el.draggable = true;
        el.ondragstart = (e) => { e.dataTransfer.setData('application/json', JSON.stringify(c)); document.getElementById('play-zone').classList.add('active-drop'); };
        el.ondragend = (e) => { document.getElementById('play-zone').classList.remove('active-drop'); document.getElementById('play-zone').classList.remove('drag-over'); };
        el.onclick = () => { if(clickCallback) clickCallback(c); };
    }
    return el;
}

function renderHand(player, isInteractive, clickCallback) {
    let hiddenDiv = document.getElementById(`hidden-${player.id}`); let shownDiv = document.getElementById(`shown-${player.id}`);
    hiddenDiv.innerHTML = ''; shownDiv.innerHTML = '';

    if (player.id === 1 || player.id === 3) { hiddenDiv.classList.add('vertical-hand'); shownDiv.classList.add('vertical-hand'); } 
    else { hiddenDiv.classList.remove('vertical-hand'); shownDiv.classList.remove('vertical-hand'); }

    player.hiddenHand.sort((a,b) => b.faceValue - a.faceValue);
    player.hiddenHand.forEach(c => {
        let isHidden = !player.isHuman && !c.isRevealing;
        let el = createCardElement(c, isInteractive, clickCallback, isHidden, 'Hidden');
        hiddenDiv.appendChild(el);
    });

    player.shownHand.sort((a,b) => b.faceValue - a.faceValue);
    player.shownHand.forEach(c => {
        let el = createCardElement(c, isInteractive, clickCallback, false, 'Shown');
        shownDiv.appendChild(el);
    });
}

function renderPlayZone() {
    for(let i=0; i<4; i++) { document.getElementById(`play-slot-${i}`).innerHTML = ''; }
    gameState.playZone.forEach(play => {
        let slot = document.getElementById(`play-slot-${play.player.id}`);
        let el = createCardElement(play.card, false, null, false, play.section);
        el.style.position = 'absolute';
        slot.appendChild(el);
    });
}

async function animatePrizeToWinner(winnerId, cardElement) {
    if (!cardElement) return;
    const target = document.getElementById(`info-${winnerId}`);
    const startRect = cardElement.getBoundingClientRect();
    const endRect = target.getBoundingClientRect();

    const deltaX = (endRect.left + endRect.width / 2) - (startRect.left + startRect.width / 2);
    const deltaY = (endRect.top + endRect.height / 2) - (startRect.top + startRect.height / 2);

    cardElement.style.transition = "transform 0.7s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.7s ease-in";
    cardElement.style.zIndex = "1000";
    cardElement.style.pointerEvents = "none";
    cardElement.style.transform = `translate(${deltaX}px, ${deltaY}px) scale(0.3) rotate(15deg)`;
    cardElement.style.opacity = "0";

    await delay(700);
}
