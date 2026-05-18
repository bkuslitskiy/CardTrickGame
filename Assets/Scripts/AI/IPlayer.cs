/// <summary>
/// Interface for all player types (human, rule-based AI, ML AI).
/// Standardizes decision-making across all player types.
/// Enables seamless swapping between implementations at runtime.
/// </summary>
public interface IPlayer
{
    /// <summary>
    /// Called during Reveal phase.
    /// Player selects another player to reveal a card from.
    /// Must choose from players with Hidden cards available.
    /// </summary>
    /// <param name="gameState">Current game state (read-only)</param>
    /// <returns>Player ID (1-4) to reveal from, or 0 if none available</returns>
    int SelectPlayerToRevealFrom(GameState gameState);

    /// <summary>
    /// Called during Reveal phase.
    /// Player selects a card from target player's Hidden hand to reveal.
    /// </summary>
    /// <param name="gameState">Current game state (read-only)</param>
    /// <param name="targetPlayerId">ID of player to reveal from</param>
    /// <returns>Card to reveal, must be in target's Hidden hand</returns>
    Card SelectCardToReveal(GameState gameState, int targetPlayerId);

    /// <summary>
    /// Called during Play phase.
    /// Player selects a card from their hand (Hidden or Shown) to play.
    /// </summary>
    /// <param name="gameState">Current game state (read-only)</param>
    /// <returns>Card to play from this player's hand</returns>
    Card SelectCardToPlay(GameState gameState);

    /// <summary>
    /// Called to provide game state observations to AI.
    /// Used during training or logging for later analysis.
    /// </summary>
    /// <param name="gameState">Current game state after decision</param>
    void ObserveGameState(GameState gameState);
}
