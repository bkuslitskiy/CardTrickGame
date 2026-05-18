using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Unity MonoBehaviour wrapper for GameManager logic.
/// Integrates game logic with UI event system.
/// Supports human players and AI opponents.
/// </summary>
public class GameManagerController : MonoBehaviour
{
    private GameManager gameManager;
    private Difficulty[] currentDifficulties;
    private int humanPlayerId = -1; // -1 means all AI, 0-3 means that player index is human
    
    public static GameManagerController Instance { get; private set; }
    public Difficulty[] CurrentDifficulties => currentDifficulties;
    public int HumanPlayerId => humanPlayerId;
    
    // Events for UI
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int> OnRoundStarted;
    public event Action<Player> OnRevealCard;
    public event Action<Card, int, HandSection> OnCardPlayed; // (Card, PlayerId, Section)
    public event Action<Player, Card> OnTrickWon;
    public event Action OnGameComplete;
    
    public GameState CurrentGameState => gameManager?.CurrentGameState;
    
    // Track whether we're in input wait mode
    private bool waitingForHumanInput = false;
    private float inputTimeoutCounter = 0f;
    private const float INPUT_TIMEOUT = 60f; // 60 second timeout
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Update()
    {
        // Check for input timeout
        if (waitingForHumanInput)
        {
            inputTimeoutCounter += Time.deltaTime;
            if (inputTimeoutCounter > INPUT_TIMEOUT)
            {
                Debug.LogWarning("Human input timeout - no selection made");
                waitingForHumanInput = false;
                inputTimeoutCounter = 0f;
                // Continue with next phase
            }
        }
    }
    
    /// <summary>
    /// Initialize game with difficulty settings
    /// </summary>
    public void InitializeGame(Difficulty[] difficulties, int humanPlayer = -1)
    {
        if (difficulties == null || difficulties.Length != 4)
        {
            Debug.LogError("Must specify 4 difficulties!");
            return;
        }
        
        currentDifficulties = difficulties;
        humanPlayerId = humanPlayer;
        waitingForHumanInput = false;
        inputTimeoutCounter = 0f;
        gameManager = new GameManager(difficulties);
        
        // Subscribe to GameManager events
        gameManager.OnPhaseChanged += (state) => 
        {
            OnPhaseChanged?.Invoke(state.CurrentPhase);
            HandlePhaseChange(state.CurrentPhase);
        };
        gameManager.OnRoundStarted += (state) => OnRoundStarted?.Invoke(state.CurrentRound);
        gameManager.OnRevealCard += (player) => OnRevealCard?.Invoke(player);
        gameManager.OnCardPlayed += (card, player, section) => OnCardPlayed?.Invoke(card, player.ID, section);
        gameManager.OnTrickWon += (player, card) => OnTrickWon?.Invoke(player, card);
        gameManager.OnGameComplete += (state) => 
        {
            GameLoopManager.Instance?.StopGameLoop();
            OnGameComplete?.Invoke();
        };
    }
    
    /// <summary>
    /// Handle phase transitions (check if human input is needed)
    /// </summary>
    private void HandlePhaseChange(GamePhase phase)
    {
        // Input prompting is now handled dynamically and just-in-time by PrepareForPhaseIfHuman.
        // Removed pre-request logic here to prevent race conditions and duplicate callbacks.
    }
    
    /// <summary>
    /// Start a new game
    /// </summary>
    public void StartGame()
    {
        if (gameManager == null)
        {
            Debug.LogError("Game not initialized!");
            return;
        }

        gameManager.StartGame();

        if (humanPlayerId >= 0 && !(ReplayManager.Instance != null && ReplayManager.Instance.IsReplaying))
        {
            var state = gameManager.CurrentGameState;
            if (state != null)
            {
                Player p = state.GetPlayer(humanPlayerId + 1);
                if (p != null)
                {
                    p.AIImplementation = new HumanPlayer(humanPlayerId + 1, this);
                    Debug.Log($"GameManagerController: Assigned HumanPlayer to Player {p.ID}");
                }
            }
        }
    }

    public void StartReplay()
    {
        if (ReplayManager.Instance == null || ReplayManager.Instance.LastRecord == null) return;
        ReplayManager.Instance.SetupReplay();
        var rec = ReplayManager.Instance.CurrentRecord;
        InitializeGame(rec.Difficulties, rec.HumanId);
        StartGame();
        
        GameLoopManager.Instance?.SetPhaseDuration(0.3f); // Fast forward playback
        GameLoopManager.Instance?.StartGameLoop();
    }

