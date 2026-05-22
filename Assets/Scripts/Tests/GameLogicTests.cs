// ── Unity Test Framework (Edit Mode) ──────────────────────────────────────
// This file uses NUnit via the Unity Test Framework package.
//
// Setup required (one-time):
//   1. In the Unity Package Manager install "Test Framework" (com.unity.test-framework).
//   2. The Tests.asmdef in this folder already sets testPlatforms = ["Editor"],
//      so these run as Edit-mode tests (no Play-mode overhead).
//   3. Open Window → General → Test Runner → EditMode tab and press Run All.
// ─────────────────────────────────────────────────────────────────────────

using NUnit.Framework;
using System.Collections.Generic;

public class GameLogicTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static Player[] MakePlayers()
    {
        var players = new Player[4];
        for (int i = 1; i <= 4; i++) players[i - 1] = new Player(i);
        return players;
    }

    private static GameState MakeState() => new GameState(MakePlayers());

    /// <summary>
    /// Advance the state to the Play phase so cards can be added to the play zone.
    /// </summary>
    private static void SetPlayPhase(GameState state) => state.AdvancePhase();

    // ── Card Scoring ──────────────────────────────────────────────────────

    [Test]
    public void CardScoring_HiddenHand_FaceValue()
    {
        Assert.AreEqual(2,  new Card(Suit.Clubs,   Rank.Two  ).GetHiddenHandScore());
        Assert.AreEqual(10, new Card(Suit.Hearts,  Rank.Ten  ).GetHiddenHandScore());
        Assert.AreEqual(11, new Card(Suit.Diamonds,Rank.Jack ).GetHiddenHandScore());
        Assert.AreEqual(12, new Card(Suit.Spades,  Rank.Queen).GetHiddenHandScore());
        Assert.AreEqual(13, new Card(Suit.Clubs,   Rank.King ).GetHiddenHandScore());
        Assert.AreEqual(14, new Card(Suit.Hearts,  Rank.Ace  ).GetHiddenHandScore());
    }

    [Test]
    public void CardScoring_ShownHand_SpecialValues()
    {
        Assert.AreEqual(4,  new Card(Suit.Clubs,   Rank.Two  ).GetShownHandScore());
        Assert.AreEqual(12, new Card(Suit.Hearts,  Rank.Ten  ).GetShownHandScore());
        Assert.AreEqual(13, new Card(Suit.Diamonds,Rank.Jack ).GetShownHandScore());
        Assert.AreEqual(14, new Card(Suit.Spades,  Rank.Queen).GetShownHandScore());
        Assert.AreEqual(5,  new Card(Suit.Clubs,   Rank.King ).GetShownHandScore());
        Assert.AreEqual(10, new Card(Suit.Hearts,  Rank.Ace  ).GetShownHandScore());
    }

    // ── Hand ──────────────────────────────────────────────────────────────

    [Test]
    public void Hand_PlayCard_FromHidden_RemovesCard()
    {
        var hand = new Hand();
        var card = new Card(Suit.Clubs, Rank.Two);
        hand.AddToHidden(card);

        var played = hand.PlayCard(card);

        Assert.AreEqual(card, played);
        Assert.AreEqual(0, hand.HiddenCount);
        Assert.IsFalse(hand.Contains(card));
    }

    [Test]
    public void Hand_RevealCard_MovesHiddenToShown()
    {
        var hand = new Hand();
        var card = new Card(Suit.Hearts, Rank.King);
        hand.AddToHidden(card);

        bool ok = hand.RevealCard(card);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, hand.HiddenCount);
        Assert.AreEqual(1, hand.ShownCount);
        Assert.AreEqual(HandSection.Shown, hand.GetCardSection(card));
    }

    [Test]
    public void Hand_RevealCard_NonexistentCard_ReturnsFalse()
    {
        var hand = new Hand();
        var other = new Card(Suit.Spades, Rank.Ace);

        bool ok = hand.RevealCard(other);

        Assert.IsFalse(ok);
    }

    // ── Trick Determination ───────────────────────────────────────────────

    [Test]
    public void DetermineTrickWinner_ClearWinner_IsCorrect()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        SetPlayPhase(state);

        // Ace(14) > King(13) > Queen(12) > Jack(11), all hidden
        var cards = new[]
        {
            new Card(Suit.Clubs,   Rank.Ace  ),
            new Card(Suit.Hearts,  Rank.King ),
            new Card(Suit.Spades,  Rank.Queen),
            new Card(Suit.Diamonds,Rank.Jack ),
        };
        foreach (var (c, p) in System.Linq.Enumerable.Zip(cards, players, (c, p) => (c, p)))
            state.AddCardToPlayZone(c, p, HandSection.Hidden);

        var order = state.GetPlayOrder(state.GetRoundStarterPlayer()).ToArray();
        var (winner, prize) = GameRules.DetermineTrickWinner(cards, order, state);

        Assert.IsNotNull(winner, "There should be a winner");
        Assert.AreEqual(players[0].ID, winner.ID, "Player 1 (Ace) should win");
        // Prize is highest face-value remaining: King (13)
        Assert.IsNotNull(prize);
        Assert.AreEqual(Rank.King, prize.Rank);
    }

    [Test]
    public void DetermineTrickWinner_TwoWayTie_TiedCardsDiscarded()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        SetPlayPhase(state);

        // Two Aces tie (both score 14 from hidden). Remaining: King, Queen.
        // Winner should be player 3 (King, score 13) since both Aces are discarded.
        var cards = new[]
        {
            new Card(Suit.Clubs,   Rank.Ace  ),   // P1 — tied
            new Card(Suit.Hearts,  Rank.Ace  ),   // P2 — tied
            new Card(Suit.Spades,  Rank.King ),   // P3 — wins (13 after Aces removed)
            new Card(Suit.Diamonds,Rank.Queen),   // P4 — loses
        };
        foreach (var (c, p) in System.Linq.Enumerable.Zip(cards, players, (c, p) => (c, p)))
            state.AddCardToPlayZone(c, p, HandSection.Hidden);

        var order = state.GetPlayOrder(state.GetRoundStarterPlayer()).ToArray();
        var (winner, prize) = GameRules.DetermineTrickWinner(cards, order, state);

        Assert.IsNotNull(winner, "King should win after Ace-tie discards");
        Assert.AreEqual(players[2].ID, winner.ID);
        // Only remaining card is Queen — that's the prize
        Assert.IsNotNull(prize);
        Assert.AreEqual(Rank.Queen, prize.Rank);
    }

    [Test]
    public void DetermineTrickWinner_ThreeWayTie_NoWinner()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        SetPlayPhase(state);

        // Three Aces tie → all four cards discarded, no winner.
        var cards = new[]
        {
            new Card(Suit.Clubs,   Rank.Ace),
            new Card(Suit.Hearts,  Rank.Ace),
            new Card(Suit.Spades,  Rank.Ace),
            new Card(Suit.Diamonds,Rank.Two),
        };
        foreach (var (c, p) in System.Linq.Enumerable.Zip(cards, players, (c, p) => (c, p)))
            state.AddCardToPlayZone(c, p, HandSection.Hidden);

        var order = state.GetPlayOrder(state.GetRoundStarterPlayer()).ToArray();
        var (winner, prize) = GameRules.DetermineTrickWinner(cards, order, state);

        Assert.IsNull(winner, "3-way tie should produce no winner");
        Assert.IsNull(prize,  "3-way tie should produce no prize");
    }

    [Test]
    public void DetermineTrickWinner_FourWayTie_NoWinner()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        SetPlayPhase(state);

        // All four cards score 14 (Shown Queens = 14, Hidden Aces = 14).
        // Using four Aces from hidden for simplicity.
        var cards = new[]
        {
            new Card(Suit.Clubs,   Rank.Ace),
            new Card(Suit.Hearts,  Rank.Ace),
            new Card(Suit.Spades,  Rank.Ace),
            new Card(Suit.Diamonds,Rank.Ace),
        };
        foreach (var (c, p) in System.Linq.Enumerable.Zip(cards, players, (c, p) => (c, p)))
            state.AddCardToPlayZone(c, p, HandSection.Hidden);

        var order = state.GetPlayOrder(state.GetRoundStarterPlayer()).ToArray();
        var (winner, prize) = GameRules.DetermineTrickWinner(cards, order, state);

        Assert.IsNull(winner, "4-way tie should produce no winner");
        Assert.IsNull(prize,  "4-way tie should produce no prize");
    }

    [Test]
    public void DetermineTrickWinner_ShownHandScoring_AppliedCorrectly()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        SetPlayPhase(state);

        // King from Shown = 5. Jack from Hidden = 11. Jack should win.
        var kShown = new Card(Suit.Clubs,   Rank.King);
        var jHid   = new Card(Suit.Hearts,  Rank.Jack);
        var two1   = new Card(Suit.Spades,  Rank.Two );
        var two2   = new Card(Suit.Diamonds,Rank.Two );

        state.AddCardToPlayZone(kShown, players[0], HandSection.Shown  );
        state.AddCardToPlayZone(jHid,   players[1], HandSection.Hidden );
        state.AddCardToPlayZone(two1,   players[2], HandSection.Hidden );
        state.AddCardToPlayZone(two2,   players[3], HandSection.Hidden );

        var cardArr = new[] { kShown, jHid, two1, two2 };
        var order   = state.GetPlayOrder(state.GetRoundStarterPlayer()).ToArray();
        var (winner, prize) = GameRules.DetermineTrickWinner(cardArr, order, state);

        // two1 and two2 tie (both 2), discarded. Remaining: kShown(5) vs jHid(11) → Jack wins.
        Assert.IsNotNull(winner);
        Assert.AreEqual(players[1].ID, winner.ID, "Jack (hidden, 11) should beat Shown King (5)");
    }

    // ── Reveal Validation ─────────────────────────────────────────────────

    [Test]
    public void IsValidReveal_CannotRevealFromSelf()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        players[0].Hand.AddToHidden(new Card(Suit.Clubs, Rank.Two));
        var card = players[0].Hand.GetHiddenCards()[0];

        bool result = GameRules.IsValidReveal(players[0], players[0], card, state);

        Assert.IsFalse(result, "Player should not be able to reveal from their own hand");
    }

    [Test]
    public void IsValidReveal_TargetWithNoHiddenCards_Invalid()
    {
        var state   = MakeState();
        var players = state.GetAllPlayers();
        // Player 1 (revealer) has a hidden card; Player 2 (target) has none.
        players[0].Hand.AddToHidden(new Card(Suit.Clubs, Rank.Two));
        var someCard = new Card(Suit.Hearts, Rank.Ace);

        bool result = GameRules.IsValidReveal(players[0], players[1], someCard, state);

        Assert.IsFalse(result, "Cannot reveal from a player with no hidden cards");
    }

    // ── Round Starter ─────────────────────────────────────────────────────

    [Test]
    public void GameState_RoundStarter_DoesNotAutoIncrementOnAdvance()
    {
        // AdvanceRound() must not touch RoundStarter — GameManager owns that.
        var state = MakeState();
        int initialStarter = state.RoundStarter;

        // Simulate a full round-trip through all 4 phases
        state.AdvancePhase(); // Reveal → Play
        state.AdvancePhase(); // Play → Determine
        state.AdvancePhase(); // Determine → Score
        state.AdvancePhase(); // Score → Reveal (next round, calls AdvanceRound internally)

        Assert.AreEqual(initialStarter, state.RoundStarter,
            "RoundStarter should not change automatically — GameManager must set it explicitly");
    }

    [Test]
    public void GameState_SetRoundStarter_IsRespected()
    {
        var state = MakeState();
        state.SetRoundStarter(3);
        Assert.AreEqual(3, state.RoundStarter);
    }
}
