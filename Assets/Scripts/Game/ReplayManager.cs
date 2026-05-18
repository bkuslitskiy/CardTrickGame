using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReplayActionType { RevealTarget, RevealCard, PlayCard }

[Serializable]
public class ReplayAction {
    public int PlayerId;
    public ReplayActionType Type;
    public int ValueInt; // TargetId
    public Suit CardSuit;
    public Rank CardRank;
}

[Serializable]
public class MatchRecord {
    public Difficulty[] Difficulties;
    public int HumanId;
    public List<Suit> DeckSuits = new List<Suit>();
    public List<Rank> DeckRanks = new List<Rank>();
    public List<ReplayAction> Actions = new List<ReplayAction>();
}

public class ReplayManager : MonoBehaviour {
    public static ReplayManager Instance;
    public MatchRecord CurrentRecord;
    public MatchRecord LastRecord;
    public bool IsReplaying;
    private int replayIndex = 0;

    private void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }

    public void StartRecording(Difficulty[] diffs, int humanId, List<Card> deck) {
        CurrentRecord = new MatchRecord { Difficulties = diffs, HumanId = humanId };
        foreach(var c in deck) { CurrentRecord.DeckSuits.Add(c.Suit); CurrentRecord.DeckRanks.Add(c.Rank); }
        IsReplaying = false;
    }

    public void RecordAction(int pId, ReplayActionType type, int targetId = 0, Card card = null) {
        if (IsReplaying || CurrentRecord == null) return;
        var action = new ReplayAction { PlayerId = pId, Type = type, ValueInt = targetId };
        if (card != null) { action.CardSuit = card.Suit; action.CardRank = card.Rank; }
        CurrentRecord.Actions.Add(action);
    }

    public void FinishMatch() {
        if (!IsReplaying && CurrentRecord != null) { LastRecord = CurrentRecord; CurrentRecord = null; }
    }

    public ReplayAction GetNextAction(int pId, ReplayActionType type) {
        if (CurrentRecord == null || replayIndex >= CurrentRecord.Actions.Count) return null;
        var act = CurrentRecord.Actions[replayIndex];
        if (act.PlayerId == pId && act.Type == type) { replayIndex++; return act; }
        return null;
    }

    public void SetupReplay() {
        IsReplaying = true;
        CurrentRecord = LastRecord;
        replayIndex = 0;
    }
}