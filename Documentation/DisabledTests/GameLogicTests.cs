// using NUnit.Framework;
// DISABLED: NUnit not installed. Install via Package Manager:
// Window → General → Package Manager → Search "Test Framework" → Install

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comprehensive unit tests for game logic.
/// Tests card scoring, trick determination, hand management, and game flow.
/// DISABLED: Uncomment using statements after installing NUnit via Package Manager
/// </summary>
// [TestFixture]
public class GameLogicTests
{
    private GameState _gameState;
    private Player[] _players;

    // [SetUp]
    public void Setup()
    {
        _players = new Player[4];
        for (int i = 1; i <= 4; i++)
        {
            _players[i - 1] = new Player(i, $"Player {i}");
        }
        _gameState = new GameState(_players);
    }

    #region Card Scoring Tests

    // [Test]
    public void CardScoring_HiddenHand_FaceValue()
    {
        Assert.AreEqual(2, new Card(Suit.Clubs, Rank.Two).GetHiddenHandScore());
        Assert.AreEqual(10, new Card(Suit.Hearts, Rank.Ten).GetHiddenHandScore());
        Assert.AreEqual(11, new Card(Suit.Diamonds, Rank.Jack).GetHiddenHandScore());
        Assert.AreEqual(12, new Card(Suit.Spades, Rank.Queen).GetHiddenHandScore());
        Assert.AreEqual(13, new Card(Suit.Clubs, Rank.King).GetHiddenHandScore());
        Assert.AreEqual(14, new Card(Suit.Hearts, Rank.Ace).GetHiddenHandScore());
    }

    // [Test]
    public void CardScoring_ShownHand_SpecialValues()
    {
        // 2-10: face value + 2
        Assert.AreEqual(4, new Card(Suit.Clubs, Rank.Two).GetShownHandScore());
        Assert.AreEqual(12, new Card(Suit.Hearts, Rank.Ten).GetShownHandScore());

        // Face cards: J=13, Q=14, K=5, A=10
        Assert.AreEqual(13, new Card(Suit.Diamonds, Rank.Jack).GetShownHandScore());
        Assert.AreEqual(14, new Card(Suit.Spades, Rank.Queen).GetShownHandScore());
        Assert.AreEqual(5, new Card(Suit.Clubs, Rank.King).GetShownHandScore());
        Assert.AreEqual(10, new Card(Suit.Hearts, Rank.Ace).GetShownHandScore());
    }

    // [Test]
    public void CardScoring_FinalTally_AllFaceValue()
    {
        Card card2 = new Card(Suit.Clubs, Rank.Two);
        Card cardK = new Card(Suit.Hearts, Rank.King);
        Card cardA = new Card(Suit.Diamonds, Rank.Ace);

        Assert.AreEqual(2, card2.GetFaceValue());
        Assert.AreEqual(13, cardK.GetFaceValue());
        Assert.AreEqual(14, cardA.GetFaceValue());
    }

    #endregion

    #region Hand Management Tests

