using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Complete UI Integration layer with full animation support.
/// Handles all display updates, event coordination, and visual feedback.
/// </summary>
public class GameBoardUIEnhanced : MonoBehaviour
{
    [SerializeField] private HandDisplay[] playerHandDisplays = new HandDisplay[4];
    [SerializeField] private Transform playZoneContainer;
    [SerializeField] private Text phaseDisplayText;
    [SerializeField] private Text roundDisplayText;
    [SerializeField] private Text[] playerScoreTexts = new Text[4];
    [SerializeField] private Image[] playerHighlights = new Image[4];
    [SerializeField] private Transform[] playerPanels = new Transform[4];
    [SerializeField] private Transform floatingTextPrefabContainer;
    
    private GameManagerController gameManagerController;
    private InputManagerEnhanced inputManager;
    private AnimationController animationController;
    private PlayZoneAnimator playZoneAnimator;
    
    private List<CardDisplay> currentPlayZoneCards = new List<CardDisplay>();
    private Dictionary<int, int> playerScores = new Dictionary<int, int>();
    private Action<Card> onCardSelected;
    
    public void Initialize(GameManagerController gmController)
    {
        if (gameManagerController == null)
            gameManagerController = gmController;
    }

    public void ApplyRuntimeBindings(HandDisplay[] handDisplays, Transform playZone, Text phaseText, Text roundText, Text[] scoreTexts, UnityEngine.UI.Button[] selectButtons, Image roundWinnerImage)
    {
        this.playerHandDisplays = handDisplays;
        this.playZoneContainer = playZone;
        this.phaseDisplayText = phaseText;
        this.roundDisplayText = roundText;
        this.playerScoreTexts = scoreTexts;
        for (int i = 0; i < 4; i++)
        {
            if (selectButtons != null && i < selectButtons.Length && selectButtons[i] != null)
            {
                this.playerPanels[i] = selectButtons[i].transform;
                this.playerHighlights[i] = selectButtons[i].GetComponent<Image>();
            }
        }
    }

    public void DisplayCardInPlayZone(Card card, int playerId)
    {
        DisplayCardPlayed(card, playerId, (HandSection)0);
    }

    public void DisplayRoundWinner(Player winner)
    {
        // Visual hook for round winner if needed
    }

    public void DisplayGameResults(Player[] finalScores)
    {
        DisplayGameEnd();
    }

    private void Start()
    {
        gameManagerController = GameManagerController.Instance;
        inputManager = InputManagerEnhanced.Instance;
        animationController = AnimationController.Instance;
        
        if (gameManagerController == null)
        {
            Debug.LogError("GameManagerController not found!");
            return;
        }
        
        if (animationController == null)
        {
            Debug.LogError("AnimationController not found!");
            return;
        }
        
        // Get play zone animator
        if (playZoneContainer != null)
        {
            playZoneAnimator = playZoneContainer.GetComponent<PlayZoneAnimator>();
            if (playZoneAnimator == null)
                playZoneAnimator = playZoneContainer.gameObject.AddComponent<PlayZoneAnimator>();
        }
        
        // Subscribe to game events
        gameManagerController.OnPhaseChanged += (phase) => UpdatePhaseDisplay(phase);
        gameManagerController.OnRoundStarted += (round) => {
            UpdateRoundDisplay(round);
            RefreshAllPlayerHands(true);
        };
        gameManagerController.OnCardPlayed += (card, playerId, section) => {
            DisplayCardPlayed(card, playerId, section);
            RefreshAllPlayerHands();
        };
        gameManagerController.OnRevealCard += (player) => {
            DisplayRevealedCard(player);
            RefreshAllPlayerHands();
        };
        gameManagerController.OnTrickWon += (player, card) => DisplayTrickWinner(player, card);
        gameManagerController.OnGameComplete += () => DisplayGameEnd();
        
        // Subscribe to input events
        if (inputManager != null)
            inputManager.OnInvalidMoveAttempted += (reason) => ShowInvalidMoveMessage(reason);
        
        // Initialize player scores
        for (int i = 0; i < 4; i++)
        {
            playerScores[i] = 0;
        }
        
        // Initialize hand displays
        for (int i = 0; i < 4; i++)
        {
            if (playerHandDisplays[i] != null)
            {
                playerHandDisplays[i].Initialize(i);
            }
        }
    }
    
    private void RefreshAllPlayerHands(bool isDeal = false)
    {
        if (gameManagerController == null) return;
        GameState state = gameManagerController.GetGameState();
        if (state == null) return;
        
        Player[] players = state.GetAllPlayers();
        if (players != null)
        {
            RefreshAllHands(players, isDeal);
        }
    }

