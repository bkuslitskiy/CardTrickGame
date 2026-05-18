using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a game player with hand, scoring zone, and AI implementation.
/// </summary>
public class Player
{
    public int ID { get; }
    public string Name { get; set; }
    public Hand Hand { get; }
    
    private List<Card> _scoringZone;

    /// <summary>
    /// Reference to AI implementation if this is an AI player.
    /// Null for human players.
    /// </summary>
    public IPlayer AIImplementation { get; set; }

    public bool IsAI => AIImplementation != null;

    /// <summary>
    /// Helper to map Player IDs to their table seat names.
    /// </summary>
    public static string GetDirectionName(int id)
    {
        return id switch { 1 => "South", 2 => "West", 3 => "North", 4 => "East", _ => $"Player {id}" };
    }

    public Player(int id, string name = null)
    {
        if (id < 1 || id > 4)
            throw new ArgumentException("Player ID must be 1-4", nameof(id));

        ID = id;
        Name = name ?? GetDirectionName(id);
        Hand = new Hand();
        _scoringZone = new List<Card>();
    }

    /// <summary>
    /// Add card to scoring zone (prize won from a trick).
    /// </summary>
    public void AddToScoringZone(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));
        _scoringZone.Add(card);
    }

    /// <summary>
    /// Get total score from all cards in scoring zone (face value).
    /// </summary>
    public int GetScore()
    {
        return _scoringZone.Sum(card => card.GetFaceValue());
    }

    /// <summary>
    /// Get number of cards in scoring zone.
    /// </summary>
    public int GetScoringZoneCount()
    {
        return _scoringZone.Count;
    }

    /// <summary>
    /// Get copy of scoring zone cards.
    /// </summary>
    public List<Card> GetScoringZoneCards()
    {
        return new List<Card>(_scoringZone);
    }

    /// <summary>
    /// Check if player has any cards left in hand.
    /// </summary>
    public bool HasCards()
    {
        return Hand.HasPlayableCards();
    }

    /// <summary>
    /// Check if player can reveal from another player's Hidden hand.
    /// </summary>
    public bool CanRevealFrom(Player targetPlayer)
    {
        if (targetPlayer == null)
            return false;
        return targetPlayer.Hand.HasHiddenCards();
    }

    /// <summary>
    /// Play a card from hand (removes from Hidden or Shown).
    /// Returns card if successful, null if card not in hand.
    /// </summary>
    public Card PlayCard(Card card)
    {
        return Hand.PlayCard(card);
    }

    /// <summary>
    /// Check if a specific card can be played (exists in hand).
    /// </summary>
    public bool CanPlayCard(Card card)
    {
        return Hand.Contains(card);
    }

    /// <summary>
    /// Check if player has cards in Hidden hand.
    /// </summary>
    public bool HasHiddenCards()
    {
        return Hand.HasHiddenCards();
    }

    /// <summary>
    /// Check if player has cards in Shown hand.
    /// </summary>
    public bool HasShownCards()
    {
        return Hand.HasShownCards();
    }

    /// <summary>
    /// Clear all cards (for reset/new game).
    /// </summary>
    public void Clear()
    {
        Hand.Clear();
        _scoringZone.Clear();
    }

    public override string ToString()
    {
        return $"{Name} (ID: {ID}, Score: {GetScore()})";
    }
}
