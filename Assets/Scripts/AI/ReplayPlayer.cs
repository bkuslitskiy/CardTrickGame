using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Specialized AI that overrides its decisions by reading from the ReplayManager's history log.
/// </summary>
public class ReplayPlayer : IPlayer {
    private int _playerId;
    public ReplayPlayer(int id) { _playerId = id; }
    public void ObserveGameState(GameState gameState) {}

    public int SelectPlayerToRevealFrom(GameState gameState) {
        var action = ReplayManager.Instance.GetNextAction(_playerId, ReplayActionType.RevealTarget);
        return action?.ValueInt ?? gameState.GetValidRevealTargets(gameState.GetPlayer(_playerId)).FirstOrDefault()?.ID ?? 0;
    }

    public Card SelectCardToReveal(GameState gameState, int targetPlayerId) {
        var action = ReplayManager.Instance.GetNextAction(_playerId, ReplayActionType.RevealCard);
        if (action == null) return gameState.GetValidRevealCards(gameState.GetPlayer(targetPlayerId)).FirstOrDefault();
        return new Card(action.CardSuit, action.CardRank);
    }
    
    public Card SelectCardToPlay(GameState gameState) {
        var action = ReplayManager.Instance.GetNextAction(_playerId, ReplayActionType.PlayCard);
        if (action == null) return gameState.GetValidPlayCards(gameState.GetPlayer(_playerId)).FirstOrDefault();
        return new Card(action.CardSuit, action.CardRank);
    }
}