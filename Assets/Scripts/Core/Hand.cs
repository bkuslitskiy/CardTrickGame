using System;
using System.Collections.Generic;
using System.Linq;

public enum HandSection
{
    Hidden,
    Shown
}

/// <summary>
/// Represents a player's hand divided into Hidden and Shown sections.
/// Hidden cards are private to the owner; Shown cards are visible to all.
/// </summary>
public class Hand
{
    private List<Card> _hidden;
    private List<Card> _shown;

    public int HiddenCount => _hidden.Count;
    public int ShownCount => _shown.Count;

    public Hand()
    {
        _hidden = new List<Card>();
        _shown = new List<Card>();
    }

    /// <summary>
    /// Get count of hidden cards (convenience method).
    /// </summary>
    public int GetHiddenCount() => HiddenCount;

    /// <summary>
    /// Get count of shown cards (convenience method).
    /// </summary>
    public int GetShownCount() => ShownCount;

    /// <summary>
    /// Check if a card is in the Shown section.
    /// </summary>
    public bool IsCardShown(Card card)
    {
        return _shown.Contains(card);
    }

    /// <summary>
    /// Check if a card is in the Hidden section.
    /// </summary>
    public bool IsCardHidden(Card card)
    {
        return _hidden.Contains(card);
    }

    /// <summary>
    /// Add card to Hidden section.
    /// </summary>
    public void AddToHidden(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));
        _hidden.Add(card);
    }

    /// <summary>
    /// Add card to Shown section.
    /// </summary>
    public void AddToShown(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));
        _shown.Add(card);
    }

    /// <summary>
    /// Move card from Hidden to Shown during reveal phase.
    /// </summary>
    public bool RevealCard(Card card)
    {
        if (!_hidden.Contains(card))
            return false;

        _hidden.Remove(card);
        _shown.Add(card);
        return true;
    }

    /// <summary>
    /// Remove and return card from either section.
    /// Returns null if card not found.
    /// </summary>
    public Card PlayCard(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));

        if (_hidden.Contains(card))
        {
            _hidden.Remove(card);
            return card;
        }

        if (_shown.Contains(card))
        {
            _shown.Remove(card);
            return card;
        }

        return null;
    }

    /// <summary>
    /// Check if hand has any cards in Hidden section.
    /// </summary>
    public bool HasHiddenCards()
    {
        return _hidden.Count > 0;
    }

    /// <summary>
    /// Check if hand has any cards in Shown section.
    /// </summary>
    public bool HasShownCards()
    {
        return _shown.Count > 0;
    }

    /// <summary>
    /// Check if hand has any playable cards.
    /// </summary>
    public bool HasPlayableCards()
    {
        return _hidden.Count + _shown.Count > 0;
    }

    /// <summary>
    /// Get all Hidden cards (only visible to owner).
    /// </summary>
    public List<Card> GetHiddenCards()
    {
        return new List<Card>(_hidden);
    }

    /// <summary>
    /// Get all Shown cards (visible to all).
    /// </summary>
    public List<Card> GetShownCards()
    {
        return new List<Card>(_shown);
    }

    /// <summary>
    /// Get all cards in hand (Hidden + Shown).
    /// Allocates a new list — fine for UI/display calls.
    /// For tight AI/simulation loops use <see cref="ForEachCard"/> instead.
    /// </summary>
    public List<Card> GetAllCards()
    {
        return new List<Card>(_hidden.Concat(_shown));
    }

    /// <summary>
    /// Non-allocating enumeration of all cards (Hidden then Shown).
    /// Preferred for AI decision-making and RL simulation paths where
    /// GC pressure matters.
    /// </summary>
    public void ForEachCard(System.Action<Card, HandSection> action)
    {
        foreach (Card c in _hidden) action(c, HandSection.Hidden);
        foreach (Card c in _shown)  action(c, HandSection.Shown);
    }

    /// <summary>
    /// Get cards visible to this player.
    /// For local player: own Hidden + all Shown
    /// For other players: only Shown
    /// </summary>
    public List<Card> GetVisibleCards(bool isLocalPlayer)
    {
        if (isLocalPlayer)
            return GetAllCards();
        else
            return GetShownCards();
    }

    /// <summary>
    /// Check if specific card exists in hand.
    /// </summary>
    public bool Contains(Card card)
    {
        return _hidden.Contains(card) || _shown.Contains(card);
    }

    /// <summary>
    /// Get which section a card belongs to, or null if not found.
    /// </summary>
    public HandSection? GetCardSection(Card card)
    {
        if (_hidden.Contains(card))
            return HandSection.Hidden;
        if (_shown.Contains(card))
            return HandSection.Shown;
        return null;
    }

    /// <summary>
    /// Clear entire hand (for reset/new game).
    /// </summary>
    public void Clear()
    {
        _hidden.Clear();
        _shown.Clear();
    }
}
