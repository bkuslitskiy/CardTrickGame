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

        List<Card> cards = playedCards.ToList();
        List<Player> players = playersInOrder.ToList();

        var scoredCards = cards.Select((c, i) => new {
            Card = c,
            Player = players[i],
            Score = ScoreCard(c, gameState.GetCardSectionInPlayZone(c) ?? HandSection.Hidden)
        }).ToList();

        var scoreGroups = scoredCards.GroupBy(x => x.Score).ToList();

        if (scoreGroups.Any(g => g.Count() >= 3))
        {
            foreach (var card in cards)
                gameState.DiscardCard(card);
            return (null, null);
        }

        var untiedCards = new List<Card>();
        var untiedPlayers = new List<Player>();

        foreach (var group in scoreGroups)
        {
            if (group.Count() == 2)
            {
                foreach (var item in group)
                    gameState.DiscardCard(item.Card);
            }
            else
            {
                foreach (var item in group)
                {
                    untiedCards.Add(item.Card);
                    untiedPlayers.Add(item.Player);
                }
            }
        }

        if (untiedCards.Count == 0)
            return (null, null);

        var untiedScored = untiedCards.Select((c, i) => new {
            Card = c,
            Player = untiedPlayers[i],
            Score = ScoreCard(c, gameState.GetCardSectionInPlayZone(c) ?? HandSection.Hidden)
        }).ToList();

        int maxScore = untiedScored.Max(x => x.Score);
        var winner = untiedScored.First(x => x.Score == maxScore);

        gameState.DiscardCard(winner.Card);
        untiedCards.Remove(winner.Card);

        Card prizeCard = ResolvePrizeCard(untiedCards, gameState);
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
