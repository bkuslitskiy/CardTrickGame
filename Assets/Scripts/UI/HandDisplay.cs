using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Displays a player's hand (Hidden and Shown cards).
/// Handles card selection for human players with visual feedback for valid moves.
/// </summary>
public class HandDisplay : MonoBehaviour
{
    [SerializeField] private Transform hiddenHandContainer;
    [SerializeField] private Transform shownHandContainer;
    [SerializeField] private int playerId;
    [SerializeField] private Color validCardColor = new Color(0, 1, 0, 1); // Green
    [SerializeField] private Color invalidCardColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Dim gray
    [SerializeField] private Color normalCardColor = Color.white;
    
    private Hand hand;
    private List<CardDisplay> cardDisplays = new List<CardDisplay>();
    private int hiddenCount = 0;
    private Action<Card> onCardSelected;
    private bool selectionEnabled = false;
    private bool revealSelectionEnabled = false;
    private List<Card> validCards = new List<Card>();
    
    public void Initialize(int id)
    {
        playerId = id;
    }
    
    /// <summary>
    /// Refresh the hand display based on current hand state
    /// </summary>
    public void RefreshDisplay(Hand playerHand, bool isDeal = false)
    {
        hand = playerHand;
        
        // Clear existing displays
        foreach (CardDisplay display in cardDisplays)
            Destroy(display.gameObject);
        cardDisplays.Clear();
        
        if (hand == null) return;
        
        bool isHuman = GameManagerController.Instance != null && GameManagerController.Instance.HumanPlayerId == playerId;
        bool revealAll = UIManager.Instance != null && UIManager.Instance.DebugRevealAll;

        // Display Hidden hand cards (back of card) but keep their Card objects for reveal
        hiddenCount = 0;
        if (hiddenHandContainer != null)
        {
            var hiddenCards = hand.GetHiddenCards().OrderByDescending(c => c.GetFaceValue()).ThenBy(c => c.Suit).ToList();
            foreach (Card hiddenCard in hiddenCards)
            {
                CardDisplay cardDisplay = PrefabFactory.CreateCardDisplayInstance();
                if (isHuman || revealAll)
                    cardDisplay.SetCard(hiddenCard, HandSection.Hidden);
                else
                    cardDisplay.SetCardBack(hiddenCard);
                
                cardDisplay.SetOwnerIndex(playerId);
                cardDisplay.transform.SetParent(hiddenHandContainer, false);
                cardDisplays.Add(cardDisplay);
                hiddenCount++;
            }
        }
        
        // Display Shown hand cards (face up)
        if (shownHandContainer != null)
        {
            var shownCards = hand.GetShownCards().OrderByDescending(c => c.GetFaceValue()).ThenBy(c => c.Suit).ToList();
            foreach (Card card in shownCards)
            {
                CardDisplay cardDisplay = PrefabFactory.CreateCardDisplayInstance();
                cardDisplay.SetCard(card, HandSection.Shown);
                cardDisplay.SetOwnerIndex(playerId);
                cardDisplay.transform.SetParent(shownHandContainer, false);

                if (selectionEnabled || revealSelectionEnabled)
                {
                    Card captured = card;
                    cardDisplay.MakeClickable(() => OnCardClicked(captured));
                }

                cardDisplays.Add(cardDisplay);
            }
        }
        
        // Layout displays so they don't all overlap
        LayoutDisplays(isDeal);

        // Update visual feedback for valid cards
        UpdateCardValidityVisuals();
    }

    private void ApplyPos(CardDisplay d, RectTransform rt, Vector2 targetPos, bool isDeal)
    {
        if (isDeal)
        {
            rt.anchoredPosition = Vector2.zero; // Animate deal from center of player panel
            if (AnimationController.Instance != null)
                AnimationController.Instance.AnimateCardPlay(d, targetPos);
            else
                rt.anchoredPosition = targetPos;
        }
        else rt.anchoredPosition = targetPos;
    }

    private void LayoutDisplays(bool isDeal)
    {
        if (hand == null) return;
        int hCount = hiddenCount;
        int sCount = hand.GetShownCount();

        if (hCount > 0) LayoutCards(hCount, 0, 55f, isDeal);
        if (sCount > 0) LayoutCards(sCount, hCount, 60f, isDeal);
    }

