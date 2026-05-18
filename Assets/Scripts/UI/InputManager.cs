using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages input from human players. Interfaces between UI and GameManager.
/// </summary>
public class InputManager : MonoBehaviour
{
    private GameManagerController gameManagerController;
    private GameBoardUI gameBoardUI;
    
    public static InputManager Instance { get; private set; }
    
    // Events for card selection
    public event Action<Card> OnCardSelected;
    public event Action<Player> OnPlayerSelected;
    
    private bool awaitingCardInput = false;
    private bool awaitingPlayerInput = false;
    
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
        // Use newer API when available to avoid Obsolete warning
    #if UNITY_2023_2_OR_NEWER
        gameBoardUI = UnityEngine.Object.FindAnyObjectByType<GameBoardUI>();
    #else
        gameBoardUI = FindObjectOfType<GameBoardUI>();
    #endif
    }
    
    /// <summary>
    /// Request card input from human player
    /// </summary>
    public void RequestCardInput(Player player, Action<Card> onCardSelected)
    {
        awaitingCardInput = true;
        OnCardSelected += onCardSelected;
        
        if (gameBoardUI != null)
            gameBoardUI.EnableCardSelection(player, OnCardClicked);
    }
    
    /// <summary>
    /// Request player input (for reveal target selection)
    /// </summary>
    public void RequestPlayerInput(Player revealingPlayer, Action<Player> onPlayerSelected)
    {
        awaitingPlayerInput = true;
        OnPlayerSelected += onPlayerSelected;
        
        if (gameBoardUI != null)
            gameBoardUI.EnablePlayerSelection(revealingPlayer, OnPlayerClicked);
    }
    
    /// <summary>
    /// Request reveal card input
    /// </summary>
    public void RequestRevealInput(Player targetPlayer, Action<Card> onCardSelected)
    {
        awaitingCardInput = true;
        OnCardSelected += onCardSelected;
        
        if (gameBoardUI != null)
            gameBoardUI.EnableRevealSelection(null, targetPlayer, OnCardClicked);
    }
    
    private void OnCardClicked(Card card)
    {
        if (awaitingCardInput)
        {
            awaitingCardInput = false;
            OnCardSelected?.Invoke(card);
            OnCardSelected = null; // Clear subscribers
            
            if (gameBoardUI != null)
                gameBoardUI.DisableCardSelection();
        }
    }
    
    private void OnPlayerClicked(Player player)
    {
        if (awaitingPlayerInput)
        {
            awaitingPlayerInput = false;
            OnPlayerSelected?.Invoke(player);
            OnPlayerSelected = null; // Clear subscribers
        }
    }
    
    /// <summary>
    /// Cancel current input request
    /// </summary>
    public void CancelInput()
    {
        awaitingCardInput = false;
        awaitingPlayerInput = false;
        OnCardSelected = null;
        OnPlayerSelected = null;
        
        if (gameBoardUI != null)
            gameBoardUI.DisableCardSelection();
    }
    
    public bool IsAwaitingInput => awaitingCardInput || awaitingPlayerInput;
}