    public void UpdatePhaseDisplay(GamePhase phase)
    {
        if (phaseDisplayText != null)
        {
            // Update UI based on phase
            switch (phase)
            {
                case GamePhase.Reveal:
                    phaseDisplayText.text = "PHASE: Reveal - Select a card to reveal";
                    break;
                case GamePhase.Play:
                    phaseDisplayText.text = "PHASE: Play - Select a card to play";
                    RefreshAllPlayerHands();
                    break;
                case GamePhase.Determine:
                    phaseDisplayText.text = "PHASE: Determining winner...";
                    break;
                case GamePhase.Score:
                    phaseDisplayText.text = "PHASE: Scoring...";
                    break;
            }
            
            // Animate phase transition
            if (animationController != null)
                animationController.AnimatePhaseTransition(phaseDisplayText);
        }
    }
    
    private void UpdateRoundDisplay(int round)
    {
        if (roundDisplayText != null)
        {
            roundDisplayText.text = $"Round {round} / 13";
        }
    }
    
    private void DisplayCardPlayed(Card card, int playerId, HandSection section)
    {
        if (playZoneContainer == null) return;
        
        // Create card display in play zone (with fallback)
        CardDisplay cardDisplay = PrefabFactory.CreateCardDisplayInstance();
        if (cardDisplay != null)
        {
            cardDisplay.SetCard(card);
            cardDisplay.SetCard(card, section);
            if (playZoneContainer != null)
                cardDisplay.transform.SetParent(playZoneContainer, false);

            // Position card based on player ID (playerId is 1-based)
            RectTransform rect = cardDisplay.GetComponent<RectTransform>();
            if (rect != null)
            {
                // Simple positioning array (0-based indexing)
                Vector2[] positions = new Vector2[4]
                {
                    new Vector2(0, -80),     // Player index 0 (South)
                    new Vector2(-80, 0),     // Player index 1 (West)
                    new Vector2(0, 80),      // Player index 2 (North)
                    new Vector2(80, 0)       // Player index 3 (East)
                };
                int pIdx = Mathf.Clamp(playerId - 1, 0, 3);
                rect.anchoredPosition = positions[pIdx];
            }
            cardDisplay.SetOwnerIndex(playerId);

            currentPlayZoneCards.Add(cardDisplay);

            // Animate card entry
            if (playZoneAnimator != null)
                playZoneAnimator.AnimateCardEnter(cardDisplay);
        }
    }
    
    private void DisplayRevealedCard(Player targetPlayer)
    {
        if (targetPlayer != null)
        {
            int idx = targetPlayer.ID - 1;
            if (idx >= 0 && idx < playerHandDisplays.Length && playerHandDisplays[idx] != null)
            {
                playerHandDisplays[idx].RefreshDisplay(targetPlayer.Hand);
            }
        }
    }
    
    private void DisplayTrickWinner(Player winner, Card prizeCard)
    {
        // Highlight winner with animation
        if (winner != null)
        {
            int idx = winner.ID - 1;
            if (idx >= 0 && idx < playerHighlights.Length && playerHighlights[idx] != null && animationController != null)
            {
                // Play winner animation
                animationController.AnimateTrickWinner(playerHighlights[idx], () =>
                {
                    // Update winner's score
                    int newScore = winner.GetScore();
                    playerScores[idx] = newScore;
                UpdatePlayerScore(winner.ID, newScore);
                });

                // Show floating text effect
                ShowFloatingText("+" + prizeCard.GetFaceValue(), idx, Color.yellow);
                if (UIEffectsManager.Instance != null) UIEffectsManager.Instance.EmitSparks(playerHighlights[idx].transform);
            }
        }

        // Animate prize card to winner
        CardDisplay prizeCardDisplay = currentPlayZoneCards.Find(cd => cd != null && cd.GetCard() == prizeCard);
        if (prizeCardDisplay != null && winner != null && animationController != null)
        {
            int winnerIdx = winner.ID - 1;
            if (winnerIdx >= 0 && winnerIdx < playerScoreTexts.Length && playerScoreTexts[winnerIdx] != null)
            {
                currentPlayZoneCards.Remove(prizeCardDisplay); // Don't let ClearPlayZone destroy it
                animationController.AnimatePrizeCard(prizeCardDisplay, playerScoreTexts[winnerIdx].transform, () => Destroy(prizeCardDisplay.gameObject));
            }
        }

        // Highlight cards
        foreach (CardDisplay cd in currentPlayZoneCards)
        {
            if (winner != null && cd.GetOwnerIndex() == winner.ID)
            {
                cd.SetHighlightColor(Color.green);
                cd.Highlight();
            }
            else if (cd.GetCard() != null && cd.GetCard() == prizeCard)
            {
                cd.SetHighlightColor(Color.yellow);
                cd.Highlight();
            }
            else
            {
                CanvasGroup cg = cd.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = cd.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.5f;
            }
        }

        // Wait for click if human
        bool hasHuman = false;
        var state = gameManagerController.GetGameState();
        if (state != null)
        {
            foreach (var p in state.GetAllPlayers())
                if (p.AIImplementation is HumanPlayer) hasHuman = true;
        }

        if (hasHuman)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.GetType().GetMethod("ShowPrompt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(UIManager.Instance, new object[] { "Trick Complete. Click anywhere to continue." });

            StartCoroutine(WaitForTrickAcknowledge());
        }
        else
        {
            float clearDelay = animationController != null ? animationController.GetTrickWinnerDuration() + 0.5f : 2f;
            StartCoroutine(ClearPlayZoneAfterDelay(clearDelay));
        }
    }

