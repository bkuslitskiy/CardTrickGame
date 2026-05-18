using UnityEngine;
using System;

[Serializable]
public class PlayerStats
{
    public int GamesPlayed;
    public int HumanWins;
    public int HighestScore;
}

/// <summary>
/// Handles persistent stat tracking between game sessions.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    public PlayerStats Stats = new PlayerStats();

    private void Awake()
    {
        Instance = this;
        if (PlayerPrefs.HasKey("CardGameStats"))
            Stats = JsonUtility.FromJson<PlayerStats>(PlayerPrefs.GetString("CardGameStats"));
    }

    public void RecordGame(Player[] finalScores, int humanPlayerId)
    {
        Stats.GamesPlayed++;
        if (humanPlayerId > 0 && humanPlayerId <= 4)
        {
            Player human = finalScores[humanPlayerId - 1]; // Convert 1-4 to 0-3 index
            if (human.GetScore() > Stats.HighestScore) Stats.HighestScore = human.GetScore();
            
            int maxScore = 0;
            foreach (var p in finalScores) if (p.GetScore() > maxScore) maxScore = p.GetScore();
            if (human.GetScore() == maxScore) Stats.HumanWins++;
        }
        PlayerPrefs.SetString("CardGameStats", JsonUtility.ToJson(Stats));
        PlayerPrefs.Save();
    }
}