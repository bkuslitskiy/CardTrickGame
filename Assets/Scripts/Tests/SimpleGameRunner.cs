using System;
using System.Collections.Generic;
using UnityEngine;

public class SimpleGameRunner
{
    public static void Run()
    {
        Debug.Log("Starting test game run...");
        
        Difficulty[] difficulties = new Difficulty[4] { Difficulty.Easy, Difficulty.Easy, Difficulty.Easy, Difficulty.Easy };
        GameManager gm = new GameManager(difficulties);
        
        try
        {
            gm.StartGame();
            Debug.Log("Game started successfully.");
            
            while (!gm.CurrentGameState.IsGameComplete())
            {
                gm.ExecutePhase();
                Debug.Log($"Phase executed: {gm.CurrentGameState.CurrentPhase}, Round: {gm.CurrentGameState.CurrentRound}");
            }
            
            Debug.Log("Game completed successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Game run failed: {e.Message}");
        }
    }
}