    // [Test]
    public void Hand_AddToHidden()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);

        hand.AddToHidden(card);
        Assert.AreEqual(1, hand.HiddenCount);
        Assert.AreEqual(0, hand.ShownCount);
        Assert.IsTrue(hand.Contains(card));
    }

    // [Test]
    public void Hand_RevealCard()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);
        hand.AddToHidden(card);

        Assert.IsTrue(hand.RevealCard(card));
        Assert.AreEqual(0, hand.HiddenCount);
        Assert.AreEqual(1, hand.ShownCount);
        Assert.IsTrue(hand.Contains(card));
    }

    // [Test]
    public void Hand_PlayCard_FromHidden()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);
        hand.AddToHidden(card);

        Card played = hand.PlayCard(card);
        Assert.AreEqual(card, played);
        Assert.AreEqual(0, hand.HiddenCount);
        Assert.IsFalse(hand.Contains(card));
    }

    // [Test]
    public void Hand_PlayCard_FromShown()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);
        hand.AddToShown(card);

        Card played = hand.PlayCard(card);
        Assert.AreEqual(card, played);
        Assert.AreEqual(0, hand.ShownCount);
        Assert.IsFalse(hand.Contains(card));
    }

    // [Test]
    public void Hand_PlayCard_NotFound()
    {
        Hand hand = new Hand();
        Card card = new Card(Suit.Clubs, Rank.Two);
        Card otherCard = new Card(Suit.Clubs, Rank.Three);
        hand.AddToHidden(card);

        Card played = hand.PlayCard(otherCard);
        Assert.IsNull(played);
    }

    #endregion

    #region Player Tests

    // [Test]
    public void Player_ScoringZone()
    {
        Player player = new Player(1);
        Card card1 = new Card(Suit.Clubs, Rank.King); // 13 points
        Card card2 = new Card(Suit.Hearts, Rank.Ace); // 14 points

        player.AddToScoringZone(card1);
        player.AddToScoringZone(card2);

        Assert.AreEqual(2, player.GetScoringZoneCount());
        Assert.AreEqual(27, player.GetScore());
    }

    // [Test]
    public void Player_CanRevealFrom()
    {
        Player player1 = new Player(1);
        Player player2 = new Player(2);
        
        Card card = new Card(Suit.Clubs, Rank.Two);
        player2.Hand.AddToHidden(card);

        Assert.IsTrue(player1.CanRevealFrom(player2));
        
        // Reveal the card
        player2.Hand.RevealCard(card);
        Assert.IsFalse(player1.CanRevealFrom(player2));
    }

    #endregion

    #region Game State Tests

    // [Test]
    public void GameState_InitialState()
    {
        Assert.AreEqual(GamePhase.Reveal, _gameState.CurrentPhase);
        Assert.AreEqual(1, _gameState.CurrentRound);
        Assert.AreEqual(1, _gameState.RoundStarter);
        Assert.AreEqual(0, _gameState.GetPlayZoneCount());
    }

    // [Test]
    public void GameState_GetPlayer()
    {
        Player p1 = _gameState.GetPlayer(1);
        Assert.AreEqual(1, p1.ID);
        
        Player invalid = _gameState.GetPlayer(5);
        Assert.IsNull(invalid);
    }

    // [Test]
    public void GameState_AddToPlayZone()
    {
        Card card = new Card(Suit.Clubs, Rank.Two);
        Player player = _gameState.GetPlayer(1);
        player.Hand.AddToHidden(card);

        _gameState.AddCardToPlayZone(card, player, HandSection.Hidden);
        Assert.AreEqual(1, _gameState.GetPlayZoneCount());
    }

    // [Test]
    public void GameState_AdvancePhase()
    {
        Assert.AreEqual(GamePhase.Reveal, _gameState.CurrentPhase);
        
        _gameState.AdvancePhase();
        Assert.AreEqual(GamePhase.Play, _gameState.CurrentPhase);
        
        _gameState.AdvancePhase();
        Assert.AreEqual(GamePhase.Determine, _gameState.CurrentPhase);
    }

    // [Test]
    public void GameState_GetPlayOrder()
    {
        Player starter = _gameState.GetPlayer(2);
        List<Player> order = _gameState.GetPlayOrder(starter);

        Assert.AreEqual(4, order.Count);
        Assert.AreEqual(2, order[0].ID);
        Assert.AreEqual(3, order[1].ID);
        Assert.AreEqual(4, order[2].ID);
        Assert.AreEqual(1, order[3].ID);
    }

    #endregion

    #region Trick Determination Tests

    // [Test]
    public void TrickDetermination_SimpleWinner()
    {
        // Create a simple trick: 2, 5, 10, 7 all from Hidden
        Card[] playedCards = new Card[4];
        Player[] playersInOrder = _gameState.GetAllPlayers();
