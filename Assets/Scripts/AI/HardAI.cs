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
            // Determine current winning Value (dynamic power) in the play zone
            int maxValueInPlay = playZone.Max(c => GameRules.ScoreCard(c, gameState.GetCardSectionInPlayZone(c) ?? HandSection.Hidden));
            
            // Find cards in hand that can beat the current winning Value
            var winningCards = validCards.Where(c => GameRules.ScoreCard(c, me.Hand.GetCardSection(c) ?? HandSection.Hidden) > maxValueInPlay).ToList();
            
            if (winningCards.Count > 0)
            {
                // Efficient play: Play the card with the lowest face value (Score) that still wins the trick.
                // This preserves high-Score cards for when we are likely to lose or need a better prize.
                return winningCards.OrderBy(c => c.GetFaceValue()).First();
            }
            
            // Can't win: discard the lowest face value (Score) card to minimize loss.
            return validCards.OrderBy(c => c.GetFaceValue()).First();
        }
    }
}