    private IEnumerator WaitForTrickAcknowledge()
    {
        yield return new WaitForSeconds(0.5f); // small delay before allowing click
        while (!Input.GetMouseButtonDown(0))
        {
            yield return null;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.CancelSelection();

        if (gameManagerController != null)
            gameManagerController.AcknowledgeTrick();

        StartCoroutine(ClearPlayZoneAfterDelay(0f));
    }
    
    private void DisplayGameEnd()
    {
        Transform existingOverlay = transform.Find("EndGameOverlay");
        if (existingOverlay != null) return; // Prevent duplicate overlays

        GameState state = gameManagerController.GetGameState();
        if (state == null) return;
        
        Player[] players = state.GetAllPlayers();
        
        // Display game over screen
        Debug.Log("==== GAME OVER ====");
        for (int i = 0; i < players.Length; i++)
        {
            Debug.Log($"Player {i + 1}: {players[i].GetScore()} points");
        }
        
        // Find winner
        Player winner = players[0];
        for (int i = 1; i < players.Length; i++)
        {
            if (players[i].GetScore() > winner.GetScore())
                winner = players[i];
        }
        
        Debug.Log($"Winner: Player {winner.ID}!");
        
        ShowFloatingText("WINNER!", winner.ID - 1, Color.green);
        
        // Create End Game Overlay Panel dynamically
        GameObject overlayGO = new GameObject("EndGameOverlay");
        overlayGO.transform.SetParent(this.transform, false);
        
        Image bg = overlayGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        RectTransform bgRT = overlayGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        GameObject textGO = new GameObject("EndGameText");
        textGO.transform.SetParent(overlayGO.transform, false);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 42;
        text.color = Color.white;
        
        string resultsText = $"GAME OVER\n\n<color=#FFD700>WINNER: {winner.Name}</color>\nScore: {winner.GetScore()}\n\n";
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != winner)
                resultsText += $"{players[i].Name}: {players[i].GetScore()} pts\n";
        }
        text.text = resultsText;
        
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        CanvasGroup group = overlayGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        if (animationController != null)
            animationController.AnimateFadeIn(group, 1f);

        if (gameManagerController != null && SaveManager.Instance != null)
            SaveManager.Instance.RecordGame(players, gameManagerController.HumanPlayerId + 1);
            
        if (UIEffectsManager.Instance != null)
            UIEffectsManager.Instance.EmitConfetti(this.transform);

        var menuBtnGO = new GameObject("MenuBtn", typeof(RectTransform));
        menuBtnGO.transform.SetParent(overlayGO.transform, false);
        var menuImg = menuBtnGO.AddComponent<Image>();
        menuImg.color = new Color(0.6f, 0.2f, 0.2f);
        var menuBtn = menuBtnGO.AddComponent<Button>();
        var menuRT = menuBtnGO.GetComponent<RectTransform>();
        menuRT.anchorMin = new Vector2(0.5f, 0.2f); menuRT.anchorMax = new Vector2(0.5f, 0.2f);
        menuRT.sizeDelta = new Vector2(250, 50);
        menuRT.anchoredPosition = new Vector2(0, -60);
        var menuLblGO = new GameObject("Label", typeof(RectTransform));
        menuLblGO.transform.SetParent(menuBtnGO.transform, false);
        var menuLbl = menuLblGO.AddComponent<Text>();
        menuLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        menuLbl.text = "Main Menu";
        menuLbl.alignment = TextAnchor.MiddleCenter;
        menuLbl.color = Color.white;
        var menuLblRT = menuLbl.GetComponent<RectTransform>();
        menuLblRT.anchorMin = Vector2.zero; menuLblRT.anchorMax = Vector2.one;
        menuLblRT.sizeDelta = Vector2.zero;
        menuBtn.onClick.AddListener(() => { Destroy(overlayGO); UIManager.Instance?.ShowMenu(); });

