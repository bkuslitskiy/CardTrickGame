using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles scene initialization and game startup flow.
/// Manages transitions between Menu → Game Board scenes.
/// Supports human players and AI opponents.
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private bool autoStartGame = false;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private float phaseDelaySeconds = 2f;
    
    private static Difficulty[] selectedDifficulties;
    private static int selectedHumanPlayer = -1;
    
    private void Awake()
    {
        // Initialize default difficulties if not set
        if (selectedDifficulties == null)
        {
            selectedDifficulties = new Difficulty[4]
            {
                Difficulty.Easy,
                Difficulty.Medium,
                Difficulty.Hard,
                Difficulty.Easy
            };
        }
    }
    
    private void Start()
    {
        InitializeGame();
    }
    
    /// <summary>
    /// Initialize the game with current difficulty settings and human player
    /// </summary>
    private void InitializeGame()
    {
        GameManagerController gameManager = GameManagerController.Instance;
        GameLoopManager gameLoop = GameLoopManager.Instance;
        InputManagerEnhanced inputManager = InputManagerEnhanced.Instance;
        ValidationManager validator = ValidationManager.Instance;
        
        if (gameManager == null)
        {
            Debug.LogError("GameManagerController not found!");
            return;
        }
        
        if (gameLoop == null)
        {
            Debug.LogError("GameLoopManager not found!");
            return;
        }
        
        // Initialize game with selected difficulties and human player
        gameManager.InitializeGame(selectedDifficulties, selectedHumanPlayer);
        gameManager.StartGame();
        
        // If human player is set, replace their AI with HumanPlayer wrapper
        if (selectedHumanPlayer >= 0 && selectedHumanPlayer < 4)
        {
            GameState gameState = gameManager.GetGameState();
            if (gameState != null)
            {
                Player humanPlayer = gameState.GetPlayer(selectedHumanPlayer + 1); // Convert 0-3 to 1-4
                if (humanPlayer != null)
                {
                    humanPlayer.AIImplementation = AIFactory.CreateHumanPlayer(selectedHumanPlayer + 1, gameManager);
                    Debug.Log($"Player {selectedHumanPlayer + 1} set as human player");
                }
            }
        }
        
        // Configure game loop
        gameLoop.SetPhaseDuration(phaseDelaySeconds);
        gameLoop.StartGameLoop();
        
        if (debugMode)
        {
            Debug.Log($"Game initialized:");
            Debug.Log($"  Difficulties: {string.Join(", ", selectedDifficulties)}");
            Debug.Log($"  Human Player: {(selectedHumanPlayer >= 0 ? $"Player {selectedHumanPlayer + 1}" : "None (All AI)")}");
            Debug.Log($"  Phase Delay: {phaseDelaySeconds}s");
        }
        
        if (autoStartGame && selectedHumanPlayer < 0)
        {
            // Run complete game only if no human player
            gameManager.RunCompleteGame();
        }
    }
    
    /// <summary>
    /// Set difficulties for next game (call from menu)
    /// </summary>
    public static void SetGameDifficulties(Difficulty[] difficulties)
    {
        if (difficulties != null && difficulties.Length == 4)
            selectedDifficulties = difficulties;
    }
    
    /// <summary>
    /// Set which player is human (0-3 = player 1-4, -1 = all AI)
    /// </summary>
    public static void SetHumanPlayer(int playerIndex)
    {
        if (playerIndex >= -1 && playerIndex < 4)
            selectedHumanPlayer = playerIndex;
    }
    
    /// <summary>
    /// Get current selected difficulties
    /// </summary>
    public static Difficulty[] GetGameDifficulties() => selectedDifficulties;
    
    /// <summary>
    /// Get current selected human player
    /// </summary>
    public static int GetHumanPlayer() => selectedHumanPlayer;
}