    private void LayoutCards(int count, int startIndex, float spacing, bool isDeal)
    {
        for (int i = 0; i < count && (startIndex + i) < cardDisplays.Count; i++)
        {
            var d = cardDisplays[startIndex + i];
            if (d == null) continue;
            var rt = d.GetComponent<RectTransform>();
            if (rt == null) continue;
            
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(80f, 120f);
            
            float posAmount = -((count - 1) * spacing) / 2f + i * spacing;
            
            if (playerId == 0) { // South
                rt.localRotation = Quaternion.identity;
                ApplyPos(d, rt, new Vector2(posAmount, 0f), isDeal);
            } else if (playerId == 1) { // West
                rt.localRotation = Quaternion.Euler(0f, 0f, -90f);
                ApplyPos(d, rt, new Vector2(0f, -posAmount), isDeal);
            } else if (playerId == 2) { // North
                rt.localRotation = Quaternion.identity;
                ApplyPos(d, rt, new Vector2(posAmount, 0f), isDeal);
            } else if (playerId == 3) { // East
                rt.localRotation = Quaternion.Euler(0f, 0f, 90f);
                ApplyPos(d, rt, new Vector2(0f, posAmount), isDeal);
            }
        }
    }
    
    /// <summary>
    /// Update card colors to show which are valid
    /// </summary>
    private void UpdateCardValidityVisuals()
    {
        if (!selectionEnabled && !revealSelectionEnabled)
            return;

        // Update all displays (hidden + shown) that have backing Card objects
        foreach (CardDisplay display in cardDisplays)
        {
            if (display == null) continue;
            var c = display.GetCard();
            if (c == null) continue;

            if (validCards.Contains(c))
            {
                display.SetHighlightColor(validCardColor);
                display.Highlight();
            }
            else
            {
                display.SetHighlightColor(invalidCardColor);
                display.RemoveHighlight();
            }
        }
    }
    
    /// <summary>
    /// Enable card selection mode (human player playing a card)
    /// </summary>
    public void EnableCardSelection(Action<Card> callback, List<Card> validCardsList = null)
    {
        selectionEnabled = true;
        onCardSelected = callback;
        validCards = validCardsList ?? new List<Card>();
        
        // Make all valid cards clickable
        for (int i = 0; i < cardDisplays.Count; i++)
        {
            var display = cardDisplays[i];
            if (display == null) continue;
            var card = display.GetCard();
            if (card == null) continue;

            if (validCards.Count == 0 || validCards.Contains(card))
                display.MakeClickable(() => OnCardClicked(card));
            else
                display.MakeUnclickable();
        }
        
        UpdateCardValidityVisuals();
    }
    
    /// <summary>
    /// Disable card selection
    /// </summary>
    public void DisableCardSelection()
    {
        selectionEnabled = false;
        revealSelectionEnabled = false;
        onCardSelected = null;
        validCards.Clear();
        
        foreach (CardDisplay display in cardDisplays)
        {
            if (display != null)
            {
                display.MakeUnclickable();
                display.RemoveHighlight();
            }
        }
    }
    
    /// <summary>
    /// Enable reveal selection mode (selecting card to reveal from hidden hand)
    /// </summary>
    public void EnableRevealSelection(Action<Card> callback, List<Card> validCardsList = null)
    {
        revealSelectionEnabled = true;
        onCardSelected = callback;
        validCards = validCardsList ?? new List<Card>();

        // Make only hidden-card displays clickable (they are stored first)
        for (int i = 0; i < hiddenCount && i < cardDisplays.Count; i++)
        {
            var display = cardDisplays[i];
            if (display == null) continue;
            var c = display.GetCard();
            display.MakeClickable(() => OnCardClicked(c));
        }

        UpdateCardValidityVisuals();
    }
    
    private void OnCardClicked(Card card)
    {
        if (onCardSelected != null && (selectionEnabled || revealSelectionEnabled))
        {
            // Validate if there's a valid list
            if (validCards.Count > 0 && !validCards.Contains(card))
            {
                Debug.LogWarning($"Card {card} is not in valid list!");
                return;
            }
            
            onCardSelected.Invoke(card);
            DisableCardSelection();
        }
    }
    
    public int GetPlayerId() => playerId;
}
