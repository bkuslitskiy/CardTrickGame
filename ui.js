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

// Corrupt or missing stats must never throw — a parse error here would kill
// the DOMContentLoaded handler (drag-drop setup, resume button) with it.
function readStats() {
    try {
        const stats = JSON.parse(localStorage.getItem('cardGameStats'));
        if (stats && typeof stats.played === 'number' && typeof stats.wins === 'number'
            && typeof stats.highestScore === 'number') return stats;
    } catch (e) {}
    return { played: 0, wins: 0, highestScore: 0 };
}

function updateStatsUI() {
    let stats = readStats();
    document.getElementById('stats-display').innerText = `Games Played: ${stats.played} | Human Wins: ${stats.wins} | High Score: ${stats.highestScore}`;
}

function recordStats(winner, humanScore) {
    let stats = readStats();
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

// Validate the save before trusting it. A tampered or stale-schema payload
// must fail load (return false → fresh menu), not resume into a corrupt game.
function isValidSave(p) {
    if (!p || p.version !== window.SAVE_VERSION) return false;
    if (!Array.isArray(p.players) || p.players.length !== 4) return false;
    if (!Number.isInteger(p.round) || p.round < 1 || p.round > 13) return false;
    if (!Number.isInteger(p.starterId) || p.starterId < 0 || p.starterId > 3) return false;
    if (!['Reveal', 'Play', 'Determine', 'Score'].includes(p.phase)) return false;
    if (!Array.isArray(p.discards) || !Array.isArray(p.playZone)) return false;
    const validCard = c => c && Number.isInteger(c.rank) && c.rank >= 2 && c.rank <= 14
        && Object.values(Suits).includes(c.suit);
    if (!p.players.every(pl => pl && Number.isInteger(pl.id)
        && [pl.hiddenHand, pl.shownHand, pl.scoringZone].every(h => Array.isArray(h) && h.every(validCard)))) return false;
    if (!p.discards.every(validCard)) return false;
    // Every play-zone entry must reference a player that exists in the save.
    if (!p.playZone.every(pz => pz && validCard(pz.card)
        && ['Hidden', 'Shown'].includes(pz.section)
        && p.players.some(pl => pl.id === pz.player?.id))) return false;
    return true;
}

function loadSavedGame() {
    let saved = localStorage.getItem('cardGameState');
    if (!saved) return false;
    try {
        let p = JSON.parse(saved);
        if (!isValidSave(p)) return false;
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

/**
 * Start a new game.
 *
 * @param {Object}   [opts]
 * @param {number}   [opts.humanId=0]   Which seat (0–3) the human plays. Set
 *                                      to -1 (or any out-of-range value) for
 *                                      a fully autonomous AI-vs-AI demo run.
 *                                      Tests use humanId: -1 to avoid the
 *                                      Reveal-phase prompt blocking on a click.
 * @param {string[]} [opts.diffs]       Override AI difficulties [p0,p1,p2,p3].
 *                                      When omitted, picks up the dropdown
 *                                      values for AI seats and 'Easy' for the
 *                                      human seat (if it's been demoted to AI).
 */
async function startGame(opts = {}) {
    clearSavedGame();
    document.getElementById('menu-screen').style.display = 'none';
    document.getElementById('end-screen').style.display = 'none';
    document.getElementById('game-board').style.display = 'grid';

    const humanId = (opts.humanId === undefined) ? 0 : opts.humanId;
    const dropdownDiffs = [
        'Easy',                                       // p0 — no dropdown for the human seat
        document.getElementById('diff-1').value,
        document.getElementById('diff-2').value,
        document.getElementById('diff-3').value,
    ];
    const diffs = opts.diffs || dropdownDiffs;

    const seatName = ['South', 'West', 'North', 'East'];
    gameState = {
        players: seatName.map((name, i) => new Player(
            i,
            name,
            i === humanId,
            i === humanId ? 'Human' : (diffs[i] || 'Easy')
        )),
        activePlayerId: null,
        round: 1, phase: 'Reveal', starterId: 0,
        playZone: [], discards: [],
        isRunning: true, resolveInput: null,
    };
    let deck = new Deck();
    gameState.players.forEach(p => p.hiddenHand = deck.deal(13));
    renderAll();
    await gameLoop();
}

async function resumeGame() {
    if (loadSavedGame()) {
        document.getElementById('menu-screen').style.display = 'none'; document.getElementById('end-screen').style.display = 'none'; document.getElementById('game-board').style.display = 'grid';
        renderAll();
        await gameLoop();
    }
}

function endGame() {
    gameState.isRunning = false; let maxScore = -1; let winner = null;
    // Ties: strict > keeps the first player at the max score, so the human
    // (seat 0) wins any tie they are part of.
    gameState.players.forEach(p => { if(p.score > maxScore) { maxScore = p.score; winner = p; } });
    recordStats(winner, gameState.players[0].score); clearSavedGame();

    // Player names round-trip through localStorage saves — build the results
    // with textContent (never innerHTML) so a tampered save can't inject HTML.
    const results = document.getElementById('end-results');
    results.innerHTML = '';
    const banner = document.createElement('div');
    banner.style.cssText = 'font-size:32px; font-weight:bold; color:var(--accent); margin-bottom:20px;';
    banner.textContent = `WINNER: ${winner.name}`;
    results.appendChild(banner);
    gameState.players.forEach(p => {
        const row = document.createElement('div');
        row.textContent = `${p.name}: ${p.score} pts`;
        results.appendChild(row);
    });
    document.getElementById('end-screen').style.display = 'flex';
}

function updateStatus(text) { document.getElementById('status-text').innerText = text; }
// 'flex' rather than 'block': #prompt-overlay centres its text in a
// fixed-height pill (styles.css §12). .center-area permanently reserves that
// height, so toggling the prompt never moves the play zone.
function setPrompt(text) { const p = document.getElementById('prompt-overlay'); if(text) { p.innerText = text; p.style.display = 'flex'; } else p.style.display = 'none'; }

// True on touch/pen devices. HTML5 drag-and-drop never fires there, so the
// card-play prompt must not advertise dragging, and cards get the pointer
// drag path in createCardElement() instead.
function isCoarsePointer() {
    return typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;
}

function requestHumanRevealTarget(validTargets) {
    return new Promise(resolve => {
        setPrompt("Choose an opponent to reveal a card from."); gameState.resolveInput = resolve;
        const cleanup = () => validTargets.forEach(t => {
            let el = document.getElementById(`info-${t.id}`);
            el.classList.remove('selectable-player');
            el.onclick = null; el.onkeydown = null;
            el.removeAttribute('role'); el.removeAttribute('tabindex');
        });
        validTargets.forEach(target => {
            let infoDiv = document.getElementById(`info-${target.id}`); infoDiv.classList.add('selectable-player');
            const choose = () => {
                cleanup();
                setPrompt(null); let r = gameState.resolveInput; gameState.resolveInput = null; if(r) r(target.id);
            };
            infoDiv.onclick = choose;
            // Keyboard: opponent badges become focusable buttons during selection.
            infoDiv.setAttribute('role', 'button');
            infoDiv.tabIndex = 0;
            infoDiv.onkeydown = (e) => {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); choose(); }
            };
        });
    });
}

function requestHumanCardPlay(player) {
    return new Promise(resolve => {
        setPrompt(isCoarsePointer()
            ? "Select a card to play. Tap or Drag to Center."
            : "Select a card to play. Click or Drag to Center.");
        gameState.resolveInput = resolve;
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

// Inline SVGs used by decorateCard(). Both fit fully INSIDE the 64x90 card
// face — the card has overflow:hidden, so anything positioned outside the
// frame gets clipped (which is exactly what caused the crown's top half to
// disappear in earlier revisions).
const CROWN_SVG = `
<svg class="card-crown" viewBox="0 0 24 18" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <path d="M 2 15 L 4 6 L 8 11 L 12 3 L 16 11 L 20 6 L 22 15 Z"
        fill="#f4c542" stroke="#8b6914" stroke-width="0.8" stroke-linejoin="round"/>
  <rect x="1.5" y="14" width="21" height="2.6" fill="#e5b32a" stroke="#8b6914" stroke-width="0.7"/>
  <circle cx="4"  cy="5"   r="1.0" fill="#c0392b"/>
  <circle cx="12" cy="2.6" r="1.3" fill="#c0392b"/>
  <circle cx="20" cy="5"   r="1.0" fill="#c0392b"/>
</svg>`.trim();

const COINS_SVG = `
<svg class="card-coins" viewBox="0 0 28 26" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <ellipse cx="14" cy="24"  rx="13" ry="1.6" fill="#2c1f06" opacity="0.55"/>
  <ellipse cx="14" cy="22"  rx="12" ry="2.6" fill="#b8830f" stroke="#5a4310" stroke-width="0.7"/>
  <ellipse cx="11" cy="18"  rx="11" ry="2.6" fill="#f4c542" stroke="#8b6914" stroke-width="0.7"/>
  <ellipse cx="16" cy="14"  rx="11" ry="2.6" fill="#f4c542" stroke="#8b6914" stroke-width="0.7"/>
  <ellipse cx="12" cy="10"  rx="10" ry="2.4" fill="#f4c542" stroke="#8b6914" stroke-width="0.7"/>
  <ellipse cx="15" cy="6"   rx="9"  ry="2.2" fill="#f4c542" stroke="#8b6914" stroke-width="0.7"/>
  <ellipse cx="13" cy="2.5" rx="8"  ry="2.0" fill="#f4c542" stroke="#8b6914" stroke-width="0.7"/>
  <text x="13" y="3.6" text-anchor="middle" font-size="2.9" fill="#5a4310"
        font-family="Georgia, serif" font-weight="bold">$</text>
  <ellipse cx="3"  cy="23"  rx="2.8" ry="1.2" fill="#f4c542" stroke="#8b6914" stroke-width="0.5"/>
  <ellipse cx="25" cy="23"  rx="2.4" ry="1.1" fill="#f4c542" stroke="#8b6914" stroke-width="0.5"/>
</svg>`.trim();

/**
 * Apply a Determine-phase decoration to a card. Sets a flag on the Card
 * object (so subsequent re-renders preserve it) and patches the existing
 * DOM element in place (so the CSS transition / @keyframes animation plays
 * smoothly without rebuilding the slot).
 *
 *   type 'cut'   — value/score-tie elimination: dim + diagonal slash
 *   type 'crown' — winner indicator (gold crown above the card)
 *   type 'coins' — prize indicator (coin stack on the card)
 */
function decorateCard(card, type) {
    if (!card) return;
    if (type === 'cut')   card.isCut    = true;
    if (type === 'crown') card.hasCrown = true;
    if (type === 'coins') card.hasCoins = true;

    const entry = gameState.playZone.find(p => p.card === card);
    if (!entry) return;
    const slot = document.getElementById(`play-slot-${entry.player.id}`);
    const el   = slot?.querySelector('.card');
    if (!el) return;

    if (type === 'cut') {
        el.classList.add('cut');
    } else if (type === 'crown' && !el.querySelector('.card-crown')) {
        el.insertAdjacentHTML('beforeend', CROWN_SVG);
    } else if (type === 'coins' && !el.querySelector('.card-coins')) {
        el.insertAdjacentHTML('beforeend', COINS_SVG);
    }
}

/**
 * Touch/pen drag for a playable card.
 *
 * The desktop path uses HTML5 drag-and-drop (dragstart/drop), which browsers
 * never fire for touch input — so on a phone the card could only be tapped,
 * while the prompt advertised dragging. This adds the equivalent gesture on
 * top of Pointer Events, for coarse pointers only; mouse input keeps using
 * native DnD so nothing about the desktop behaviour changes.
 *
 * A drag that ends anywhere over the play zone plays the card. A press that
 * never moves past DRAG_SLOP is left alone, so the existing click handler
 * still gets it (via the synthesised click) and taps keep working.
 */
const DRAG_SLOP = 8; // px of movement before a press counts as a drag
function attachPointerDrag(el, card, clickCallback) {
    el.style.touchAction = 'none';  // stop the browser scrolling instead of dragging
    el.onpointerdown = (e) => {
        if (e.pointerType === 'mouse') return;   // native DnD handles mouse
        const startX = e.clientX, startY = e.clientY;
        let dragging = false;
        const zone = document.getElementById('play-zone');

        const move = (ev) => {
            if (!dragging) {
                if (Math.hypot(ev.clientX - startX, ev.clientY - startY) < DRAG_SLOP) return;
                dragging = true;
                // Capture keeps the gesture attached to the card once it
                // leaves the card's own box. It throws NotFoundError if the
                // pointer id is not active, which must not abort the drag —
                // the move/up listeners are on the element either way.
                try { el.setPointerCapture(ev.pointerId); } catch (_) {}
                zone.classList.add('active-drop');
            }
            el.style.transform = `translate(${ev.clientX - startX}px, ${ev.clientY - startY}px) scale(1.06)`;
            el.style.zIndex = '900';
            zone.classList.toggle('drag-over', overZone(ev, zone));
        };

        const up = (ev) => {
            el.removeEventListener('pointermove', move);
            el.removeEventListener('pointerup', up);
            el.removeEventListener('pointercancel', up);
            zone.classList.remove('active-drop', 'drag-over');
            if (!dragging) return;               // a tap — let onclick handle it
            el.style.transform = ''; el.style.zIndex = '';
            if (overZone(ev, zone) && clickCallback) clickCallback(card);
        };

        el.addEventListener('pointermove', move);
        el.addEventListener('pointerup', up);
        el.addEventListener('pointercancel', up);
    };
}

function overZone(ev, zone) {
    const b = zone.getBoundingClientRect();
    return ev.clientX >= b.left && ev.clientX <= b.right
        && ev.clientY >= b.top  && ev.clientY <= b.bottom;
}

const SUIT_NAMES = { '♣': 'clubs', '♦': 'diamonds', '♥': 'hearts', '♠': 'spades' };
function cardLabel(c, section) {
    return `${Ranks[c.rank]} of ${SUIT_NAMES[c.suit] || c.suit}${section === 'Shown' ? ' (shown)' : ''}`;
}

function createCardElement(c, isInteractive, clickCallback, isHidden, section) {
    let el = document.createElement('div');
    if (isHidden) { el.className = 'card back'; el.setAttribute('aria-hidden', 'true'); return el; }

    let isRevealing = c.isRevealing ? 'revealing' : '';
    let isShownClass = section === 'Shown' ? 'shown' : '';
    let interactiveClass = isInteractive ? 'clickable' : '';
    let cutClass = c.isCut ? 'cut' : '';
    el.className = `card ${c.colorClass} ${isShownClass} ${interactiveClass} ${isRevealing} ${cutClass}`.trim();

    let contentHTML = `
        <div class="card-top">${Ranks[c.rank]}<br>${c.suit}</div>
        <div class="card-center">${c.suit}</div>
        ${section === 'Shown' ? '<div class="card-modifier">SHOWN</div>' : ''}
        ${c.hasCrown ? CROWN_SVG : ''}
        ${c.hasCoins ? COINS_SVG : ''}
    `;

    if (isRevealing) { setTimeout(() => { el.innerHTML = contentHTML; }, 300); } else { el.innerHTML = contentHTML; }

    el.setAttribute('aria-label', cardLabel(c, section));

    if (isInteractive) {
        el.draggable = true;
        el.ondragstart = (e) => { e.dataTransfer.setData('application/json', JSON.stringify(c)); document.getElementById('play-zone').classList.add('active-drop'); };
        el.ondragend = (e) => { document.getElementById('play-zone').classList.remove('active-drop'); document.getElementById('play-zone').classList.remove('drag-over'); };
        el.onclick = () => { if(clickCallback) clickCallback(c); };
        attachPointerDrag(el, c, clickCallback);
        // Keyboard play: cards act as buttons (Tab to focus, Enter/Space to play).
        el.setAttribute('role', 'button');
        el.tabIndex = 0;
        el.setAttribute('aria-label', `Play ${cardLabel(c, section)}`);
        el.onkeydown = (e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); if (clickCallback) clickCallback(c); }
        };
    }
    return el;
}

function renderHand(player, isInteractive, clickCallback) {
    let hiddenDiv = document.getElementById(`hidden-${player.id}`); let shownDiv = document.getElementById(`shown-${player.id}`);
    hiddenDiv.innerHTML = ''; shownDiv.innerHTML = '';

    if (player.id === 1 || player.id === 3) { hiddenDiv.classList.add('vertical-hand'); shownDiv.classList.add('vertical-hand'); }
    else { hiddenDiv.classList.remove('vertical-hand'); shownDiv.classList.remove('vertical-hand'); }

    // --hand-n is the only thing JS tells CSS about layout: how many cards are
    // in this hand. styles.css §9 derives the overlap stride from it, so a
    // full 13-card hand compresses to fit the available band and a short hand
    // spreads back out — at every viewport, and on resize with no JS involved.
    hiddenDiv.style.setProperty('--hand-n', player.hiddenHand.length);
    shownDiv.style.setProperty('--hand-n', player.shownHand.length);

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
