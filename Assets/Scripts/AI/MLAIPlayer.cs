using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stub for ML-Agents Integration.
/// Requires the Unity ML-Agents package to be installed.
/// </summary>
/* // <--- REMOVE THIS TO ACTIVATE ONCE UNITY ML-AGENTS IS INSTALLED
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
        
        RequestDecision(); // Calls OnActionReceived
        
        // The action buffer will process it async; for strict interface adherence, 
        // ML-Agents handles the step progression dynamically.
        return _currentValidPlayers[Random.Range(0, _currentValidPlayers.Count)].ID;
    }

    public Card SelectCardToReveal(GameState gameState, int targetPlayerId)
    {
        // Hidden cards are functionally identical, no need to query the neural network
        Player target = gameState.GetPlayer(targetPlayerId);
        var validCards = gameState.GetValidRevealCards(target);
        return validCards.Count > 0 ? validCards[Random.Range(0, validCards.Count)] : null;
    }

    public Card SelectCardToPlay(GameState gameState)
    {
        _currentGameState = gameState;
        _currentValidCards = gameState.GetValidPlayCards(gameState.GetPlayer(_playerId));
        
        if (_currentValidCards.Count == 0) return null;
        
        RequestDecision(); // Wakes up neural net to call OnActionReceived
        return _currentValidCards[Random.Range(0, _currentValidCards.Count)];
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_currentGameState == null) return;
        
        Player me = _currentGameState.GetPlayer(_playerId);
        
        // Observe Round Progress (1 float)
        sensor.AddObservation((float)_currentGameState.CurrentRound / GameState.TOTAL_ROUNDS);
        
        // Observe Play Zone count (1 float)
        sensor.AddObservation(_currentGameState.GetPlayZoneCount());

        // Observe My Score and Opponent Scores (4 floats)
        sensor.AddObservation(me.GetScore());
        foreach (Player p in _currentGameState.GetAllPlayers())
        {
            if (p.ID != _playerId) sensor.AddObservation(p.GetScore());
        }
        
        // Encode Hand State
        foreach (Card c in me.Hand.GetAllCards())
        {
            // Observe static Score (Face Value)
            sensor.AddObservation(c.GetFaceValue());
            
            // Observe Hand Section (Value depends on origin: Hidden vs Shown)
            sensor.AddObservation(me.Hand.GetCardSection(c) == HandSection.Shown ? 1.0f : 0.0f);
            
            // Observe dynamic Value based on current hand state
            sensor.AddObservation(GameRules.ScoreCard(c, me.Hand.GetCardSection(c) ?? HandSection.Hidden));
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_currentGameState == null) return;

        int discreteChoice = actions.DiscreteActions[0];

        // Handle Reward State: Called periodically or checked via event
        if (_currentGameState.CurrentPhase == GamePhase.Complete)
        {
            // Reward based on score differential
            Player me = _currentGameState.GetPlayer(_playerId);
            float myScore = me.GetScore();
            float topOpponentScore = _currentGameState.GetAllPlayers().Where(p => p.ID != _playerId).Max(p => p.GetScore());
            
            if (myScore > topOpponentScore)
                AddReward(1.0f); // Win
            else
                AddReward(-1.0f); // Loss
                
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Allows you to manually control the agent using keyboard input for baseline testing
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;
    }
}
*/ // <--- REMOVE THIS TO ACTIVATE