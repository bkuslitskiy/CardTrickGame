using System;
using System.Collections.Generic;
using System.Linq;

public enum GamePhase
{
    Reveal,
    Play,
    Determine,
    Score,
    Complete
}

/// <summary>
/// Central game state machine tracking phase, round, and all cards in play.
/// Immutable where possible; state transitions validated before execution.
/// </summary>
public class GameState
{
    public GamePhase CurrentPhase { get; private set; }
    public int CurrentRound { get; private set; }
    public int RoundStarter { get; private set; }
    
    private Player[] _players;
    private List<Card> _playZone;
    private Dictionary<Card, Player> _playZoneOrigins;
    private Dictionary<Card, HandSection> _playZoneSections;
    private List<Card> _discardedCards;

    public const int TOTAL_ROUNDS = 13;
    public const int PLAYER_COUNT = 4;
    public const int CARDS_PER_HAND = 13;

    public GameState(Player[] players)
    {
        if (players == null || players.Length != PLAYER_COUNT)
            throw new ArgumentException($"Must have exactly {PLAYER_COUNT} players", nameof(players));

        _players = players;
        _playZone = new List<Card>();
        _playZoneOrigins = new Dictionary<Card, Player>();
        _playZoneSections = new Dictionary<Card, HandSection>();
        _discardedCards = new List<Card>();

        CurrentPhase = GamePhase.Reveal;
        CurrentRound = 1;
        RoundStarter = 1;
    }

    /// <summary>
    /// Get player by ID (1-4).
    /// </summary>
    public Player GetPlayer(int id)
    {
        if (id < 1 || id > PLAYER_COUNT)
            return null;
        return _players[id - 1];
    }

    /// <summary>
    /// Get all players.
    /// </summary>
    public Player[] GetAllPlayers()
    {
        return (Player[])_players.Clone();
    }

    /// <summary>
    /// Get current round starter player.
    /// </summary>
    public Player GetRoundStarterPlayer()
    {
        return _players[RoundStarter - 1];
    }

    /// <summary>
    /// Override the round starter player.
    /// </summary>
    public void SetRoundStarter(int playerId)
    {
        if (playerId >= 1 && playerId <= PLAYER_COUNT)
            RoundStarter = playerId;
    }

    /// <summary>
    /// Get next player in clockwise order from given player.
    /// </summary>
    public Player GetNextPlayer(Player player)
    {
        int nextId = player.ID == PLAYER_COUNT ? 1 : player.ID + 1;
        return _players[nextId - 1];
    }

    /// <summary>
    /// Get player order for play phase starting from given player.
    /// </summary>
    public List<Player> GetPlayOrder(Player startPlayer)
    {
        List<Player> order = new List<Player>();
        Player current = startPlayer;
        
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            order.Add(current);
            current = GetNextPlayer(current);
        }
        
