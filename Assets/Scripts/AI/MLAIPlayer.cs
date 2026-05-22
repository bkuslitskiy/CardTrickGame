using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stub for ML-Agents Integration.
/// Requires the Unity ML-Agents package to be installed.
///
/// To activate:
///   1. In Package Manager install "ML Agents" (com.unity.ml-agents).
///   2. Remove the outer /* ... */ block below.
///   3. Uncomment the using directives.
///   4. Register MLAIPlayer as a difficulty option in AIFactory.
/// </summary>

/* // <--- REMOVE THIS LINE ONCE UNITY ML-AGENTS IS INSTALLED

using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class MLAIPlayer : Agent, IPlayer
{
    private int _playerId;
    private GameState _currentGameState;
    private List<Card> _currentValidCards;
    private List<Player> _currentValidPlayers;

    public void Initialize(int playerId)
    {
        _playerId = playerId;
    }

    public void ObserveGameState(GameState gameState)
    {
        _currentGameState = gameState;
    }

    public int SelectPlayerToRevealFrom(GameState gameState)
    {
        _currentGameState = gameState;
        _currentValidPlayers = gameState.GetValidRevealTargets(gameState.GetPlayer(_playerId));

        if (_currentValidPlayers.Count == 0) return -1;

        RequestDecision();
        return _currentValidPlayers[Random.Range(0, _currentValidPlayers.Count)].ID;
    }

    public Card SelectCardToReveal(GameState gameState, int targetPlayerId)
    {
        // Hidden cards are indistinguishable — no NN query needed.
        Player target     = gameState.GetPlayer(targetPlayerId);
        var validCards    = gameState.GetValidRevealCards(target);
        return validCards.Count > 0 ? validCards[Random.Range(0, validCards.Count)] : null;
    }

    public Card SelectCardToPlay(GameState gameState)
    {
        _currentGameState  = gameState;
        _currentValidCards = gameState.GetValidPlayCards(gameState.GetPlayer(_playerId));

        if (_currentValidCards.Count == 0) return null;

        RequestDecision();
        return _currentValidCards[Random.Range(0, _currentValidCards.Count)];
    }

    // ── Observation Space ──────────────────────────────────────────────
    // Total observation count (update this header whenever the vector changes):
    //   Round progress      :  1
    //   Play zone count     :  1
    //   Score differential  :  4  (self + 3 opponents)
    //   Own hand            : 13 cards × 3 floats (faceValue, section, playScore) = 39
    //   Opponents' shown    :  3 opponents × 13 slots × 3 floats (rank, exists, faceValue) = 117
    //   Discard pile counts : 13 ranks × 1 float each = 13 (fraction discarded per rank)
    //   Play zone state     :  4 slots × 2 floats (playScore, exists) = 8
    // ─────────────────────────────────────────────────────────────────
    //   TOTAL ≈ 183 floats
    // ─────────────────────────────────────────────────────────────────

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_currentGameState == null) return;

        Player me = _currentGameState.GetPlayer(_playerId);

        // ── Round progress (1) ──────────────────────────────────────────
        sensor.AddObservation((float)_currentGameState.CurrentRound / GameState.TOTAL_ROUNDS);

        // ── Play zone count (1) ─────────────────────────────────────────
        sensor.AddObservation(_currentGameState.GetPlayZoneCount() / 4f);

        // ── Scores (4): self first, then opponents in ID order ──────────
        sensor.AddObservation(me.GetScore() / 200f); // normalised
        foreach (Player p in _currentGameState.GetAllPlayers())
            if (p.ID != _playerId)
                sensor.AddObservation(p.GetScore() / 200f);

        // ── Own hand (up to 13 cards × 3 floats each = 39 max) ─────────
        // Pad with zeros for missing slots so the vector size is fixed.
        var ownCards = me.Hand.GetAllCards();
        for (int i = 0; i < GameState.CARDS_PER_HAND; i++)
        {
            if (i < ownCards.Count)
            {
                Card c = ownCards[i];
                HandSection sec = me.Hand.GetCardSection(c) ?? HandSection.Hidden;
                sensor.AddObservation(c.GetFaceValue() / 14f);
                sensor.AddObservation(sec == HandSection.Shown ? 1f : 0f);
                sensor.AddObservation(GameRules.ScoreCard(c, sec) / 14f);
            }
            else
            {
                sensor.AddObservation(0f); // faceValue
                sensor.AddObservation(0f); // section
                sensor.AddObservation(0f); // playScore
            }
        }

        // ── Opponents' shown cards (3 opponents × 13 slots × 3 floats) ──
        // Shown hands are public information — the agent must see them.
        // Padded so each opponent always contributes exactly 13 × 3 = 39 floats.
        foreach (Player opp in _currentGameState.GetAllPlayers())
        {
            if (opp.ID == _playerId) continue;

            var shown = opp.Hand.GetShownCards();
            for (int i = 0; i < GameState.CARDS_PER_HAND; i++)
            {
                if (i < shown.Count)
                {
                    Card c = shown[i];
                    sensor.AddObservation(1f);               // slot occupied
                    sensor.AddObservation(c.GetFaceValue() / 14f);
                    sensor.AddObservation(c.GetShownHandScore() / 14f);
                }
                else
                {
                    sensor.AddObservation(0f); // slot empty
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
        }

        // ── Discard information (13 rank buckets) ───────────────────────
        // For each rank (2–A), report what fraction of the 4 copies has been
        // discarded. Lets the agent track card counting.
        var discarded = _currentGameState.GetDiscardedCards();
        foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
        {
            int count = discarded.Count(c => c.Rank == rank);
            sensor.AddObservation(count / 4f); // max 4 of any rank in a standard deck
        }

        // ── Current play zone (4 slots × 2 floats) ─────────────────────
        var playZone = _currentGameState.GetPlayZoneCards();
        var sections = _currentGameState.GetPlayZoneWithSections();
        for (int i = 0; i < GameState.PLAYER_COUNT; i++)
        {
            if (i < playZone.Count)
            {
                Card c = playZone[i];
                HandSection sec = sections.ContainsKey(c) ? sections[c] : HandSection.Hidden;
                sensor.AddObservation(1f); // slot occupied
                sensor.AddObservation(GameRules.ScoreCard(c, sec) / 14f);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
    }

    // ── Action Handling ────────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_currentGameState == null) return;

        if (_currentGameState.CurrentPhase == GamePhase.Complete)
        {
            Player me             = _currentGameState.GetPlayer(_playerId);
            float myScore         = me.GetScore();
            float topOpponentScore = _currentGameState.GetAllPlayers()
                                        .Where(p => p.ID != _playerId)
                                        .Max(p => p.GetScore());

            AddReward(myScore > topOpponentScore ? 1.0f : -1.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual override for baseline testing — always picks action 0.
        actionsOut.DiscreteActions[0] = 0;
    }
}

*/ // <--- REMOVE THIS LINE ONCE UNITY ML-AGENTS IS INSTALLED
