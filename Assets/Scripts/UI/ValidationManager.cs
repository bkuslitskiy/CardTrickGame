using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates all player moves before they're executed.
/// Prevents invalid cards from being played and invalid targets from being selected.
/// </summary>
public class ValidationManager : MonoBehaviour
{
    public static ValidationManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    /// <summary>
    /// Validate that a card can be played during Play phase
    /// </summary>
    public bool IsValidCardPlay(Player player, Card card, GameState gameState)
    {
        if (player == null || card == null || gameState == null)
            return false;
        
        // Check if player has this card
        if (!player.Hand.Contains(card))
            return false;
        
        // Check game phase
        if (gameState.CurrentPhase != GamePhase.Play)
            return false;
        
        // Check if it's this player's turn
        // (This would be validated at a higher level, but included for safety)
        
        return true;
    }
    
    /// <summary>
    /// Validate that a card can be revealed during Reveal phase
    /// </summary>
    public bool IsValidCardReveal(Player revealingPlayer, Player targetPlayer, Card card, GameState gameState)
    {
        if (revealingPlayer == null || targetPlayer == null || card == null || gameState == null)
            return false;
        
        // Check game phase
        if (gameState.CurrentPhase != GamePhase.Reveal)
            return false;
        
        // Check that target is different from revealer
        if (revealingPlayer.ID == targetPlayer.ID)
            return false;
        
        // Check that target player has this card in hidden hand
        if (!targetPlayer.Hand.Contains(card))
            return false;
        
        // Check that card is in hidden hand (not shown)
        if (targetPlayer.Hand.IsCardShown(card))
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Validate that a target player can be revealed from
    /// </summary>
    public bool IsValidRevealTarget(Player targetPlayer, GameState gameState)
    {
        if (targetPlayer == null || gameState == null)
            return false;
        
        // Check game phase
        if (gameState.CurrentPhase != GamePhase.Reveal)
            return false;
        
        // Check that target has cards in hidden hand
        if (targetPlayer.Hand.GetHiddenCount() == 0)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Get all valid cards a player can play
    /// </summary>
    public List<Card> GetValidPlayCards(Player player, GameState gameState)
    {
        List<Card> validCards = new List<Card>();
        
        if (player == null || gameState == null || gameState.CurrentPhase != GamePhase.Play)
            return validCards;
        
        // Player can play any card from Hidden or Shown hand
        foreach (Card card in player.Hand.GetAllCards())
        {
            if (IsValidCardPlay(player, card, gameState))
                validCards.Add(card);
        }
        
        return validCards;
    }
    
    /// <summary>
    /// Get all valid targets for revealing
    /// </summary>
    public List<Player> GetValidRevealTargets(Player revealingPlayer, GameState gameState)
    {
        List<Player> validTargets = new List<Player>();
        
        if (revealingPlayer == null || gameState == null)
            return validTargets;
        
        foreach (Player player in gameState.GetAllPlayers())
        {
            if (player.ID != revealingPlayer.ID && IsValidRevealTarget(player, gameState))
                validTargets.Add(player);
        }
        
        return validTargets;
    }
    
    /// <summary>
    /// Get all valid cards that can be revealed from a target player
    /// </summary>
    public List<Card> GetValidRevealCards(Player revealingPlayer, Player targetPlayer, GameState gameState)
    {
        List<Card> validCards = new List<Card>();
        
        if (targetPlayer == null || gameState == null)
            return validCards;
        
        // Can reveal any card from target's hidden hand
        foreach (Card card in targetPlayer.Hand.GetHiddenCards())
        {
            if (IsValidCardReveal(revealingPlayer, targetPlayer, card, gameState))
                validCards.Add(card);
        }
        
        return validCards;
    }
    
    /// <summary>
    /// Check if a move would violate any game rules
    /// </summary>
    public string ValidateMove(Player player, Card card, GameState gameState)
    {
        if (player == null)
            return "Player is null";
        
        if (card == null)
            return "Card is null";
        
        if (gameState == null)
            return "Game state is null";
        
        switch (gameState.CurrentPhase)
        {
            case GamePhase.Play:
                if (!IsValidCardPlay(player, card, gameState))
                    return "Invalid card to play";
                break;
            case GamePhase.Reveal:
                return "Cannot play cards during Reveal phase";
            default:
                return $"Cannot play cards during {gameState.CurrentPhase} phase";
        }
        
        return ""; // Empty string = valid
    }
}
