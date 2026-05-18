using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// Main game board UI. Displays 4 player hands, play zone, and score tracking.
/// </summary>
public class GameBoardUI : MonoBehaviour
{
    [SerializeField] private HandDisplay[] playerHands = new HandDisplay[4];
    [SerializeField] private Transform playZoneContainer;
    [SerializeField] private Text phaseDisplayText;
    [SerializeField] private Text roundDisplayText;
    [SerializeField] private Text[] playerScoreTexts = new Text[4];
    [SerializeField] private Button[] playerSelectionButtons = new Button[4];
    [SerializeField] private Image roundWinnerIndicator;
    private Image[] playerPanels = new Image[4];
    
    private GameManagerController gameManagerController;
    private List<CardDisplay> cardsInPlayZone = new List<CardDisplay>();
    private GamePhase currentPhase;
    private int currentRound;

    /// <summary>
    /// Apply runtime-provided references to the board UI. This is a safe public setter
    /// that avoids fragile reflection wiring at runtime.
    /// </summary>
    public void ApplyRuntimeBindings(HandDisplay[] hands, Transform playZone, Text phaseDisplay, Text roundDisplay, Text[] scoreTexts, Button[] selectionButtons, Image roundWinner)
    {
        if (hands != null) playerHands = hands;
        if (playZone != null) playZoneContainer = playZone;
        if (phaseDisplay != null) phaseDisplayText = phaseDisplay;
        if (roundDisplay != null) roundDisplayText = roundDisplay;
        if (scoreTexts != null) playerScoreTexts = scoreTexts;
        if (selectionButtons != null) playerSelectionButtons = selectionButtons;
        if (roundWinner != null) roundWinnerIndicator = roundWinner;

        // Store panels for turn highlighting
        if (playerHands != null)
        {
            for (int i = 0; i < playerHands.Length; i++)
                if (playerHands[i] != null) playerPanels[i] = playerHands[i].GetComponentInParent<Image>();
        }
        Debug.Log("GameBoardUI: ApplyRuntimeBindings applied");
    }
    
