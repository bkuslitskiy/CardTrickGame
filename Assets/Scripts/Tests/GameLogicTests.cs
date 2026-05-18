using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GameLogicTests
{
    private GameState _gameState;
    private Player[] _players;

    public void Setup()
    {
        _players = new Player[4];
        for (int i = 1; i <= 4; i++)
        {
            _players[i - 1] = new Player(i);
        }
        _gameState = new GameState(_players);
    }

    public void CardScoring_HiddenHand_FaceValue()
    {
        Debug.Assert(2 == new Card(Suit.Clubs, Rank.Two).GetHiddenHandScore());
        Debug.Assert(10 == new Card(Suit.Hearts, Rank.Ten).GetHiddenHandScore());
        Debug.Assert(11 == new Card(Suit.Diamonds, Rank.Jack).GetHiddenHandScore());
        Debug.Assert(12 == new Card(Suit.Spades, Rank.Queen).GetHiddenHandScore());
        Debug.Assert(13 == new Card(Suit.Clubs, Rank.King).GetHiddenHandScore());
        Debug.Assert(14 == new Card(Suit.Hearts, Rank.Ace).GetHiddenHandScore());
    }

    public void CardScoring_ShownHand_SpecialValues()
    {
        Debug.Assert(4 == new Card(Suit.Clubs, Rank.Two).GetShownHandScore());
        Debug.Assert(12 == new Card(Suit.Hearts, Rank.Ten).GetShownHandScore());
        Debug.Assert(13 == new Card(Suit.Diamonds, Rank.Jack).GetShownHandScore());
        Debug.Assert(14 == new Card(Suit.Spades, Rank.Queen).GetShownHandScore());
        Debug.Assert(5 == new Card(Suit.Clubs, Rank.King).GetShownHandScore());
        Debug.Assert(10 == new Card(Suit.Hearts, Rank.Ace).GetShownHandScore());
    }

    public void Hand_PlayCard_FromHidden()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);
        hand.AddToHidden(card);

        Card played = hand.PlayCard(card);
        Debug.Assert(card == played);
        Debug.Assert(0 == hand.HiddenCount);
        Debug.Assert(!hand.Contains(card));
    }

    public void RunAllTests()
    {
        try {
            Setup();
            CardScoring_HiddenHand_FaceValue();
            CardScoring_ShownHand_SpecialValues();
            Hand_PlayCard_FromHidden();
            Debug.Log("All tests passed!");
        } catch (Exception e) {
            Debug.LogError($"Test failed: {e.Message}");
        }
    }
}