        return order;
    }

    /// <summary>
    /// Add card to play zone with associated player and origin section.
    /// </summary>
    public void AddCardToPlayZone(Card card, Player player, HandSection section)
    {
        if (card == null || player == null)
            throw new ArgumentNullException();
        if (_playZone.Contains(card))
            throw new InvalidOperationException("Card already in play zone");

        _playZone.Add(card);
        _playZoneOrigins[card] = player;
        _playZoneSections[card] = section;
    }

    /// <summary>
    /// Get all cards currently in play zone.
    /// </summary>
    public List<Card> GetPlayZoneCards()
    {
        return new List<Card>(_playZone);
    }

    /// <summary>
    /// Get cards in play zone with their origins.
    /// </summary>
    public Dictionary<Card, Player> GetPlayZoneWithOrigins()
    {
        return new Dictionary<Card, Player>(_playZoneOrigins);
    }

    /// <summary>
    /// Get cards in play zone with their origin sections.
    /// </summary>
    public Dictionary<Card, HandSection> GetPlayZoneWithSections()
    {
        return new Dictionary<Card, HandSection>(_playZoneSections);
    }

    /// <summary>
    /// Get play zone count.
    /// </summary>
    public int GetPlayZoneCount()
    {
        return _playZone.Count;
    }

    /// <summary>
    /// Clear play zone after trick determination.
    /// </summary>
    public void ClearPlayZone()
    {
        _playZone.Clear();
        _playZoneOrigins.Clear();
        _playZoneSections.Clear();
    }

    /// <summary>
    /// Add card to discard pile.
    /// </summary>
    public void DiscardCard(Card card)
    {
        if (card == null)
            throw new ArgumentNullException(nameof(card));
        _discardedCards.Add(card);
    }

    /// <summary>
    /// Get all discarded cards (useful for RL training).
    /// </summary>
    public List<Card> GetDiscardedCards()
    {
        return new List<Card>(_discardedCards);
    }

    /// <summary>
    /// Get origin player of a card in play zone.
    /// </summary>
    public Player GetCardOriginInPlayZone(Card card)
    {
        return _playZoneOrigins.ContainsKey(card) ? _playZoneOrigins[card] : null;
    }

    /// <summary>
    /// Get origin hand section of a card in play zone.
    /// </summary>
    public HandSection? GetCardSectionInPlayZone(Card card)
    {
        return _playZoneSections.ContainsKey(card) ? _playZoneSections[card] : (HandSection?)null;
    }

    /// <summary>
    /// Get valid cards a player can play.
    /// </summary>
    public List<Card> GetValidPlayCards(Player player)
    {
        if (player == null)
            return new List<Card>();

        return player.Hand.GetAllCards();
    }

    /// <summary>
    /// Get valid players to reveal from (must have Hidden cards).
    /// </summary>
    public List<Player> GetValidRevealTargets(Player revealingPlayer = null)
    {
        return _players.Where(p => p != revealingPlayer && p.Hand.HasHiddenCards()).ToList();
    }

    /// <summary>
    /// Get valid cards to reveal from a target player.
    /// </summary>
    public List<Card> GetValidRevealCards(Player targetPlayer)
    {
        if (targetPlayer == null)
            return new List<Card>();

        return targetPlayer.Hand.GetHiddenCards();
    }

    /// <summary>
    /// Advance to next phase.
    /// </summary>
    public void AdvancePhase()
    {
        CurrentPhase = CurrentPhase switch
        {
            GamePhase.Reveal => GamePhase.Play,
            GamePhase.Play => GamePhase.Determine,
            GamePhase.Determine => GamePhase.Score,
            GamePhase.Score => NextRoundPhase(),
            _ => CurrentPhase
        };
    }

    /// <summary>
    /// Determine next phase after scoring (either Reveal for next round or Complete).
    /// </summary>
    private GamePhase NextRoundPhase()
    {
        if (CurrentRound >= TOTAL_ROUNDS)
            return GamePhase.Complete;

        AdvanceRound();
        return GamePhase.Reveal;
    }

    /// <summary>
    /// Advance to next round.
    /// RoundStarter is intentionally NOT modified here — GameManager owns that
    /// assignment based on trick outcome:
    ///   • Someone won → starter = player to the right of winner
    ///   • Nobody won  → starter stays the same (same player leads again)
    /// </summary>
    private void AdvanceRound()
    {
        CurrentRound++;
    }

    /// <summary>
    /// Check if game is complete.
    /// </summary>
    public bool IsGameComplete()
    {
        return CurrentPhase == GamePhase.Complete;
    }

    /// <summary>
    /// Get game progress as percentage (0-100).
    /// </summary>
    public float GetProgressPercentage()
    {
        return (CurrentRound - 1f) / TOTAL_ROUNDS * 100f;
    }

    public override string ToString()
    {
        return $"Round {CurrentRound}/{TOTAL_ROUNDS}, Phase: {CurrentPhase}, Starter: Player {RoundStarter}";
    }
}