    public void Initialize(GameManagerController manager)
    {
        gameManagerController = manager;

        // Diagnostic logging to help trace runtime wiring
        Debug.Log($"GameBoardUI.Initialize: playerHands={playerHands?.Length ?? 0}, playZoneContainer={(playZoneContainer!=null)}, phaseDisplayText={(phaseDisplayText!=null)}, roundDisplayText={(roundDisplayText!=null)}");
        
        // Initialize hand displays for each player
        for (int i = 0; i < 4; i++)
        {
            if (playerHands != null && i < playerHands.Length && playerHands[i] != null)
            {
                playerHands[i].Initialize(i);
                Debug.Log($"GameBoardUI: Initialized hand display for player index {i}");
            }
            else
            {
                Debug.LogWarning($"GameBoardUI: playerHands[{i}] is null or not assigned");
            }
        }
        
        // Subscribe to manager events to keep UI in sync
        if (gameManagerController != null)
        {
            gameManagerController.OnPhaseChanged += (phase) => UpdatePhaseDisplay(phase);
            gameManagerController.OnRoundStarted += (round) => UpdateRoundDisplay(round);
            gameManagerController.OnCardPlayed += (card, playerId, section) => DisplayCardInPlayZone(card, playerId);
            gameManagerController.OnRevealCard += (player) => RefreshAllHands(gameManagerController.GetGameState().GetAllPlayers());
            gameManagerController.OnTrickWon += (player, card) => 
            {
                UpdatePlayerScore(player.ID, player.GetScore());
                DisplayRoundWinner(player);
            };
            gameManagerController.OnPhaseChanged += (phase) => {
                HighlightActivePlayer(gameManagerController.GetGameState());
            };
            gameManagerController.OnGameComplete += () => DisplayGameResults(gameManagerController.GetGameState().GetAllPlayers());
        }

        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Update phase display text
    /// </summary>
    public void UpdatePhaseDisplay(GamePhase phase)
    {
        currentPhase = phase;
        if (phaseDisplayText != null)
            phaseDisplayText.text = $"Phase: {phase}";
    }
    
    /// <summary>
    /// Update round display
    /// </summary>
    public void UpdateRoundDisplay(int round)
    {
        currentRound = round;
        if (roundDisplayText != null)
            roundDisplayText.text = $"Round: {round} / 13";
    }
    
    /// <summary>
    /// Update player's score display
    /// </summary>
    public void UpdatePlayerScore(int playerId, int score)
    {
        int idx = playerId - 1;
        if (idx >= 0 && idx < playerScoreTexts.Length && playerScoreTexts[idx] != null)
            playerScoreTexts[idx].text = $"Score: {score}";
    }
    
    /// <summary>
    /// Display a card in the play zone
    /// </summary>
    public void DisplayCardInPlayZone(Card card, int playerId)
    {
        CardDisplay cardDisplay = PrefabFactory.CreateCardDisplayInstance();
        cardDisplay.SetCard(card);
        if (playZoneContainer != null)
        {
            // Try to place card in player-specific slot if present
            var slot = playZoneContainer.Find($"PlayerSlot_{playerId}");

            RectTransform rt = cardDisplay.GetComponent<RectTransform>();
            if (rt == null)
            {
                cardDisplay.transform.SetParent(playZoneContainer, false);
                cardsInPlayZone.Add(cardDisplay);
                return;
            }

            // We'll parent to playZoneContainer during animation, then reparent to slot when complete
            cardDisplay.transform.SetParent(playZoneContainer, false);

            Vector2 targetAnchoredPos = Vector2.zero;
            Vector2 finalLocalPosInSlot = Vector2.zero;

            if (slot != null)
            {
                var slotRT = slot.GetComponent<RectTransform>();
                int existing = slot.childCount; // number of already-present cards
                int idx = existing;
                float spacing = 30f;
                float start = -((existing) * spacing) / 2f;
                int playerIndex = playerId - 1;

                if (playerIndex == 0 || playerIndex == 2)
                {
                    finalLocalPosInSlot = new Vector2(start + idx * spacing, 0f);
                }
                else
                {
                    finalLocalPosInSlot = new Vector2(0f, start + idx * spacing);
                }

                // Compute target anchored position in playZoneContainer coordinate space
                targetAnchoredPos = slotRT.anchoredPosition + finalLocalPosInSlot;
            }
            else
            {
                // No slot - just center
                targetAnchoredPos = Vector2.zero;
            }

            // Start from center of play zone (or small offset) and animate to slot
            Vector2 startAnchoredPos = Vector2.zero;
            rt.anchoredPosition = startAnchoredPos;

            // Animate if available
            if (AnimationController.Instance != null)
            {
                AnimationController.Instance.AnimateCardPlay(cardDisplay, targetAnchoredPos, () =>
                {
                    // When complete, reparent into slot (if exists) and set final local pos
                    if (slot != null)
                    {
                        cardDisplay.transform.SetParent(slot, false);
                        var finalRT = cardDisplay.GetComponent<RectTransform>();
                        if (finalRT != null)
                            finalRT.anchoredPosition = finalLocalPosInSlot;
                    }
                });
            }
            else
            {
                // No animation available - snap directly
                rt.anchoredPosition = targetAnchoredPos;
                if (slot != null)
                {
                    cardDisplay.transform.SetParent(slot, false);
                    rt.anchoredPosition = finalLocalPosInSlot;
                }
            }

            cardsInPlayZone.Add(cardDisplay);
        }
    }
    
    /// <summary>
    /// Clear the play zone (after determination phase)
    /// </summary>
    public void ClearPlayZone()
    {
        foreach (CardDisplay card in cardsInPlayZone)
            Destroy(card.gameObject);
        cardsInPlayZone.Clear();
    }
    
    /// <summary>
    /// Display round winner announcement
    /// </summary>
    public void DisplayRoundWinner(Player winner)
    {
        if (roundWinnerIndicator != null)
        {
            roundWinnerIndicator.enabled = true;
            // Position indicator at winning player's position
            RectTransform rect = roundWinnerIndicator.GetComponent<RectTransform>();
            if (rect != null && winner != null)
            {
                // Adjust position based on player ID
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }
    
    /// <summary>
    /// Display final game results
    /// </summary>
    public void DisplayGameResults(Player[] finalScores)
    {
        Debug.Log("Game Complete!");
        if (finalScores == null || finalScores.Length == 0)
        {
            Debug.Log("No final scores provided.");
            return;
        }

        // Create an overlay panel
        var overlay = new GameObject("EndGamePanel");
        overlay.transform.SetParent(this.transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.75f);
        var overlayRect = overlayImg.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        // Header
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(overlay.transform, false);
        var headerText = headerGO.AddComponent<Text>();
        headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        headerText.fontSize = 28;
        headerText.alignment = TextAnchor.UpperCenter;
        headerText.color = Color.white;
        headerText.text = "Game Over";
        var headerRT = headerText.rectTransform;
        headerRT.anchorMin = new Vector2(0.5f, 1f);
        headerRT.anchorMax = new Vector2(0.5f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -20f);

        // Score lines
        for (int i = 0; i < finalScores.Length; i++)
        {
            var lineGO = new GameObject($"Result_{i}");
            lineGO.transform.SetParent(overlay.transform, false);
            var lineText = lineGO.AddComponent<Text>();
            lineText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lineText.fontSize = 18;
            lineText.alignment = TextAnchor.UpperCenter;
            lineText.color = Color.white;
            lineText.text = $"Player {i + 1}: {finalScores[i].GetScore()}";
            var lineRT = lineText.rectTransform;
            lineRT.anchorMin = new Vector2(0.5f, 1f);
            lineRT.anchorMax = new Vector2(0.5f, 1f);
            lineRT.pivot = new Vector2(0.5f, 1f);
            lineRT.anchoredPosition = new Vector2(0f, -60f - (i * 22));
        }

        // Play Again button
        var playAgainGO = new GameObject("PlayAgainButton");
        playAgainGO.transform.SetParent(overlay.transform, false);
        var playImg = playAgainGO.AddComponent<Image>();
        playImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
        var playBtn = playAgainGO.AddComponent<Button>();
        var playRT = playImg.rectTransform;
        playRT.anchorMin = new Vector2(0.5f, 0f);
        playRT.anchorMax = new Vector2(0.5f, 0f);
        playRT.pivot = new Vector2(0.5f, 0f);
        playRT.anchoredPosition = new Vector2(-80f, 20f);
        playRT.sizeDelta = new Vector2(140f, 40f);
        var playLabelGO = new GameObject("Label");
        playLabelGO.transform.SetParent(playAgainGO.transform, false);
        var playLabel = playLabelGO.AddComponent<Text>();
        playLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playLabel.alignment = TextAnchor.MiddleCenter;
        playLabel.color = Color.white;
        playLabel.text = "Main Menu";
        var playLabelRT = playLabel.rectTransform;
        playLabelRT.anchorMin = Vector2.zero;
        playLabelRT.anchorMax = Vector2.one;
        playLabelRT.offsetMin = Vector2.zero;
        playLabelRT.offsetMax = Vector2.zero;

        playBtn.onClick.AddListener(() =>
        {
            var gm = GameManagerController.Instance;
            if (gm != null)
            {
                var loop = GameLoopManager.Instance;
                if (loop != null) loop.StopGameLoop();
                UIManager.Instance?.ShowMenu();
                UIManager.Instance?.CancelSelection();
            }

            Destroy(overlay);
        });

        // Back to Menu button
        var backGO = new GameObject("BackToMenuButton");
        backGO.transform.SetParent(overlay.transform, false);
        var backImg = backGO.AddComponent<Image>();
        backImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
        var backBtn = backGO.AddComponent<Button>();
        var backRT = backImg.rectTransform;
        backRT.anchorMin = new Vector2(0.5f, 0f);
        backRT.anchorMax = new Vector2(0.5f, 0f);
        backRT.pivot = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = new Vector2(80f, 20f);
        backRT.sizeDelta = new Vector2(140f, 40f);
        var backLabelGO = new GameObject("Label");
        backLabelGO.transform.SetParent(backGO.transform, false);
        var backLabel = backLabelGO.AddComponent<Text>();
        backLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        backLabel.alignment = TextAnchor.MiddleCenter;
        backLabel.color = Color.white;
        backLabel.text = "Back to Menu";
        var backLabelRT = backLabel.rectTransform;
        backLabelRT.anchorMin = Vector2.zero;
        backLabelRT.anchorMax = Vector2.one;
        backLabelRT.offsetMin = Vector2.zero;
        backLabelRT.offsetMax = Vector2.zero;

        backBtn.onClick.AddListener(() =>
        {
            var ui = UIManager.Instance;
            if (ui != null)
                ui.ShowMenu();

            var loop = GameLoopManager.Instance;
            if (loop != null)
                loop.StopGameLoop();

            Destroy(overlay);
        });
    }
    
    /// <summary>
    /// Enable card selection for human player during Play phase
    /// </summary>
    public void EnableCardSelection(Player player, Action<Card> onCardSelected, System.Collections.Generic.List<Card> validCardsList = null)
    {
        int idx = player.ID - 1;
        if (idx >= 0 && idx < playerHands.Length && playerHands[idx] != null)
        {
            playerHands[idx].EnableCardSelection(onCardSelected, validCardsList);
        }
    }
    
    /// <summary>
    /// Disable card selection
    /// </summary>
    public void DisableCardSelection()
    {
        for (int i = 0; i < playerHands.Length; i++)
        {
            if (playerHands[i] != null)
                playerHands[i].DisableCardSelection();
        }
    }
    
    /// <summary>
    /// Enable player selection for reveal target
    /// </summary>
    public void EnablePlayerSelection(Player revealingPlayer, Action<Player> onPlayerSelected, System.Collections.Generic.List<Player> validPlayers = null)
    {
        for (int i = 0; i < playerSelectionButtons.Length; i++)
        {
            var btn = playerSelectionButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();

            // Hide own player button (player IDs are 1-based)
            if (i == revealingPlayer.ID - 1)
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            // Determine player object from GameManagerController's state
            Player target = null;
            var state = gameManagerController?.GetGameState();
            if (state != null)
            {
                var players = state.GetAllPlayers();
                if (i >= 0 && i < players.Length)
                    target = players[i];
            }

            // If validPlayers provided, only enable those
            if (validPlayers != null && !validPlayers.Contains(target))
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            int playerId = i; // capture
            btn.onClick.AddListener(() =>
            {
                var players = gameManagerController?.GetGameState()?.GetAllPlayers();
                Player p = null;
                if (players != null && playerId >= 0 && playerId < players.Length)
                    p = players[playerId];
                onPlayerSelected?.Invoke(p);
            });

            btn.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Disable any player selection UI and remove listeners
    /// </summary>
    public void DisablePlayerSelection()
    {
        if (playerSelectionButtons == null) return;
        for (int i = 0; i < playerSelectionButtons.Length; i++)
        {
            var btn = playerSelectionButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Enable reveal card selection for hidden hand
    /// </summary>
    public void EnableRevealSelection(Player revealingPlayer, Player targetPlayer, Action<Card> onCardSelected, System.Collections.Generic.List<Card> validCardsList = null)
    {
        int idx = targetPlayer.ID - 1;
        if (idx >= 0 && idx < playerHands.Length && playerHands[idx] != null)
        {
            playerHands[idx].EnableRevealSelection(onCardSelected, validCardsList);
        }
    }
    
    /// <summary>
    /// Update hand displays to show current card state
    /// </summary>
    public void RefreshAllHands(Player[] players)
    {
        for (int i = 0; i < players.Length && i < playerHands.Length; i++)
        {
            if (playerHands[i] != null && players[i] != null)
                playerHands[i].RefreshDisplay(players[i].Hand);
        }
    }

    /// <summary>
    /// Highlights the player whose turn it is.
    /// </summary>
    private void HighlightActivePlayer(GameState state)
    {
        if (state == null) return;

        int activeId = -1;
        Player starter = state.GetRoundStarterPlayer();

        if (state.CurrentPhase == GamePhase.Reveal)
        {
            activeId = starter?.ID ?? -1;
        }
        else if (state.CurrentPhase == GamePhase.Play && starter != null)
        {
            var playOrder = state.GetPlayOrder(starter);
            int playedCount = state.GetPlayZoneCards()?.Count ?? 0;
            if (playOrder != null && playedCount < playOrder.Count)
                activeId = playOrder[playedCount].ID;
        }

        for (int i = 0; i < 4; i++)
        {
            bool isActive = (activeId == i + 1);
            HighlightPlayerPanel(i, isActive);
        }
    }

    private void HighlightPlayerPanel(int index, bool active)
    {
        if (playerPanels[index] == null) return;
        playerPanels[index].color = active ? new Color(1f, 0.84f, 0f, 0.3f) : new Color(0f, 0f, 0f, 0.5f);
    }
}
