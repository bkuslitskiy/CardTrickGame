using System;

/// <summary>
/// Factory for creating AI instances based on difficulty level.
/// Also supports creating Human player wrapper for UI input.
/// Centralizes player instantiation for easy swapping between implementations.
/// </summary>
public static class AIFactory
{
    /// <summary>
    /// Create player instance for given difficulty level or human.
    /// Pass playerId 1-4 for regular AI/human players.
    /// </summary>
    public static IPlayer CreateAI(int playerId, Difficulty difficulty)
    {
        if (playerId < 1 || playerId > 4)
            throw new ArgumentException("Player ID must be 1-4", nameof(playerId));

        return difficulty switch
        {
            Difficulty.Easy => new EasyAI(playerId),
            Difficulty.Medium => new MediumAI(playerId),
            Difficulty.Hard => new HardAI(playerId),
            _ => throw new ArgumentException($"Unknown difficulty: {difficulty}", nameof(difficulty))
        };
    }
    
    /// <summary>
    /// Create human player wrapper for UI input.
    /// </summary>
    public static IPlayer CreateHumanPlayer(int playerId, GameManagerController controller)
    {
        if (playerId < 1 || playerId > 4)
            throw new ArgumentException("Player ID must be 1-4", nameof(playerId));
        
        return new HumanPlayer(playerId, controller);
    }

    /// <summary>
    /// Get all available difficulty levels.
    /// </summary>
    public static Difficulty[] GetAvailableDifficulties()
    {
        return new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
    }

    /// <summary>
    /// Get human-readable name for difficulty level.
    /// </summary>
    public static string GetDifficultyName(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy => "Easy (Random)",
            Difficulty.Medium => "Medium (Basic Strategy)",
            Difficulty.Hard => "Hard (Advanced)",
            _ => "Unknown"
        };
    }
}
