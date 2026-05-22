using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Base implementation for rule-based AI.
/// Provides common decision-making framework that subclasses customize via overrides.
/// Tracks game history for Medium/Hard AI to make informed decisions.
/// </summary>
public abstract class RuleBasedAI : IPlayer
{
    protected int PlayerID { get; private set; }
    protected Difficulty DifficultyLevel { get; private set; }
    /// <summary>
    /// Per-game history of observations for future AI implementations (e.g. discard
    /// tracking in Medium AI, pattern recognition in Hard AI).
    /// Lifecycle: AIFactory creates a fresh AI instance each game, so GameHistory is
    /// naturally cleared between games — no explicit reset needed.
    /// Within a single game it grows with each ObserveGameState call; future subclasses
    /// should trim or cap it if the dataset becomes large.
    /// </summary>
    protected List<GameSnapshot> GameHistory { get; private set; }

    public RuleBasedAI(int playerId, Difficulty difficulty)
    {
        if (playerId < 1 || playerId > 4)
            throw new ArgumentException("Player ID must be 1-4", nameof(playerId));

        PlayerID = playerId;
        DifficultyLevel = difficulty;
        GameHistory = new List<GameSnapshot>();
    }

    public int SelectPlayerToRevealFrom(GameState gameState)
    {
        List<Player> validTargets = gameState.GetValidRevealTargets(gameState.GetPlayer(PlayerID));

        if (validTargets.Count == 0)
            return 0;

        return SelectTargetPlayerForReveal(gameState, validTargets);
    }

    public Card SelectCardToReveal(GameState gameState, int targetPlayerId)
    {
        Player targetPlayer = gameState.GetPlayer(targetPlayerId);
        if (targetPlayer == null)
            return null;

        List<Card> validCards = gameState.GetValidRevealCards(targetPlayer);

        if (validCards.Count == 0)
            return null;

        return SelectCardForReveal(gameState, targetPlayer, validCards);
    }

    public Card SelectCardToPlay(GameState gameState)
    {
        List<Card> validCards = gameState.GetValidPlayCards(gameState.GetPlayer(PlayerID));

        if (validCards.Count == 0)
            return null;

        return SelectCardForPlay(gameState, validCards);
    }

    public virtual void ObserveGameState(GameState gameState)
    {
        // Subclasses can override to track game history
        GameHistory.Add(new GameSnapshot
        {
            Round = gameState.CurrentRound,
            Phase = gameState.CurrentPhase,
            PlayZoneCards = gameState.GetPlayZoneCards()
        });
    }

    /// <summary>
    /// Select which player to reveal from.
    /// Subclasses override for difficulty-specific logic.
    /// </summary>
    protected abstract int SelectTargetPlayerForReveal(GameState gameState, List<Player> validTargets);

    /// <summary>
    /// Select which card to reveal.
    /// Subclasses override for difficulty-specific logic.
    /// </summary>
    protected abstract Card SelectCardForReveal(GameState gameState, Player targetPlayer, List<Card> validCards);

    /// <summary>
    /// Select which card to play.
    /// Subclasses override for difficulty-specific logic.
    /// </summary>
    protected abstract Card SelectCardForPlay(GameState gameState, List<Card> validCards);

    /// <summary>
    /// Evaluate strategic value of a card in current game state.
    /// </summary>
    protected int EvaluateCard(Card card, HandSection playedFromSection, GameState gameState)
    {
        return GameRules.ScoreCard(card, playedFromSection);
    }

    /// <summary>
    /// Evaluate strategic value of revealing from a specific player.
    /// </summary>
    protected virtual int EvaluatePlayerForReveal(Player targetPlayer, GameState gameState)
    {
        // Base implementation: prefer players with more Hidden cards (they're "stronger")
        return targetPlayer.Hand.HiddenCount;
    }

    /// <summary>
    /// Get cards visible to this AI in their own hand.
    /// </summary>
    protected List<Card> GetOwnVisibleCards(GameState gameState)
    {
        Player self = gameState.GetPlayer(PlayerID);
        return self?.Hand.GetAllCards() ?? new List<Card>();
    }

    /// <summary>
    /// Snapshot of game state for history tracking.
    /// </summary>
    protected class GameSnapshot
    {
        public int Round { get; set; }
        public GamePhase Phase { get; set; }
        public List<Card> PlayZoneCards { get; set; }
    }
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}
