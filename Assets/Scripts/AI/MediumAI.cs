using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MediumAI : RuleBasedAI
{
    public MediumAI(int playerId) : base(playerId, Difficulty.Medium) { }

    protected override int SelectTargetPlayerForReveal(GameState gameState, List<Player> validTargets)
    {
        // Target player with fewest hidden cards to limit their options
        return validTargets.OrderBy(p => p.Hand.GetHiddenCount()).First().ID;
    }

    protected override Card SelectCardForReveal(GameState gameState, Player targetPlayer, List<Card> validCards)
    {
        // Hidden cards are indistinguishable; pick random
        return validCards[Random.Range(0, validCards.Count)];
    }

    protected override Card SelectCardForPlay(GameState gameState, List<Card> validCards)
    {
        // Strategy: Prioritize winning the trick by playing the card with the highest current Value.
        // Medium AI understands the Value vs Score dichotomy but doesn't try to optimize the prize 
        // like Hard AI does; it just wants to win.
        Player me = gameState.GetPlayer(PlayerID);
        return validCards
            .OrderByDescending(c => GameRules.ScoreCard(c, me.Hand.GetCardSection(c) ?? HandSection.Hidden))
            .First();
    }
}