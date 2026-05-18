using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages a standard 52-card deck.
/// Handles creation, shuffling, and dealing.
/// </summary>
public class Deck
{
    private List<Card> _cards;
    private int _dealIndex;

    public int RemainingCount => _cards.Count - _dealIndex;

    public Deck()
    {
        _cards = new List<Card>(52);
        _dealIndex = 0;
        CreateStandardDeck();
    }

    /// <summary>
    /// Create a standard 52-card deck (13 ranks × 4 suits).
    /// </summary>
    private void CreateStandardDeck()
    {
        _cards.Clear();
        _dealIndex = 0;

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                _cards.Add(new Card(suit, rank));
            }
        }
    }

    /// <summary>
    /// Shuffle the entire deck using Fisher-Yates algorithm.
    /// Resets deal index to 0.
    /// </summary>
    public void Shuffle()
    {
        _dealIndex = 0;
        Random random = new Random();

        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int randomIndex = random.Next(i + 1);
            (_cards[i], _cards[randomIndex]) = (_cards[randomIndex], _cards[i]);
        }
    }

    /// <summary>
    /// Deal specified number of cards from top of deck.
    /// Returns empty list if not enough cards remaining.
    /// </summary>
    public List<Card> Deal(int count)
    {
        if (count < 0)
            throw new ArgumentException("Cannot deal negative cards", nameof(count));

        if (_dealIndex + count > _cards.Count)
            return new List<Card>();

        List<Card> dealtCards = _cards.Skip(_dealIndex).Take(count).ToList();
        _dealIndex += count;
        return dealtCards;
    }

    public List<Card> GetCards() => new List<Card>(_cards);

    public void SetCards(List<Suit> suits, List<Rank> ranks)
    {
        _cards.Clear();
        _dealIndex = 0;
        for (int i = 0; i < suits.Count; i++)
        {
            _cards.Add(new Card(suits[i], ranks[i]));
        }
    }

    /// <summary>
    /// Reset deck and deal index (for new game).
    /// </summary>
    public void Reset()
    {
        CreateStandardDeck();
        Shuffle();
    }

    /// <summary>
    /// Check if deck has enough cards remaining.
    /// </summary>
    public bool HasCards(int count)
    {
        return RemainingCount >= count;
    }
}
