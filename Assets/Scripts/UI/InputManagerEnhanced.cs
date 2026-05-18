using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Enhanced input manager with full card/player selection, validation, and callbacks.
/// Coordinates between UI clicks and GameManager decisions.
/// </summary>
public class InputManagerEnhanced : MonoBehaviour
{
    private GameManagerController gameManagerController;
    private ValidationManager validationManager;
    private Dictionary<int, Card> pendingCardSelections = new Dictionary<int, Card>();
    private Dictionary<int, Player> pendingPlayerSelections = new Dictionary<int, Player>();
    
    public static InputManagerEnhanced Instance { get; private set; }
    
    // Events for card/player selection
    public event Action<Card> OnCardSelected;
    public event Action<Player> OnPlayerSelected;
    public event Action<string> OnInvalidMoveAttempted;
    
    private bool awaitingCardInput = false;
    private bool awaitingPlayerInput = false;
    private Player currentSelectionPlayer;
    private List<Card> validCards = new List<Card>();
    private List<Player> validPlayers = new List<Player>();
    
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
        validationManager = ValidationManager.Instance;
        
        if (validationManager == null)
        {
            Debug.LogError("ValidationManager not found!");
        }
    }

    // Automated self-test: if a file named 'AUTOTEST_MODE' exists in the project root,
    // the InputManager will automatically pick the first valid option after a short delay.
    private bool autoSelectEnabled = false;
    private float autoSelectDelay = 0.5f;

    private void OnEnable()
    {
        try
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var flag = Path.Combine(projectRoot, "AUTOTEST_MODE");
            autoSelectEnabled = File.Exists(flag);
            if (autoSelectEnabled)
                Debug.Log($"InputManagerEnhanced: AUTOTEST_MODE detected at {flag} - will auto-select inputs after {autoSelectDelay}s");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"InputManagerEnhanced: Failed to detect AUTOTEST_MODE flag: {ex}");
            autoSelectEnabled = false;
        }
    }
    
    private IEnumerator AutoPickCardAfterDelay(Player player, Card card, float delay)
    {
        yield return new WaitForSeconds(delay);
        OnCardClicked(card);
    }

    private IEnumerator AutoPickPlayerAfterDelay(Player player, Player targetPlayer, float delay)
    {
        yield return new WaitForSeconds(delay);
        OnPlayerClicked(targetPlayer);
    }

    /// <summary>
    /// Request card input from human player during Play phase
    /// </summary>
    public void RequestCardSelection(Player player, Action<Card> onCardSelected)
    {
        if (player == null)
        {
            Debug.LogError("Player is null");
            return;
        }
        
        currentSelectionPlayer = player;
        awaitingCardInput = true;
        
        // Get valid cards
        GameState gameState = gameManagerController.GetGameState();
        if (gameState == null)
        {
            Debug.LogError("Game state is null");
            return;
        }
        
        validCards = validationManager.GetValidPlayCards(player, gameState);
        
        if (validCards.Count == 0)
        {
            Debug.LogWarning($"No valid cards for Player {player.ID}");
            onCardSelected?.Invoke(null);
            awaitingCardInput = false;
            return;
        }
        
        // Subscribe and wait for selection
        if (onCardSelected != null)
            OnCardSelected += onCardSelected;

        // Tell UI to enable selection and show valid options
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RequestCardSelection(player, OnCardClicked, validCards);
            Debug.Log($"Waiting for Player {player.ID} to select a card ({validCards.Count} valid options) - UIManager present");
        }
        else
        {
            Debug.LogWarning($"RequestCardSelection: UIManager is null - cannot show card selection UI for Player {player.ID}");
        }

        // If automated self-test mode is enabled, pick the first valid card after a short delay
        if (autoSelectEnabled)
        {
            if (validCards != null && validCards.Count > 0)
            {
                StartCoroutine(AutoPickCardAfterDelay(player, validCards[0], autoSelectDelay));
            }
            else
            {
                Debug.LogWarning($"Auto-select requested but no valid cards for Player {player.ID}");
            }
        }
    }
    
    /// <summary>
    /// Request player selection during Reveal phase
    /// </summary>
    public void RequestPlayerSelection(Player revealingPlayer, Action<Player> onPlayerSelected)
    {
        if (revealingPlayer == null)
        {
            Debug.LogError("Revealing player is null");
            return;
        }
        
        currentSelectionPlayer = revealingPlayer;
        awaitingPlayerInput = true;
        
        // Get valid targets
        GameState gameState = gameManagerController.GetGameState();
        if (gameState == null)
        {
            Debug.LogError("Game state is null");
            return;
        }
        
        validPlayers = validationManager.GetValidRevealTargets(revealingPlayer, gameState);
        
        if (validPlayers.Count == 0)
        {
            Debug.LogWarning($"No valid targets for Player {revealingPlayer.ID}");
            onPlayerSelected?.Invoke(null);
            awaitingPlayerInput = false;
            return;
        }
        
        // Subscribe and wait for selection
        if (onPlayerSelected != null)
            OnPlayerSelected += onPlayerSelected;

        // Tell UI to enable target selection
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RequestTargetPlayerSelection(revealingPlayer, OnPlayerClicked, validPlayers);
            Debug.Log($"Waiting for Player {revealingPlayer.ID} to select a target ({validPlayers.Count} valid options) - UIManager present");
        }
        else
        {
            Debug.LogWarning($"RequestPlayerSelection: UIManager is null - cannot show player selection UI for Player {revealingPlayer.ID}");
        }

        // Automated self-test: auto-pick first valid target after delay
        if (autoSelectEnabled)
        {
            if (validPlayers != null && validPlayers.Count > 0)
            {
                StartCoroutine(AutoPickPlayerAfterDelay(revealingPlayer, validPlayers[0], autoSelectDelay));
            }
            else
            {
                Debug.LogWarning($"Auto-select requested but no valid target players for Player {revealingPlayer.ID}");
            }
        }
    }
    
    /// <summary>
    /// Request reveal card selection from target player's hidden hand
    /// </summary>
    public void RequestRevealSelection(Player revealingPlayer, Player targetPlayer, Action<Card> onCardSelected)
    {
        if (targetPlayer == null)
        {
            Debug.LogError("Target player is null");
            return;
        }
        
        GameState gameState = gameManagerController.GetGameState();
        if (gameState == null)
        {
            Debug.LogError("Game state is null");
            return;
        }
        
        validCards = validationManager.GetValidRevealCards(revealingPlayer, targetPlayer, gameState);
        
        if (validCards.Count == 0)
        {
            Debug.LogWarning($"No valid cards to reveal from Player {targetPlayer.ID}");
            onCardSelected?.Invoke(null);
            return;
        }

        // RULE UPDATE: Cards in the hidden hand are indistinguishable. 
        // Automatically pick a random valid card instead of awaiting user input.
        Card randomCard = validCards[UnityEngine.Random.Range(0, validCards.Count)];
        Debug.Log($"InputManagerEnhanced: Automatically selected random card {randomCard} from Player {targetPlayer.ID}'s hidden hand for Reveal Phase.");

        onCardSelected?.Invoke(randomCard);
    }

    /// <summary>
    /// Called when human clicks a card (from UI)
    /// </summary>
    public void OnCardClicked(Card card)
    {
        if (!awaitingCardInput)
        {
            OnInvalidMoveAttempted?.Invoke("Not waiting for card input");
            return;
        }
        
        if (card == null)
        {
            OnInvalidMoveAttempted?.Invoke("Card is null");
            return;
        }
        
        // Validate the selected card
        if (!validCards.Contains(card))
        {
            OnInvalidMoveAttempted?.Invoke($"Card {card} is not a valid selection.");
            return;
        }
        
        // Valid selection - store pending selection, invoke callbacks, clear state
        awaitingCardInput = false;

        if (currentSelectionPlayer != null)
            pendingCardSelections[currentSelectionPlayer.ID] = card;

        OnCardSelected?.Invoke(card);
        OnCardSelected = null; // Clear subscribers

        // Hide UI selection affordances
        if (UIManager.Instance != null)
            UIManager.Instance.CancelSelection();
    }
    
    /// <summary>
    /// Called when human clicks a player button (from UI)
    /// </summary>
    public void OnPlayerClicked(Player player)
    {
        if (!awaitingPlayerInput)
        {
            OnInvalidMoveAttempted?.Invoke("Not waiting for player input");
            return;
        }
        
        if (player == null)
        {
            OnInvalidMoveAttempted?.Invoke("Player is null");
            return;
        }
        
        // Validate the selected player
        if (!validPlayers.Contains(player))
        {
            OnInvalidMoveAttempted?.Invoke($"Player {player.ID} is not a valid target. Valid targets: {string.Join(", ", validPlayers.Select(p => p.ID))}");
            return;
        }
        
        // Valid selection - store pending selection, invoke callbacks, clear state
        awaitingPlayerInput = false;

        if (currentSelectionPlayer != null)
            pendingPlayerSelections[currentSelectionPlayer.ID] = player;

        OnPlayerSelected?.Invoke(player);
        OnPlayerSelected = null; // Clear subscribers

        // Hide UI selection affordances
        if (UIManager.Instance != null)
            UIManager.Instance.CancelSelection();
    }
    
    /// <summary>
    /// Cancel current input request
    /// </summary>
    public void CancelInput(string reason = "")
    {
        awaitingCardInput = false;
        awaitingPlayerInput = false;
        validCards.Clear();
        validPlayers.Clear();
        pendingCardSelections.Clear();
        pendingPlayerSelections.Clear();
        OnCardSelected = null;
        OnPlayerSelected = null;
        
        // Clear UI
        if (UIManager.Instance != null)
            UIManager.Instance.CancelSelection();

        Debug.Log($"Input cancelled. Reason: {reason}");
    }

    /// <summary>
    /// Try to consume a previously-selected card for the given player (pre-selection pattern).
    /// Returns the card if present, else null.
    /// </summary>
    public Card TryConsumeSelectedCard(int playerId)
    {
        if (pendingCardSelections.TryGetValue(playerId, out Card c))
        {
            pendingCardSelections.Remove(playerId);
            return c;
        }
        return null;
    }

    /// <summary>
    /// Try to consume a previously-selected player target for the given player (pre-selection pattern).
    /// Returns the Player if present, else null.
    /// </summary>
    public Player TryConsumeSelectedPlayer(int playerId)
    {
        if (pendingPlayerSelections.TryGetValue(playerId, out Player p))
        {
            pendingPlayerSelections.Remove(playerId);
            return p;
        }
        return null;
    }
    
    /// <summary>
    /// Check if a card selection is already pending for the given player.
    /// </summary>
    public bool HasPendingCardSelection(int playerId) => pendingCardSelections.ContainsKey(playerId);

    /// <summary>
    /// Check if a player selection is already pending for the given player.
    /// </summary>
    public bool HasPendingPlayerSelection(int playerId) => pendingPlayerSelections.ContainsKey(playerId);

    /// <summary>
    /// Check if waiting for input
    /// </summary>
    public bool IsAwaitingInput => awaitingCardInput || awaitingPlayerInput;
    
    /// <summary>
    /// Get current valid cards for display hints
    /// </summary>
    public List<Card> GetCurrentValidCards() => new List<Card>(validCards);
    
    /// <summary>
    /// Get current valid players for display hints
    /// </summary>
    public List<Player> GetCurrentValidPlayers() => new List<Player>(validPlayers);
}
