using UnityEngine;
using System;

/// <summary>
/// Central UI manager that coordinates between GameManager and all UI elements.
/// Handles phase transitions, card displays, and player feedback.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] private GameBoardUIEnhanced gameBoardUI;
    [SerializeField] private MenuUI menuUI;
    
    private GameManagerController gameManagerController;
    
    public static UIManager Instance { get; private set; }
    
    public bool DebugRevealAll { get; set; } = false;

    public event Action<string> OnPhaseChanged;
    public event Action<int, int> OnScoreUpdated; // (playerId, score)
    public event Action<Card> OnCardPlayed;
    public event Action<Player> OnRoundWinnerAnnounced;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        gameManagerController = GameManagerController.Instance;
        
        if (gameBoardUI != null)
            gameBoardUI.Initialize(gameManagerController);
        if (menuUI != null)
            menuUI.Initialize();

        // Subscribe to game complete to show results and menu
        if (gameManagerController != null)
        {
            gameManagerController.OnGameComplete += () =>
            {
                var state = gameManagerController.GetGameState();
                if (state != null)
                {
                    var players = state.GetAllPlayers();
                    NotifyGameComplete(players);
                }
            };
        }
    }

    private void Update()
    {
        // Handle Android Back Button / Escape Key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (menuUI != null && !menuUI.gameObject.activeSelf)
                ShowMenu();
            else if (activePromptPanel != null)
                CancelSelection();
        }
    }

    // Runtime prompt panel used to instruct the human player when a selection is requested
    private GameObject activePromptPanel;

    private void ShowPrompt(string text)
    {
        // Remove existing
        if (activePromptPanel != null)
        {
            Destroy(activePromptPanel);
            activePromptPanel = null;
        }

        Canvas c = FindAnyObjectByType<Canvas>();
        if (c == null) return;

        activePromptPanel = new GameObject("InputPrompt");
        activePromptPanel.transform.SetParent(c.transform, false);
        var img = activePromptPanel.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0f, 0.65f);
        img.raycastTarget = false; // Prevent blocking clicks
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.2f, 0.01f);
        rt.anchorMax = new Vector2(0.8f, 0.07f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var labelGO = new GameObject("PromptLabel");
        labelGO.transform.SetParent(activePromptPanel.transform, false);
        var lbl = labelGO.AddComponent<UnityEngine.UI.Text>();
        lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.color = Color.white;
        lbl.text = text;
        lbl.raycastTarget = false; // Prevent blocking clicks
        var lrt = lbl.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Show the main menu and hide the game board UI.
    /// </summary>
    public void ShowMenu()
    {
        if (menuUI != null)
            menuUI.gameObject.SetActive(true);
        if (gameBoardUI != null)
        {
            gameBoardUI.ClearAllState();
            gameBoardUI.gameObject.SetActive(false);
        }
            
        CancelSelection(); // Clean up any active input prompts

        if (InputManagerEnhanced.Instance != null)
            InputManagerEnhanced.Instance.CancelInput("Return to menu");
    }

    /// <summary>
    /// Hide the main menu and show the game board UI.
    /// </summary>
    public void HideMenu()
    {
        if (menuUI != null)
            menuUI.gameObject.SetActive(false);
        if (gameBoardUI != null)
            gameBoardUI.gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Called when a new phase begins
    /// </summary>
    public void NotifyPhaseChanged(GamePhase phase)
    {
        OnPhaseChanged?.Invoke(phase.ToString());
        
        if (gameBoardUI != null)
            gameBoardUI.UpdatePhaseDisplay(phase);
    }
    
    /// <summary>
    /// Called when a player's score updates
    /// </summary>
    public void NotifyScoreUpdated(int playerId, int score)
    {
        OnScoreUpdated?.Invoke(playerId, score);
        
        if (gameBoardUI != null)
            gameBoardUI.UpdatePlayerScore(playerId, score);
    }
    
    /// <summary>
    /// Called when a card is played
    /// </summary>
    public void NotifyCardPlayed(Card card, Player player)
    {
        OnCardPlayed?.Invoke(card);
        
        if (gameBoardUI != null)
            gameBoardUI.DisplayCardInPlayZone(card, player.ID);
    }
    
    /// <summary>
    /// Called when a round completes and winner is announced
    /// </summary>
    public void NotifyRoundWinner(Player winner)
    {
        OnRoundWinnerAnnounced?.Invoke(winner);
        
        if (gameBoardUI != null)
            gameBoardUI.DisplayRoundWinner(winner);
    }
    
    /// <summary>
    /// Called when game ends
    /// </summary>
    public void NotifyGameComplete(Player[] finalScores)
    {
        if (gameBoardUI != null)
            gameBoardUI.DisplayGameResults(finalScores);
    }

    /// <summary>
    /// Cancel any active selection UIs (card selection, player selection)
    /// </summary>
    public void CancelSelection()
    {
        if (gameBoardUI != null)
        {
            try { gameBoardUI.DisableCardSelection(); } catch { }
            try { gameBoardUI.DisablePlayerSelection(); } catch { }
        }
        if (activePromptPanel != null)
        {
            Destroy(activePromptPanel);
            activePromptPanel = null;
        }
    }

    /// <summary>
    /// Runtime setter to assign GameBoardUI and MenuUI when created dynamically.
    /// Prefer this over reflection from bootstrapper.
    /// </summary>
    public void SetBoardAndMenu(GameBoardUIEnhanced board, MenuUI menu)
    {
        gameBoardUI = board;
        menuUI = menu;
        Debug.Log("UIManager: SetBoardAndMenu assigned at runtime");
    }
    
    /// <summary>
    /// Request human player input for card selection during Play phase
    /// </summary>
    public void RequestCardSelection(Player player, Action<Card> onCardSelected, System.Collections.Generic.List<Card> validCards = null)
    {
        if (gameBoardUI != null)
            gameBoardUI.EnableCardSelection(player, onCardSelected, validCards);
        ShowPrompt($"{player.Name}: Select a card to play");
    }
    
    /// <summary>
    /// Request human player input for card reveal during Reveal phase
    /// </summary>
    public void RequestRevealSelection(Player revealingPlayer, Player targetPlayer, Action<Card> onCardSelected, System.Collections.Generic.List<Card> validCards = null)
    {
        if (gameBoardUI != null)
            gameBoardUI.EnableRevealSelection(revealingPlayer, targetPlayer, onCardSelected, validCards);
        ShowPrompt($"{revealingPlayer.Name}: Select a card to reveal from {targetPlayer.Name}");
    }
    
    /// <summary>
    /// Request human player input for target player selection during Reveal phase
    /// </summary>
    public void RequestTargetPlayerSelection(Player revealingPlayer, Action<Player> onPlayerSelected, System.Collections.Generic.List<Player> validPlayers = null)
    {
        if (gameBoardUI != null)
            gameBoardUI.EnablePlayerSelection(revealingPlayer, onPlayerSelected, validPlayers);
        ShowPrompt($"{revealingPlayer.Name}: Choose a player to reveal from");
    }
}