        var restartBtnGO = new GameObject("RestartBtn", typeof(RectTransform));
        restartBtnGO.transform.SetParent(overlayGO.transform, false);
        var restartImg = restartBtnGO.AddComponent<Image>();
        restartImg.color = new Color(0.8f, 0.4f, 0.2f);
        var restartBtn = restartBtnGO.AddComponent<Button>();
        var restartRT = restartBtnGO.GetComponent<RectTransform>();
        restartRT.anchorMin = new Vector2(0.5f, 0.2f); restartRT.anchorMax = new Vector2(0.5f, 0.2f);
        restartRT.sizeDelta = new Vector2(250, 50);
        restartRT.anchoredPosition = new Vector2(0, 0);
        var restartLbl = Instantiate(menuLblGO, restartBtnGO.transform).GetComponent<Text>();
        restartLbl.text = "Restart Game";
        restartBtn.onClick.AddListener(() => { 
            Destroy(overlayGO); 
            var gmc = GameManagerController.Instance;
            if (gmc != null) {
                gmc.InitializeGame(gmc.CurrentDifficulties, gmc.HumanPlayerId);
                gmc.StartGame();
                GameLoopManager.Instance?.StartGameLoop();
            }
        });

        if (ReplayManager.Instance != null && ReplayManager.Instance.LastRecord != null) {
            var repBtnGO = new GameObject("ReplayBtn", typeof(RectTransform));
            repBtnGO.transform.SetParent(overlayGO.transform, false);
            var repImg = repBtnGO.AddComponent<Image>();
            repImg.color = new Color(0.2f, 0.4f, 0.8f);
            var repBtn = repBtnGO.AddComponent<Button>();
            var repRT = repBtnGO.GetComponent<RectTransform>();
            repRT.anchorMin = new Vector2(0.5f, 0.2f); repRT.anchorMax = new Vector2(0.5f, 0.2f);
            repRT.sizeDelta = new Vector2(250, 50);
            repRT.anchoredPosition = new Vector2(0, 60);
            var repLbl = Instantiate(menuLblGO, repBtnGO.transform).GetComponent<Text>();
            repLbl.text = "Watch Replay";
            
            repBtn.onClick.AddListener(() => { Destroy(overlayGO); GameManagerController.Instance?.StartReplay(); });
        }
    }
    
    public void UpdatePlayerScore(int playerId, int score)
    {
        int idx = playerId - 1;
        if (idx >= 0 && idx < playerScoreTexts.Length && playerScoreTexts[idx] != null)
        {
            playerScoreTexts[idx].text = $"{Player.GetDirectionName(playerId)}: {score}";
        }
    }
    
    /// <summary>
    /// Enable card selection for human player
    /// </summary>
    public void EnableCardSelection(Player player, Action<Card> onCardSelected, List<Card> validCards = null)
    {
        this.onCardSelected = onCardSelected;

        if (player != null)
        {
            int idx = player.ID - 1;
            if (idx >= 0 && idx < playerHandDisplays.Length && playerHandDisplays[idx] != null)
            {
                playerHandDisplays[idx].EnableCardSelection(onCardSelected);

                // Highlight this player
                if (idx < playerHighlights.Length && playerHighlights[idx] != null)
                {
                    playerHighlights[idx].enabled = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Enable reveal card selection
    /// </summary>
    public void EnableRevealSelection(Player revealingPlayer, Player targetPlayer, Action<Card> onCardSelected, List<Card> validCards = null)
    {
        this.onCardSelected = onCardSelected;

        if (targetPlayer != null)
        {
            int idx = targetPlayer.ID - 1;
            if (idx >= 0 && idx < playerHandDisplays.Length && playerHandDisplays[idx] != null)
            {
                playerHandDisplays[idx].EnableRevealSelection(onCardSelected);
            }
        }
    }
    
    /// <summary>
    /// Enable player selection for reveal target
    /// </summary>
    public void EnablePlayerSelection(Player revealingPlayer, Action<Player> onPlayerSelected, List<Player> validPlayers)
    {
        GameState state = gameManagerController.GetGameState();
        
        // Highlight all players except revealing player
        for (int i = 0; i < playerPanels.Length; i++)
        {
            if (revealingPlayer != null && i != revealingPlayer.ID - 1 && playerPanels[i] != null)
            {
                Player player = state.GetPlayer(i + 1);
                if (validPlayers.Contains(player))
                {
                    if (i < playerHighlights.Length && playerHighlights[i] != null)
                        playerHighlights[i].enabled = true;
                    
                    playerPanels[i].gameObject.SetActive(true);

                    // Add click handler
                    Button btn = playerPanels[i].gameObject.GetComponent<Button>();
                    if (btn == null) btn = playerPanels[i].gameObject.AddComponent<Button>();
                    
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => onPlayerSelected(player));
                }
                else
                {
                    playerPanels[i].gameObject.SetActive(false);
                }
            }
            else if (playerPanels[i] != null)
            {
                playerPanels[i].gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Disable card selection
    /// </summary>
    public void DisableCardSelection()
    {
        onCardSelected = null;
        
        foreach (HandDisplay display in playerHandDisplays)
        {
            if (display != null)
                display.DisableCardSelection();
        }
        
        foreach (Image highlight in playerHighlights)
        {
            if (highlight != null)
                highlight.enabled = false;
        }

        DisablePlayerSelection();
    }
    
    /// <summary>
    /// Disable player selection
    /// </summary>
    public void DisablePlayerSelection()
    {
        // Remove click handlers from player panels
        foreach (Transform panel in playerPanels)
        {
            if (panel != null)
            {
                Button btn = panel.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.RemoveAllListeners();
                    
                panel.gameObject.SetActive(false);
            }
        }
        
        foreach (Image highlight in playerHighlights)
        {
            if (highlight != null)
                highlight.enabled = false;
        }
    }
    
    public void ClearAllState()
    {
        foreach (var card in currentPlayZoneCards)
        {
            if (card != null && card.gameObject != null) Destroy(card.gameObject);
        }
        currentPlayZoneCards.Clear();
        
        foreach (var hand in playerHandDisplays)
        {
            if (hand != null) hand.RefreshDisplay(null);
        }
        
        // Safely purge all end game overlays
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "EndGameOverlay") Destroy(child.gameObject);
        }

        for (int i = 0; i < playerScoreTexts.Length; i++)
        {
            if (playerScoreTexts[i] != null) 
                playerScoreTexts[i].text = $"{Player.GetDirectionName(i+1)}: 0";
        }

        if (phaseDisplayText != null) phaseDisplayText.text = "";
        if (roundDisplayText != null) roundDisplayText.text = "";
    }

    /// <summary>
    /// Refresh all hand displays
    /// </summary>
    public void RefreshAllHands(Player[] players, bool isDeal = false)
    {
        for (int i = 0; i < players.Length && i < playerHandDisplays.Length; i++)
        {
            if (playerHandDisplays[i] != null && players[i] != null)
            {
                playerHandDisplays[i].RefreshDisplay(players[i].Hand, isDeal);
            }
        }
    }
    
    private System.Collections.IEnumerator ClearPlayZoneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Disable highlights
        foreach (Image highlight in playerHighlights)
        {
            if (highlight != null)
                highlight.enabled = false;
        }
        
        // Enforce immediate destruction of cards in the play zone
        foreach (CardDisplay card in currentPlayZoneCards)
        {
            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }
        }
        currentPlayZoneCards.Clear();
    }
    
    /// <summary>
    /// Show floating text effect for score updates
    /// </summary>
    private void ShowFloatingText(string text, int playerId, Color color)
    {
        if (playerId < 0 || playerId >= playerHandDisplays.Length)
            return;
        
        if (playerHandDisplays[playerId] == null)
            return;
        
        // Create floating text object
        GameObject floatingTextObj = new GameObject("FloatingText");
        floatingTextObj.transform.SetParent(playerHandDisplays[playerId].transform, false);
        floatingTextObj.transform.localPosition = new Vector3(0, 50, 0); // Spawns slightly above the hand
        
        FloatingTextEffect effect = floatingTextObj.AddComponent<FloatingTextEffect>();
        effect.PlayEffect(text, color);
    }
    
    /// <summary>
    /// Show invalid move message
    /// </summary>
    private void ShowInvalidMoveMessage(string reason)
    {
        Debug.LogWarning($"Invalid move: {reason}");
        
        // TODO: Show UI toast/notification with error message
        // For now, just log it
    }
}
