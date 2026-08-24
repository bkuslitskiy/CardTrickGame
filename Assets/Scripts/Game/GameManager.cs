using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Orchestrates the complete game loop and phase transitions.
/// Coordinates between GameState, AI players, and UI.
/// This is the central hub for game execution.
/// </summary>
public class GameManager
{
    private GameState _gameState;
    private Deck _deck;
    private Dictionary<int, Difficulty> _playerDifficulties;
    // When true, a phase was paused because a human player is awaiting input and
    // the phase should not advance until the human responds.
    private bool pausedForHumanInput = false;

    // Events for UI to subscribe to
    public event Action<GameState> OnPhaseChanged;
    public event Action<GameState> OnRoundStarted;
    public event Action<Player> OnRevealCard;
    public event Action<Card, Player, HandSection> OnCardPlayed;
    public event Action<Player, Card> OnTrickWon;
    public event Action<GameState> OnGameComplete;

    public GameState CurrentGameState => _gameState;

    public bool IsPausedForHumanInput => pausedForHumanInput;

    /// <summary>
    /// Initialize game manager with difficulty settings.
    /// </summary>
    public GameManager(Difficulty[] playerDifficulties)
    {
        if (playerDifficulties == null || playerDifficulties.Length != 4)
            throw new ArgumentException("Must specify difficulty for all 4 players");

        _playerDifficulties = new Dictionary<int, Difficulty>();
        for (int i = 0; i < 4; i++)
        {
            _playerDifficulties[i + 1] = playerDifficulties[i];
        }

        _deck = new Deck();
    }

    /// <summary>
    /// Start a new game.
    /// </summary>
    public void StartGame()
    {
        // Create players
        Player[] players = new Player[4];
        for (int i = 1; i <= 4; i++)
        {
            players[i - 1] = new Player(i);
        }

        // Replay Deck & AI Forcing Injection
        if (ReplayManager.Instance != null && ReplayManager.Instance.IsReplaying)
        {
            _deck.SetCards(ReplayManager.Instance.CurrentRecord.DeckSuits, ReplayManager.Instance.CurrentRecord.DeckRanks);
            for (int i = 1; i <= 4; i++) players[i - 1].AIImplementation = new ReplayPlayer(i);
        }
        else
        {
            _deck.Reset();
            if (ReplayManager.Instance != null) ReplayManager.Instance.StartRecording(_playerDifficulties.Values.ToArray(), GameManagerController.Instance.HumanPlayerId, _deck.GetCards());
            for (int i = 1; i <= 4; i++) players[i - 1].AIImplementation = AIFactory.CreateAI(i, _playerDifficulties[i]);
        }

        // Deal initial hands
        for (int i = 0; i < 4; i++)
        {
            List<Card> hand = _deck.Deal(13);
            foreach (Card card in hand)
            {
                players[i].Hand.AddToHidden(card);
            }
        }

        _gameState = new GameState(players);

        OnRoundStarted?.Invoke(_gameState);
    }

    /// <summary>
    /// Execute current phase of the game.
    /// </summary>
    public void ExecutePhase()
    {
        if (_gameState == null)
            throw new InvalidOperationException("Game not started");

        if (pausedForHumanInput) return;

        switch (_gameState.CurrentPhase)
        {
            case GamePhase.Reveal:
                ExecuteRevealPhase();
                break;
            case GamePhase.Play:
                ExecutePlayPhase();
                break;
            case GamePhase.Determine:
                ExecuteDeterminePhase();
                break;
            case GamePhase.Score:
                ExecuteScorePhase();
                break;
            case GamePhase.Complete:
                ReplayManager.Instance?.FinishMatch();
                OnGameComplete?.Invoke(_gameState);
                break;
        }

        // If a human player forced a pause during phase execution, do not advance.
        if (pausedForHumanInput)
        {
            Debug.Log("GameManager: Phase execution paused waiting for human input. Not advancing phase.");
            return;
        }

        // If in Play phase and not all 4 cards have been played, pause so GameLoopManager creates a delay between turns
        if (_gameState.CurrentPhase == GamePhase.Play && _gameState.GetPlayZoneCards().Count < 4)
        {
            return;
        }

        var previousPhase = _gameState.CurrentPhase;

        _gameState.AdvancePhase();

        if (previousPhase == GamePhase.Score)
        {
            // Leadership rotates one seat clockwise from the PREVIOUS LEADER every
            // round, regardless of the trick's outcome — who won, and whether a
            // prize was taken, are irrelevant. GetNextPlayer() walks clockwise, the
            // same order GetPlayOrder() follows, so each player leads once every
            // four rounds.
            Player nextStarter = _gameState.GetNextPlayer(_gameState.GetRoundStarterPlayer());
            _gameState.SetRoundStarter(nextStarter.ID);
        }

        OnPhaseChanged?.Invoke(_gameState);
    }

