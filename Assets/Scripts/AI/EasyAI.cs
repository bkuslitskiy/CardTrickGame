using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Easy AI: Makes random valid decisions.
/// </summary>
public class EasyAI : RuleBasedAI
{
    private Random _random;

    public EasyAI(int playerId) : base(playerId, Difficulty.Easy)
    {
        _random = new Random();
    }

    /// <summary>
    /// Select random valid player to reveal from.
    /// </summary>
    protected override int SelectTargetPlayerForReveal(GameState gameState, List<Player> validTargets)
    {
        if (validTargets.Count == 0)
            return 0;

        Player target = validTargets[_random.Next(validTargets.Count)];
        return target.ID;
    }

    /// <summary>
    /// Select random card from target's Hidden hand.
    /// </summary>
    protected override Card SelectCardForReveal(GameState gameState, Player targetPlayer, List<Card> validCards)
    {
        if (validCards.Count == 0)
            return null;

        return validCards[_random.Next(validCards.Count)];
    }

    /// <summary>
    /// Select random valid card to play.
    /// </summary>
    protected override Card SelectCardForPlay(GameState gameState, List<Card> validCards)
    {
        if (validCards.Count == 0)
            return null;

        return validCards[_random.Next(validCards.Count)];
    }
}
