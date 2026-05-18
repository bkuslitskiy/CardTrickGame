function selectAITarget(aiPlayer, validTargets) {
    if (aiPlayer.difficulty === 'Easy') {
        return validTargets[Math.floor(Math.random() * validTargets.length)].id;
    } else if (aiPlayer.difficulty === 'Medium') {
        validTargets.sort((a,b) => a.hiddenHand.length - b.hiddenHand.length);
        return validTargets[0].id;
    } else { 
        validTargets.sort((a,b) => b.score - a.score);
        return validTargets[0].id;
    }
}

function selectAICardToPlay(aiPlayer, validCards) {
    if (aiPlayer.difficulty === 'Easy') {
        return validCards[Math.floor(Math.random() * validCards.length)];
    } else if (aiPlayer.difficulty === 'Medium') {
        const getScore = (c) => aiPlayer.hiddenHand.includes(c) ? c.hiddenScore : c.shownScore;
        validCards.sort((a,b) => getScore(b) - getScore(a));
        return validCards[0];
    } else { 
        const getScore = (c) => aiPlayer.hiddenHand.includes(c) ? c.hiddenScore : c.shownScore;
        if (gameState.playZone.length === 0) {
            let sorted = [...validCards].sort((a,b) => b.faceValue - a.faceValue);
            return sorted.length > 1 ? sorted[1] : sorted[0];
        } else {
            let maxInPlay = Math.max(...gameState.playZone.map(p => p.section === 'Hidden' ? p.card.hiddenScore : p.card.shownScore));
            let winningCards = validCards.filter(c => getScore(c) > maxInPlay);
            if (winningCards.length > 0) {
                winningCards.sort((a,b) => a.faceValue - b.faceValue);
                return winningCards[0];
            }
            let sorted = [...validCards].sort((a,b) => a.faceValue - b.faceValue);
            return sorted[0];
        }
    }
}

function determineTrickWinner() {
    let plays = [...gameState.playZone];
    const getValue = (play) => play.section === 'Hidden' ? play.card.hiddenScore : play.card.shownScore;
    let valueCounts = {};
    plays.forEach(p => { let v = getValue(p); valueCounts[v] = (valueCounts[v] || 0) + 1; });
    let untiedPlays = [];
    plays.forEach(p => { let v = getValue(p); if (valueCounts[v] > 1) { gameState.discards.push(p.card); } else { untiedPlays.push(p); } });
    if (untiedPlays.length === 0) return { winner: null, prizeCard: null };
    let maxValue = Math.max(...untiedPlays.map(getValue));
    let winnerPlay = untiedPlays.find(p => getValue(p) === maxValue);
    gameState.discards.push(winnerPlay.card);
    untiedPlays = untiedPlays.filter(p => p !== winnerPlay);
    return { winner: winnerPlay.player, prizeCard: resolvePrizeCard(untiedPlays) };
}

function resolvePrizeCard(remainingPlays) {
    let plays = [...remainingPlays];
    while(plays.length > 0) {
        let maxScore = Math.max(...plays.map(p => p.card.faceValue));
        let ties = plays.filter(p => p.card.faceValue === maxScore);
        if (ties.length === 1) {
            let prize = ties[0].card;
            plays.filter(p => p !== ties[0]).forEach(p => gameState.discards.push(p.card));
            return prize;
        } else {
            ties.forEach(t => { gameState.discards.push(t.card); plays = plays.filter(p => p !== t); });
        }
    }
    return null;
}