    /// <summary>
    /// Execute reveal phase: reveal a card from another player.
    /// </summary>
    private void ExecuteRevealPhase()
    {
        Player roundStarter = _gameState.GetRoundStarterPlayer();
        List<Player> validTargets = _gameState.GetValidRevealTargets(roundStarter);

        if (validTargets.Count == 0)
        {
            // No cards to reveal this round
            return;
        }

        // Get AI decision
        IPlayer starterAI = roundStarter.AIImplementation;
        int targetPlayerId = starterAI.SelectPlayerToRevealFrom(_gameState);
        
        if (starterAI is HumanPlayer humanAI1 && humanAI1.IsAwaitingInput)
        {
            pausedForHumanInput = true;
            return;
        }

        ReplayManager.Instance?.RecordAction(roundStarter.ID, ReplayActionType.RevealTarget, targetPlayerId);

        if (targetPlayerId < 1 || targetPlayerId > 4)
            return;

        Player targetPlayer = _gameState.GetPlayer(targetPlayerId);
        if (targetPlayer == null || !validTargets.Contains(targetPlayer))
            return;

        // Get card to reveal
        List<Card> validCards = _gameState.GetValidRevealCards(targetPlayer);
        if (validCards.Count == 0)
            return;

        Card cardToReveal = starterAI.SelectCardToReveal(_gameState, targetPlayerId);
        if (cardToReveal == null || !validCards.Contains(cardToReveal))
            cardToReveal = validCards[0]; // Fallback

        ReplayManager.Instance?.RecordAction(roundStarter.ID, ReplayActionType.RevealCard, 0, cardToReveal);

        // Execute reveal
        targetPlayer.Hand.RevealCard(cardToReveal);
        OnRevealCard?.Invoke(targetPlayer);

        starterAI.ObserveGameState(_gameState);
    }

    /// <summary>
    /// Execute play phase: each player plays a card in order.
    /// </summary>
    private void ExecutePlayPhase()
    {
        Player roundStarter = _gameState.GetRoundStarterPlayer();
        List<Player> playOrder = _gameState.GetPlayOrder(roundStarter);
        List<Card> playZoneCards = _gameState.GetPlayZoneCards();

        if (playZoneCards.Count >= 4) return;

        Player player = playOrder[playZoneCards.Count];
        IPlayer playerAI = player.AIImplementation;
        List<Card> validCards = _gameState.GetValidPlayCards(player);

        if (validCards.Count == 0)
            return; // No cards to play (shouldn't happen)

        // If this player is a human and is currently awaiting input, pause the phase
        if (playerAI is HumanPlayer humanAI && humanAI.IsAwaitingInput)
        {
            pausedForHumanInput = true;
            Debug.Log($"GameManager: Pausing ExecutePlayPhase - waiting for human Player {player.ID} input");
            return;
        }

        Card cardToPlay = playerAI.SelectCardToPlay(_gameState);
        if (cardToPlay == null || !validCards.Contains(cardToPlay))
            cardToPlay = validCards[0]; // Fallback to first valid card

        ReplayManager.Instance?.RecordAction(player.ID, ReplayActionType.PlayCard, 0, cardToPlay);

        // Determine if card was from Hidden or Shown BEFORE playing it
        HandSection? section = player.Hand.GetCardSection(cardToPlay);

        Card playedCard = player.PlayCard(cardToPlay);
        if (playedCard != null)
        {
            _gameState.AddCardToPlayZone(playedCard, player, section ?? HandSection.Hidden);
            OnCardPlayed?.Invoke(playedCard, player, section ?? HandSection.Hidden);

            playerAI.ObserveGameState(_gameState);
        }
    }

    /// <summary>
    /// Execute determine phase: evaluate cards and award trick.
    /// </summary>
    private void ExecuteDeterminePhase()
    {
        List<Card> playZoneCards = _gameState.GetPlayZoneCards();
        
        if (playZoneCards.Count == 0)
            return; // Trick was already scored and acknowledged
            
        Player[] playZoneOwners = new Player[4];
        
        // Map cards to players
        var playZoneWithOrigins = _gameState.GetPlayZoneWithOrigins();
        for (int i = 0; i < playZoneCards.Count; i++)
        {
            playZoneOwners[i] = playZoneWithOrigins[playZoneCards[i]];
        }

        Player[] playOrder = _gameState.GetPlayOrder(_gameState.GetRoundStarterPlayer()).ToArray();

        // Determine winner
        (Player winner, Card prizeCard) = GameRules.DetermineTrickWinner(
            playZoneCards.ToArray(),
            playOrder,
            _gameState
        );

        if (winner != null && prizeCard != null)
        {
            winner.AddToScoringZone(prizeCard);
        }

        OnTrickWon?.Invoke(winner, prizeCard);

        bool hasHuman = _gameState.GetAllPlayers().Any(p => p.AIImplementation is HumanPlayer);
        if (hasHuman)
        {
            pausedForHumanInput = true;
        }
        else
        {
            _gameState.ClearPlayZone();
        }
    }

    /// <summary>
    /// Execute score phase: prepare for next round.
    /// </summary>
    private void ExecuteScorePhase()
    {
        // Score phase is mainly bookkeeping
        // All scoring already happened in Determine phase
        // Just prepare for next round if needed
    }

    public void AcknowledgeTrick()
    {
        if (_gameState.CurrentPhase == GamePhase.Determine)
        {
            _gameState.ClearPlayZone();
            pausedForHumanInput = false;
        }
    }

    public void Resume()
    {
        pausedForHumanInput = false;
    }

    /// <summary>
    /// Get final game results.
    /// </summary>
    public List<(Player player, int score)> GetFinalResults()
    {
        if (!_gameState.IsGameComplete())
            throw new InvalidOperationException("Game not yet complete");

        return GameRules.GetFinalScores(_gameState);
    }

    /// <summary>
    /// Run a complete game to completion (automated).
    /// </summary>
    public void RunCompleteGame()
    {
        StartGame();

        while (!_gameState.IsGameComplete())
        {
            ExecutePhase();
        }
    }

    public override string ToString()
    {
        return _gameState?.ToString() ?? "No active game";
    }
}
