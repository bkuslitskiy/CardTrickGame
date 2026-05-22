using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Encapsulates all game rules: scoring, winning determination, validation.
/// </summary>
public static class GameRules
{
    /// <summary>
    /// Score a card based on hand type it was played from.
    /// </summary>
    public static int ScoreCard(Card card, HandSection playedFromSection)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        return playedFromSection == HandSection.Hidden 
            ? card.GetHiddenHandScore() 
            : card.GetShownHandScore();
    }

    /// <summary>
    /// Determine winner of a trick given 4 played cards.
    /// Handles tie-breaking as per rules.
    /// Returns tuple of (winning player, prize card) or (null, null) if no winner.
    ///
    /// Perf note: scores are computed once and reused throughout — no second
    /// scoring pass. Allocations are kept to a minimum for RL simulation paths.
    /// </summary>
    public static (Player winner, Card prizeCard) DetermineTrickWinner(
        Card[] playedCards,
        Player[] playersInOrder,
        GameState gameState)
    {
        if (playedCards == null || playedCards.Length != 4)
            throw new ArgumentException("Must have exactly 4 cards", nameof(playedCards));
        if (playersInOrder == null || playersInOrder.Length != 4)
            throw new ArgumentException("Must have exactly 4 players", nameof(playersInOrder));

        // Score every card once and keep the result alongside its owner.
        // Using a small fixed-size array avoids heap-allocating a new List<T>.
        var scored = new (Card Card, Player Player, int Score)[4];
        for (int i = 0; i < 4; i++)
        {
            scored[i] = (
                playedCards[i],
                playersInOrder[i],
                ScoreCard(playedCards[i], gameState.GetCardSectionInPlayZone(playedCards[i]) ?? HandSection.Hidden)
            );
        }

        // Count occurrences of each score to detect ties without a GroupBy allocation.
        // A 3-or-4-way tie on any single score → discard all, no winner.
        for (int i = 0; i < 4; i++)
        {
            int count = 0;
            for (int j = 0; j < 4; j++)
                if (scored[j].Score == scored[i].Score) count++;
            if (count >= 3)
            {
                foreach (var entry in scored) gameState.DiscardCard(entry.Card);
                return (null, null);
            }
        }

        // Discard paired (2-way tied) cards, collect the untied survivors.
        // Reuse a small fixed-size buffer — max 4 untied entries.
        var untied = new (Card Card, Player Player, int Score)[4];
        int untiedCount = 0;

        for (int i = 0; i < 4; i++)
        {
            bool isPaired = false;
            for (int j = 0; j < 4; j++)
            {
                if (i != j && scored[i].Score == scored[j].Score)
                {
                    isPaired = true;
                    break;
                }
            }
            if (isPaired)
                gameState.DiscardCard(scored[i].Card);
            else
                untied[untiedCount++] = scored[i];
        }

        if (untiedCount == 0)
            return (null, null);

        // Find the highest-scoring untied card — that player wins.
        int bestIdx = 0;
        for (int i = 1; i < untiedCount; i++)
            if (untied[i].Score > untied[bestIdx].Score) bestIdx = i;

        var winner = untied[bestIdx];
        gameState.DiscardCard(winner.Card);

        // Remaining untied cards compete for the prize (highest face value wins).
        var remaining = new List<Card>(untiedCount - 1);
        for (int i = 0; i < untiedCount; i++)
            if (i != bestIdx) remaining.Add(untied[i].Card);

        Card prizeCard = ResolvePrizeCard(remaining, gameState);
        return (winner.Player, prizeCard);
    }

    private static Card ResolvePrizeCard(List<Card> remainingCards, GameState gameState)
    {
        while (remainingCards.Count > 0)
        {
            int maxPrizeScore = remainingCards.Max(c => c.GetFaceValue());
            var tiedPrizeCards = remainingCards.Where(c => c.GetFaceValue() == maxPrizeScore).ToList();
            
            if (tiedPrizeCards.Count == 1)
            {
                Card prizeCard = tiedPrizeCards[0];
                remainingCards.Remove(prizeCard);
                foreach (var leftover in remainingCards) gameState.DiscardCard(leftover);
                return prizeCard;
            }
            else
            {
                foreach (var tiedCard in tiedPrizeCards)
                {
                    gameState.DiscardCard(tiedCard);
                    remainingCards.Remove(tiedCard);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Validate that a player can reveal from a target player.
    /// </summary>
    public static bool IsValidReveal(Player revealingPlayer, Player targetPlayer, Card card, GameState gameState)
    {
        if (revealingPlayer == null || targetPlayer == null || card == null)
            return false;

        if (revealingPlayer == targetPlayer)
            return false; // Cannot reveal own card

        if (!targetPlayer.Hand.HasHiddenCards())
            return false; // Target must have Hidden cards

        if (!targetPlayer.Hand.Contains(card))
            return false; // Card must be in target's hand

        HandSection? section = targetPlayer.Hand.GetCardSection(card);
        return section == HandSection.Hidden; // Card must be in Hidden section
    }

    /// <summary>
    /// Validate that a player can play a specific card.
    /// </summary>
    public static bool IsValidCardPlay(Player player, Card card, GameState gameState)
    {
        if (player == null || card == null)
            return false;

        if (!player.Hand.Contains(card))
            return false;

        if (gameState.CurrentPhase != GamePhase.Play)
            return false;

        if (gameState.GetPlayZoneCount() >= 4)
            return false; // Already 4 cards played this round

        return true;
    }

    /// <summary>
    /// Check if all players have played a card this round.
    /// </summary>
    public static bool AllPlayersHavePlayed(GameState gameState)
    {
        return gameState.GetPlayZoneCount() == 4;
    }

    /// <summary>
    /// Check if any player has cards remaining.
    /// </summary>
    public static bool AnyPlayerHasCards(GameState gameState)
    {
        foreach (Player player in gameState.GetAllPlayers())
        {
            if (player.HasCards())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get sorted player scores for game end.
    /// </summary>
    public static List<(Player player, int score)> GetFinalScores(GameState gameState)
    {
        return gameState.GetAllPlayers()
            .Select(p => (p, p.GetScore()))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }
}