    /// <summary>
    /// Prepare and pre-request human input if the current phase requires it.
    /// Returns true when human input was requested and the caller should delay execution.
    /// </summary>
    public bool PrepareForPhaseIfHuman()
    {
        if (ReplayManager.Instance != null && ReplayManager.Instance.IsReplaying)
            return false;

        if (waitingForHumanInput)
        {
            return true;
        }

        if (humanPlayerId < 0 || humanPlayerId >= 4)
            return false;

        GameState state = gameManager?.CurrentGameState;
        if (state == null)
            return false;

        Player humanPlayer = state.GetPlayer(humanPlayerId + 1);
        if (humanPlayer == null)
            return false;

        Debug.Log($"GameManagerController: PrepareForPhaseIfHuman called for Player {humanPlayer.ID} in phase {state.CurrentPhase}");

        switch (state.CurrentPhase)
        {
            case GamePhase.Play:
                var playOrder = state.GetPlayOrder(state.GetRoundStarterPlayer());
                var playZoneCards = state.GetPlayZoneCards();
                if (playZoneCards.Count < 4 && playOrder[playZoneCards.Count] == humanPlayer)
                {
                    if (InputManagerEnhanced.Instance != null && InputManagerEnhanced.Instance.HasPendingCardSelection(humanPlayer.ID))
                        return false;

                    var validPlay = ValidationManager.Instance != null ? ValidationManager.Instance.GetValidPlayCards(humanPlayer, state) : new System.Collections.Generic.List<Card>();
                    Debug.Log($"GameManagerController: PrepareForPhaseIfHuman - valid play count for Player {humanPlayer.ID}: {validPlay.Count}");

                    // Only request human input if UI and input systems are present and there are valid options
                    if (InputManagerEnhanced.Instance != null && UIManager.Instance != null && validPlay.Count > 0)
                    {
                        waitingForHumanInput = true;
                        inputTimeoutCounter = 0f;
                        Debug.Log($"GameManagerController: Requesting play selection from Player {humanPlayer.ID} (valid {validPlay.Count} cards)");
                        try { InputManagerEnhanced.Instance.RequestCardSelection(humanPlayer, (c) => { 
                            Debug.Log("GameManagerController: Card selected by human");
                            OnHumanPlayerMoveConfirmed();
                        }); } catch (Exception ex) { Debug.LogWarning($"RequestCardSelection failed: {ex}"); }
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("GameManagerController: Cannot request play selection - missing InputManager/UIManager or no valid plays. Continuing without pause.");
                        return false;
                    }
                }
                break;
            case GamePhase.Reveal:
                if (humanPlayer == state.GetRoundStarterPlayer())
                {
                    if (InputManagerEnhanced.Instance != null && InputManagerEnhanced.Instance.HasPendingPlayerSelection(humanPlayer.ID))
                        return false;

                    var validTargets = ValidationManager.Instance != null ? ValidationManager.Instance.GetValidRevealTargets(humanPlayer, state) : new System.Collections.Generic.List<Player>();
                    Debug.Log($"GameManagerController: PrepareForPhaseIfHuman - valid reveal targets for Player {humanPlayer.ID}: {validTargets.Count}");

                    if (InputManagerEnhanced.Instance != null && UIManager.Instance != null && validTargets.Count > 0)
                    {
                        waitingForHumanInput = true;
                        inputTimeoutCounter = 0f;
                        Debug.Log($"GameManagerController: Requesting reveal target selection from Player {humanPlayer.ID} (valid {validTargets.Count} players)");
                        try { 
                            InputManagerEnhanced.Instance.RequestPlayerSelection(humanPlayer, (p) => { 
                                Debug.Log("GameManagerController: Target selected by human, requesting card...");
                                
                                try {
                                    InputManagerEnhanced.Instance.RequestRevealSelection(humanPlayer, p, (c) => {
                                        Debug.Log("GameManagerController: Target card selected by human");
                                        OnHumanPlayerMoveConfirmed();
                                    });
                                } catch (Exception ex) {
                                    Debug.LogWarning($"RequestRevealSelection failed: {ex}");
                                    OnHumanPlayerMoveConfirmed();
                                }
                            }); 
                        } catch (Exception ex) { Debug.LogWarning($"RequestPlayerSelection failed: {ex}"); }
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("GameManagerController: Cannot request reveal selection - missing InputManager/UIManager or no valid targets. Continuing without pause.");
                        return false;
                    }
                }
                break;
        }

        return false;
    }
    
    /// <summary>
    /// Execute current game phase
    /// </summary>
    public void ExecutePhase()
    {
        if (gameManager == null) return;
        // Before executing a phase, ensure we haven't just discovered that human input is needed.
        if (PrepareForPhaseIfHuman())
        {
            Debug.Log("GameManagerController: PrepareForPhaseIfHuman requested input - deferring ExecutePhase");
            return;
        }

        // Don't execute if waiting for human input (another path set this)
        if (waitingForHumanInput)
        {
            Debug.Log("Waiting for human player input...");
            return;
        }
        
        if (gameManager.IsPausedForHumanInput)
            return;

        gameManager.ExecutePhase();
    }
    
    /// <summary>
    /// Get current game state
    /// </summary>
    public GameState GetGameState() => gameManager?.CurrentGameState;
    
    /// <summary>
    /// Check if currently waiting for human player input
    /// </summary>
    public bool IsWaitingForHumanInput => waitingForHumanInput;
    
    /// <summary>
    /// Notify that human player has made a move (resume game)
    /// </summary>
    public void OnHumanPlayerMoveConfirmed()
    {
        waitingForHumanInput = false;
        inputTimeoutCounter = 0f;
        gameManager?.Resume();
        Debug.Log("GameManagerController: Human player move confirmed - attempting to resume phase execution.");

        // Try to resume phase execution immediately
        ExecutePhase();
    }
    
    /// <summary>
    /// Run complete game (for testing)
    /// </summary>
    public void RunCompleteGame()
    {
        if (gameManager == null) return;
        gameManager.RunCompleteGame();
    }
    
    public void AcknowledgeTrick()
    {
        gameManager?.AcknowledgeTrick();
    }
}
