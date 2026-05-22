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
        var validCards = gameState.GetValidRevealCards(gameState.GetPlayer(targetPlayerId));
        if (action == null) return validCards.FirstOrDefault();
        // Look up by suit+rank so we return the actual object in validCards,
        // not a new Card instance that fails reference-equality checks in GameManager.
        return validCards.FirstOrDefault(c => c.Suit == action.CardSuit && c.Rank == action.CardRank)
               ?? validCards.FirstOrDefault();
    }

    public Card SelectCardToPlay(GameState gameState) {
        var action = ReplayManager.Instance.GetNextAction(_playerId, ReplayActionType.PlayCard);
        var validCards = gameState.GetValidPlayCards(gameState.GetPlayer(_playerId));
        if (action == null) return validCards.FirstOrDefault();
        // Look up by suit+rank so we return the actual object in validCards,
        // not a new Card instance that fails reference-equality checks in GameManager.
        return validCards.FirstOrDefault(c => c.Suit == action.CardSuit && c.Rank == action.CardRank)
               ?? validCards.FirstOrDefault();
    }
}