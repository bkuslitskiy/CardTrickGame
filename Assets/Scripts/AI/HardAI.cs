using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HardAI : RuleBasedAI
{
    public HardAI(int playerId) : base(playerId, Difficulty.Hard) { }

    protected override int SelectTargetPlayerForReveal(GameState gameState, List<Player> validTargets)
    {
        // Target the player currently in the lead to disrupt their strategy
        return validTargets.OrderByDescending(p => p.GetScore()).First().ID;
    }

    protected override Card SelectCardForReveal(GameState gameState, Player targetPlayer, List<Card> validCards)
    {
        return validCards[Random.Range(0, validCards.Count)];
    }

    protected override Card SelectCardForPlay(GameState gameState, List<Card> validCards)
    {
        var playZone = gameState.GetPlayZoneCards();
        Player me = gameState.GetPlayer(PlayerID);
        
        if (playZone.Count == 0) 
        {
            // Leading: play a mid-high face value (Score) card to bait out high Values from opponents.
            // We save our highest face values for prizes or safe wins later.
            var sorted = validCards.OrderByDescending(c => c.GetFaceValue()).ToList();
            return sorted.Count > 1 ? sorted[1] : sorted[0];
        }
        else
        {
            // Determine current winning play-score in the zone.
            int maxValueInPlay = playZone.Max(c => GameRules.ScoreCard(c, gameState.GetCardSectionInPlayZone(c) ?? HandSection.Hidden));

            // Find cards in hand that can beat the current winning play-score.
            var winningCards = validCards.Where(c => GameRules.ScoreCard(c, me.Hand.GetCardSection(c) ?? HandSection.Hidden) > maxValueInPlay).ToList();

            if (winningCards.Count > 0)
            {
                // Efficient play: use the lowest *play-score* card that still wins.
                // Sorting by play-score (not face value) is correct because a Shown King
                // has play-score 5 but face-value 13 — we'd rather spend it to win cheaply
                // than waste a hidden high card, and we'd never want to accidentally classify
                // it as an "expensive" win based on its prize value.
                return winningCards.OrderBy(c => GameRules.ScoreCard(c, me.Hand.GetCardSection(c) ?? HandSection.Hidden)).First();
            }

            // Can't win: throw the lowest *face-value* card to preserve prize potential.
            return validCards.OrderBy(c => c.GetFaceValue()).First();
        }
    }
}