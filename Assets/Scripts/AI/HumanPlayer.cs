using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Human player implementation of IPlayer interface.
/// Waits for input from InputManager instead of making AI decisions.
/// </summary>
public class HumanPlayer : IPlayer
{
    private int playerId;
    private InputManager inputManager;
    private GameManagerController gameManagerController;
    private bool awaitingInput = false;
    
    public HumanPlayer(int id, GameManagerController controller)
    {
        playerId = id;
        gameManagerController = controller;
    }
    
    public int SelectPlayerToRevealFrom(GameState gameState)
    {
        // Get list of valid targets
        List<int> validTargets = new List<int>();
        for (int i = 1; i <= 4; i++)
        {
            if (i != playerId)
            {
                Player targetPlayer = gameState.GetPlayer(i);
                if (targetPlayer != null && targetPlayer.Hand.GetHiddenCount() > 0)
                {
                    validTargets.Add(i);
                }
            }
        }
        
        if (validTargets.Count == 0)
            return -1; // No valid targets
        
        // Check for a pre-selected player (pre-requested by controller)
        if (InputManagerEnhanced.Instance != null)
        {
            var pre = InputManagerEnhanced.Instance.TryConsumeSelectedPlayer(playerId);
            if (pre != null && validTargets.Contains(pre.ID))
                return pre.ID;
        }

        // Wait for human input (UI will be shown by GameManagerController in most cases)
        int selectedPlayerId = -1;
        awaitingInput = true;

        InputManagerEnhanced.Instance.RequestPlayerSelection(gameState.GetPlayer(playerId), (selectedPlayer) =>
        {
            if (selectedPlayer != null && validTargets.Contains(selectedPlayer.ID))
            {
                selectedPlayerId = selectedPlayer.ID;
            }
            awaitingInput = false;
            gameManagerController?.OnHumanPlayerMoveConfirmed();
        });

        // Fallback: return first valid target if no selection made synchronously
        return selectedPlayerId >= 0 ? selectedPlayerId : (validTargets.Count > 0 ? validTargets[0] : -1);
    }
    
    public Card SelectCardToReveal(GameState gameState, int targetPlayerId)
    {
        Player targetPlayer = gameState.GetPlayer(targetPlayerId);
        if (targetPlayer == null || targetPlayer.Hand.GetHiddenCount() == 0)
            return null;
        
        List<Card> validCards = targetPlayer.Hand.GetHiddenCards();
        return validCards.Count > 0 ? validCards[UnityEngine.Random.Range(0, validCards.Count)] : null;
    }
    
    public Card SelectCardToPlay(GameState gameState)
    {
        Player thisPlayer = gameState.GetPlayer(playerId);
        if (thisPlayer == null)
            return null;
        
        // Get valid play cards
        List<Card> validCards = ValidationManager.Instance.GetValidPlayCards(thisPlayer, gameState);
        if (validCards.Count == 0)
            return null;
        
        // If a card was pre-selected via InputManager (pre-request), consume it first
        if (InputManagerEnhanced.Instance != null)
        {
            var pre = InputManagerEnhanced.Instance.TryConsumeSelectedCard(playerId);
            if (pre != null && validCards.Contains(pre))
                return pre;
        }

        Card selectedCard = null;
        awaitingInput = true;

        InputManagerEnhanced.Instance.RequestCardSelection(thisPlayer, (card) =>
        {
            if (validCards.Contains(card))
            {
                selectedCard = card;
            }
            awaitingInput = false;
            gameManagerController?.OnHumanPlayerMoveConfirmed();
        });

        // Return first valid card if no selection made synchronously
        return selectedCard ?? validCards.FirstOrDefault();
    }
    
    public void ObserveGameState(GameState gameState)
    {
        // Human player doesn't need to observe state for learning
        // (Unlike AI, which would use this for strategy updates)
    }
    
    public bool IsAwaitingInput => awaitingInput;
}
