using System;

public enum Suit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

/// <summary>
/// Represents a single playing card with suit and rank.
/// Immutable - create new instance to modify.
/// </summary>
public class Card : IEquatable<Card>
{
    public Suit Suit { get; }
    public Rank Rank { get; }

    public Card(Suit suit, Rank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    /// <summary>
    /// Get face value (2-14) regardless of hand type.
    /// Used for final tally scoring.
    /// </summary>
    public int GetFaceValue()
    {
        return (int)Rank;
    }

    /// <summary>
    /// Get score when played from Hidden hand.
    /// 2-10: face value
    /// J: 11, Q: 12, K: 13, A: 14
    /// </summary>
    public int GetHiddenHandScore()
    {
        return (int)Rank;
    }

    /// <summary>
    /// Get score when played from Shown hand.
    /// 2-10: face value + 2
    /// J: 13, Q: 14, K: 5, A: 10
    /// </summary>
    public int GetShownHandScore()
    {
        return Rank switch
        {
            Rank.Two => 4,
            Rank.Three => 5,
            Rank.Four => 6,
            Rank.Five => 7,
            Rank.Six => 8,
            Rank.Seven => 9,
            Rank.Eight => 10,
            Rank.Nine => 11,
            Rank.Ten => 12,
            Rank.Jack => 13,
            Rank.Queen => 14,
            Rank.King => 5,
            Rank.Ace => 10,
            _ => 0
        };
    }

    /// <summary>
    /// Get human-readable card name.
    /// </summary>
    public string GetName()
    {
        return $"{GetRankName()}{GetSuitSymbol()}";
    }

    private string GetRankName()
    {
        return Rank switch
        {
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)Rank).ToString()
        };
    }

    private string GetSuitSymbol()
    {
        return Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            Suit.Spades => "♠",
            _ => "?"
        };
    }

    public override string ToString()
    {
        return GetName();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Card);
    }

    public bool Equals(Card other)
    {
        return other != null && Suit == other.Suit && Rank == other.Rank;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Suit, Rank);
    }
}
