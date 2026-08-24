using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Reflection;

/// <summary>
/// Ensures minimal UI and managers exist at runtime even when the scene
/// doesn't contain the UI GameObjects. Runs automatically at scene load.
/// </summary>
public static class SceneBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeUI()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Mobile: Cap at 30 FPS to maximize battery life and prevent thermal throttling
        Application.targetFrameRate = 30;
        QualitySettings.vSyncCount = 0; 
#else
        // Desktop: Run at 60 FPS for smoother animations
        Application.targetFrameRate = 60;
#endif

        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Ensure EventSystem
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(esGO);
        }

        // Ensure Canvas
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        GameObject canvasGO;
        if (canvas == null)
        {
            canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Ensure canvas renders above world geometry and other canvases
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            canvasGO.AddComponent<SafeAreaFitter>();
            Object.DontDestroyOnLoad(canvasGO);
        }
        else
        {
            canvasGO = canvas.gameObject;
        }

        // Create or reuse a dedicated overlay canvas for game UI to avoid attaching to a world-space canvas
        GameObject overlayCanvasGO = GameObject.Find("GameUICanvas_Overlay");
        Canvas overlayCanvasComp = null;
        if (overlayCanvasGO == null)
        {
            overlayCanvasGO = new GameObject("GameUICanvas_Overlay");
            overlayCanvasComp = overlayCanvasGO.AddComponent<Canvas>();
            overlayCanvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvasComp.overrideSorting = true;
            overlayCanvasComp.sortingOrder = 32767;
            var scaler = overlayCanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            overlayCanvasGO.AddComponent<GraphicRaycaster>();
            overlayCanvasGO.AddComponent<SafeAreaFitter>();
            Object.DontDestroyOnLoad(overlayCanvasGO);
        }
        else
        {
            overlayCanvasComp = overlayCanvasGO.GetComponent<Canvas>();
            if (overlayCanvasComp == null)
                overlayCanvasComp = overlayCanvasGO.AddComponent<Canvas>();
            overlayCanvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvasComp.overrideSorting = true;
            overlayCanvasComp.sortingOrder = 32767;
            if (overlayCanvasGO.GetComponent<CanvasScaler>() == null)
                overlayCanvasGO.AddComponent<CanvasScaler>();
            if (overlayCanvasGO.GetComponent<GraphicRaycaster>() == null)
                overlayCanvasGO.AddComponent<GraphicRaycaster>();
            if (overlayCanvasGO.GetComponent<SafeAreaFitter>() == null)
                overlayCanvasGO.AddComponent<SafeAreaFitter>();
            Object.DontDestroyOnLoad(overlayCanvasGO);
        }

        // Ensure GameManagerController and helpers
        if (GameManagerController.Instance == null)
        {
            var gmGO = new GameObject("GameManagerController");
            gmGO.AddComponent<GameManagerController>();
            gmGO.AddComponent<GameLoopManager>();
            gmGO.AddComponent<AnimationController>();
            gmGO.AddComponent<InputManagerEnhanced>();
            gmGO.AddComponent<ValidationManager>();
            gmGO.AddComponent<SaveManager>();
            gmGO.AddComponent<ReplayManager>();
            gmGO.AddComponent<UIEffectsManager>();
            Object.DontDestroyOnLoad(gmGO);
        }

        // Ensure UIManager + GameBoardUI
        if (UIManager.Instance == null)
        {
            Debug.Log("SceneBootstrapper: Creating UIManager and GameBoardUI under overlay canvas");
            var uiManagerGO = new GameObject("UIManager", typeof(RectTransform));
            var uiManager = uiManagerGO.AddComponent<UIManager>();
            uiManagerGO.transform.SetParent(overlayCanvasGO.transform, false);

            // Create a very small GameBoardUIEnhanced with Phase/Round texts so the user sees UI
            var boardGO = new GameObject("GameBoardUI", typeof(RectTransform));
            boardGO.transform.SetParent(overlayCanvasGO.transform, false);
            var board = boardGO.AddComponent<GameBoardUIEnhanced>();
            // Make board fullscreen-stretch so child RectTransforms are positioned predictably
            var boardRT = boardGO.GetComponent<RectTransform>();
            if (boardRT != null)
            {
                boardRT.anchorMin = Vector2.zero;
                boardRT.anchorMax = Vector2.one;
                boardRT.offsetMin = Vector2.zero;
                boardRT.offsetMax = Vector2.zero;
            }

            // Phase text
            var phaseGO = new GameObject("PhaseText", typeof(RectTransform));
            phaseGO.transform.SetParent(boardGO.transform, false);
            var phaseText = phaseGO.AddComponent<Text>();
            phaseText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phaseText.fontSize = 22;
            phaseText.color = Color.white;
            phaseText.alignment = TextAnchor.UpperRight;
            var rt = phaseText.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);

            // Round text
            var roundGO = new GameObject("RoundText", typeof(RectTransform));
            roundGO.transform.SetParent(boardGO.transform, false);
            var roundText = roundGO.AddComponent<Text>();
            roundText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roundText.fontSize = 18;
            roundText.color = Color.white;
            roundText.alignment = TextAnchor.UpperRight;
            var rt2 = roundText.rectTransform;
            rt2.anchorMin = new Vector2(1f, 1f);
            rt2.anchorMax = new Vector2(1f, 1f);
            rt2.pivot = new Vector2(1f, 1f);
            rt2.anchoredPosition = new Vector2(-10f, -40f);

            // Assign private serialized fields via reflection
            var boardType = typeof(GameBoardUIEnhanced);
            // We'll apply runtime bindings later via the public setter to avoid fragile reflection.

            // Create basic HandDisplays (hidden/shown containers) and wire into GameBoardUI
            var handDisplays = new HandDisplay[4];
            var scoreTexts = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                var handGO = new GameObject($"HandDisplay_Player{i}", typeof(RectTransform));
                handGO.transform.SetParent(boardGO.transform, false);
                var handRect = handGO.GetComponent<RectTransform>();

                // Position each hand in a quadrant
                switch (i)
                {
                    case 0: // bottom
                        handRect.anchorMin = new Vector2(0.5f, 0f);
                        handRect.anchorMax = new Vector2(0.5f, 0f);
                        handRect.pivot = new Vector2(0.5f, 0f);
                        handRect.anchoredPosition = new Vector2(0f, 90f);
                        handRect.sizeDelta = new Vector2(700f, 280f);
                        break;
                    case 1: // left
                        handRect.anchorMin = new Vector2(0f, 0.5f);
                        handRect.anchorMax = new Vector2(0f, 0.5f);
                        handRect.pivot = new Vector2(0f, 0.5f);
                        handRect.anchoredPosition = new Vector2(120f, 0f);
                        handRect.sizeDelta = new Vector2(280f, 500f);
                        break;
                    case 2: // top
                        handRect.anchorMin = new Vector2(0.5f, 1f);
                        handRect.anchorMax = new Vector2(0.5f, 1f);
                        handRect.pivot = new Vector2(0.5f, 1f);
                        handRect.anchoredPosition = new Vector2(0f, -90f);
                        handRect.sizeDelta = new Vector2(700f, 280f);
                        break;
                    case 3: // right
                        handRect.anchorMin = new Vector2(1f, 0.5f);
                        handRect.anchorMax = new Vector2(1f, 0.5f);
                        handRect.pivot = new Vector2(1f, 0.5f);
                        handRect.anchoredPosition = new Vector2(-120f, 0f);
                        handRect.sizeDelta = new Vector2(280f, 500f);
                        break;
                }

                var shownGO = new GameObject("ShownHand");
                shownGO.transform.SetParent(handGO.transform, false);
                var shownRT = shownGO.AddComponent<RectTransform>();

                var hiddenGO = new GameObject("HiddenHand");
                hiddenGO.transform.SetParent(handGO.transform, false);
                var hiddenRT = hiddenGO.AddComponent<RectTransform>(); 

                // Set anchors based on player position for correct "Shown is closer to center" layout
                switch (i)
                {
                    case 0: // South
                        hiddenRT.anchorMin = new Vector2(0, 0); hiddenRT.anchorMax = new Vector2(1, 0.5f);
                        shownRT.anchorMin = new Vector2(0, 0.5f); shownRT.anchorMax = new Vector2(1, 1);
                        break;
                    case 1: // West
                        hiddenRT.anchorMin = new Vector2(0, 0); hiddenRT.anchorMax = new Vector2(0.5f, 1);
                        shownRT.anchorMin = new Vector2(0.5f, 0); shownRT.anchorMax = new Vector2(1, 1);
                        break;
                    case 2: // North
                        hiddenRT.anchorMin = new Vector2(0, 0.5f); hiddenRT.anchorMax = new Vector2(1, 1);
                        shownRT.anchorMin = new Vector2(0, 0); shownRT.anchorMax = new Vector2(1, 0.5f);
                        break;
                    case 3: // East
                        hiddenRT.anchorMin = new Vector2(0.5f, 0); hiddenRT.anchorMax = new Vector2(1, 1);
                        shownRT.anchorMin = new Vector2(0, 0); shownRT.anchorMax = new Vector2(0.5f, 1);
                        break;
                }
                hiddenRT.offsetMin = hiddenRT.offsetMax = shownRT.offsetMin = shownRT.offsetMax = Vector2.zero;

                var hd = handGO.AddComponent<HandDisplay>();
                var hdType = typeof(HandDisplay);
                var hiddenField2 = hdType.GetField("hiddenHandContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                var shownField2 = hdType.GetField("shownHandContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (hiddenField2 != null) hiddenField2.SetValue(hd, hiddenGO.transform);
                if (shownField2 != null) shownField2.SetValue(hd, shownGO.transform);

                // Create a score text near this hand
                var scoreContainerGO = new GameObject($"PlayerScoreContainer_{i}", typeof(RectTransform));
                scoreContainerGO.transform.SetParent(boardGO.transform, false);
                var scoreContainerRT = scoreContainerGO.GetComponent<RectTransform>();
                scoreContainerRT.sizeDelta = new Vector2(120, 30);
                var scoreBG = scoreContainerGO.AddComponent<Image>();
                scoreBG.color = new Color(0, 0, 0, 0.5f);

                var scoreGO = new GameObject($"PlayerScoreText_{i}");
                scoreGO.transform.SetParent(scoreContainerGO.transform, false);
                var scoreText = scoreGO.AddComponent<Text>();
                scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                scoreText.fontSize = 16;
                scoreText.color = Color.white;
                scoreText.alignment = TextAnchor.MiddleCenter;
                var scoreTextRT = scoreText.rectTransform;
                scoreTextRT.anchorMin = Vector2.zero;
                scoreTextRT.anchorMax = Vector2.one;
                scoreTextRT.offsetMin = Vector2.zero;
                scoreTextRT.offsetMax = Vector2.zero;

                switch (i)
                {
                    case 0: scoreContainerRT.anchorMin = new Vector2(0.5f, 0f); scoreContainerRT.anchorMax = new Vector2(0.5f, 0f); scoreContainerRT.anchoredPosition = new Vector2(0f, 25f); break;
                    case 1: scoreContainerRT.anchorMin = new Vector2(0f, 0.5f); scoreContainerRT.anchorMax = new Vector2(0f, 0.5f); scoreContainerRT.anchoredPosition = new Vector2(25f, 0f); scoreContainerRT.localEulerAngles = new Vector3(0,0,90); break;
                    case 2: scoreContainerRT.anchorMin = new Vector2(0.5f, 1f); scoreContainerRT.anchorMax = new Vector2(0.5f, 1f); scoreContainerRT.anchoredPosition = new Vector2(0f, -25f); break;
                    case 3: scoreContainerRT.anchorMin = new Vector2(1f, 0.5f); scoreContainerRT.anchorMax = new Vector2(1f, 0.5f); scoreContainerRT.anchoredPosition = new Vector2(-25f, 0f); scoreContainerRT.localEulerAngles = new Vector3(0,0,-90); break;
                }

                hd.Initialize(i);
                handDisplays[i] = hd;
                scoreTexts[i] = scoreText;
                Debug.Log($"SceneBootstrapper: Created HandDisplay_Player{i} and score text");
            }

            // We'll set player hand and score references when calling ApplyRuntimeBindings below.

            // Create a PlayZone container centered on screen
            var playZoneGO = new GameObject("PlayZoneContainer", typeof(RectTransform));
            playZoneGO.transform.SetParent(boardGO.transform, false);
            var playZoneRT = playZoneGO.GetComponent<RectTransform>();
            playZoneRT.anchorMin = new Vector2(0.5f, 0.5f);
            playZoneRT.anchorMax = new Vector2(0.5f, 0.5f);
            playZoneRT.pivot = new Vector2(0.5f, 0.5f);
            playZoneRT.anchoredPosition = Vector2.zero;
            playZoneRT.sizeDelta = new Vector2(800f, 400f);
            // playZoneContainer will be applied via ApplyRuntimeBindings below

            // Create 4 player play slots within play zone (named PlayerSlot_1..4)
            for (int i = 0; i < 4; i++)
            {
                int slotId = i + 1; // make slot names 1-based to match Player.ID
                var slot = new GameObject($"PlayerSlot_{slotId}", typeof(RectTransform));
                slot.transform.SetParent(playZoneGO.transform, false);
                var slotRT = slot.GetComponent<RectTransform>();
                slotRT.sizeDelta = new Vector2(160f, 120f);
                switch (i)
                {
                    case 0: slotRT.anchoredPosition = new Vector2(0f, -120f); break; // bottom (Player 1)
                    case 1: slotRT.anchoredPosition = new Vector2(-260f, 0f); break; // left (Player 2)
                    case 2: slotRT.anchoredPosition = new Vector2(0f, 120f); break; // top (Player 3)
                    case 3: slotRT.anchoredPosition = new Vector2(260f, 0f); break; // right (Player 4)
                }
                // Show a faint background and visible outline to help debugging layout at runtime
                var img = slot.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.03f);
                img.raycastTarget = false;
                // Visible outline to spot play slot areas
                var outline = slot.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 1f, 1f, 0.35f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // Create player selection buttons (hidden by default)
            var selButtons = new UnityEngine.UI.Button[4];
            for (int i = 0; i < 4; i++)
            {
                var btnGO = new GameObject($"PlayerSelectBtn_{i}", typeof(RectTransform));
                btnGO.transform.SetParent(boardGO.transform, false);
                var img = btnGO.AddComponent<Image>();
                img.color = new Color(0.1f, 0.6f, 0.1f, 0.9f);
                var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

                var btnRT = btnGO.GetComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(160f, 40f);
                switch (i)
                {
                    case 0: btnRT.anchoredPosition = new Vector2(0f, -220f); break; // bottom (Player 1)
                    case 1: btnRT.anchoredPosition = new Vector2(-420f, 0f); break; // left (Player 2)
                    case 2: btnRT.anchoredPosition = new Vector2(0f, 220f); break; // top (Player 3)
                    case 3: btnRT.anchoredPosition = new Vector2(420f, 0f); break; // right (Player 4)
                }

                // Add a label
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(btnGO.transform, false);
                var lbl = lblGO.AddComponent<Text>();
                lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.text = $"Reveal {Player.GetDirectionName(i + 1)}";
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = Color.white;
                var lblRT = lbl.rectTransform;
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = Vector2.zero;
                lblRT.offsetMax = Vector2.zero;
                btnGO.SetActive(false);
                selButtons[i] = btn;
            }
            // playerSelectionButtons will be applied via ApplyRuntimeBindings below

            // Create round winner indicator
            var rwGO = new GameObject("RoundWinnerIndicator", typeof(RectTransform));
            rwGO.transform.SetParent(boardGO.transform, false);
            var rwImg = rwGO.AddComponent<Image>();
            rwImg.enabled = false;
            // roundWinnerIndicator will be applied via ApplyRuntimeBindings below
            // Create Menu UI and assign to UIManager
            var menuGO = new GameObject("MenuUI", typeof(RectTransform));
            menuGO.transform.SetParent(overlayCanvasGO.transform, false);
            var menu = menuGO.AddComponent<MenuUI>();

            // Title
            var titleGO2 = new GameObject("TitleText");
            titleGO2.transform.SetParent(menuGO.transform, false);
            var titleText = titleGO2.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 36;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "Card Game";
            var titleRT = titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0.5f, 0.8f);
            titleRT.anchorMax = new Vector2(0.5f, 0.8f);
            titleRT.pivot = new Vector2(0.5f, 0.5f);
            titleRT.anchoredPosition = Vector2.zero;

            // Helper to create button
            System.Func<string, Vector2, Button> makeBtn = (label, pos) =>
            {
                var btnGO = new GameObject(label + "Btn");
                btnGO.transform.SetParent(menuGO.transform, false);
                var img = btnGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                var btn = btnGO.AddComponent<Button>();
                var rtBtn = img.rectTransform;
                rtBtn.anchorMin = new Vector2(0.5f, 0.5f);
                rtBtn.anchorMax = new Vector2(0.5f, 0.5f);
                rtBtn.pivot = new Vector2(0.5f, 0.5f);
                rtBtn.anchoredPosition = pos;
                rtBtn.sizeDelta = new Vector2(220f, 40f);
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(btnGO.transform, false);
                var lbl = lblGO.AddComponent<Text>();
                lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = Color.white;
                lbl.text = label;
                var lblRT = lbl.rectTransform;
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = Vector2.zero;
                lblRT.offsetMax = Vector2.zero;
                return btn;
            };

            Difficulty[] currentDiffs = new Difficulty[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Easy };
            bool[] isHuman = new bool[] { true, false, false, false };

            var p1Btn = makeBtn("South: Human", new Vector2(-150f, 60f));
            var p2Btn = makeBtn("West: Medium AI", new Vector2(150f, 60f));
            var p3Btn = makeBtn("North: Hard AI", new Vector2(-150f, 0f));
            var p4Btn = makeBtn("East: Easy AI", new Vector2(150f, 0f));

            var startBtn = makeBtn("Start Game", new Vector2(0f, -60f));
            var statsBtn = makeBtn("Player Stats", new Vector2(0f, -120f));
            var quitBtn = makeBtn("Quit", new Vector2(0f, -180f));

            // Assign MenuUI fields via reflection
            var menuType = typeof(MenuUI);
            var quitField = menuType.GetField("quitButton", BindingFlags.NonPublic | BindingFlags.Instance);
            var titleField = menuType.GetField("titleText", BindingFlags.NonPublic | BindingFlags.Instance);
            if (quitField != null) quitField.SetValue(menu, quitBtn);
            if (titleField != null) titleField.SetValue(menu, titleText);

            System.Action<int, Button> togglePlayer = (pIndex, btn) =>
            {
                if (isHuman[pIndex]) {
                    isHuman[pIndex] = false;
                    currentDiffs[pIndex] = Difficulty.Easy;
                } else if (currentDiffs[pIndex] == Difficulty.Easy) {
                    currentDiffs[pIndex] = Difficulty.Medium;
                } else if (currentDiffs[pIndex] == Difficulty.Medium) {
                    currentDiffs[pIndex] = Difficulty.Hard;
                } else {
                    isHuman[pIndex] = true;
                }
                
                var txt = btn.GetComponentInChildren<Text>();
                if (isHuman[pIndex]) txt.text = $"{Player.GetDirectionName(pIndex + 1)}: Human";
                else txt.text = $"{Player.GetDirectionName(pIndex + 1)}: {currentDiffs[pIndex]} AI";
            };

            p1Btn.onClick.AddListener(() => togglePlayer(0, p1Btn));
            p2Btn.onClick.AddListener(() => togglePlayer(1, p2Btn));
            p3Btn.onClick.AddListener(() => togglePlayer(2, p3Btn));
            p4Btn.onClick.AddListener(() => togglePlayer(3, p4Btn));

            startBtn.onClick.AddListener(() =>
            {
                var gm = GameManagerController.Instance;
                if (gm != null)
                {
                    try 
                    {
                        int mainHumanSeat = -1;
                        for(int i = 0; i < 4; i++) {
                            if (isHuman[i]) { mainHumanSeat = i; break; }
                        }

                        gm.InitializeGame(currentDiffs, mainHumanSeat);
                        gm.StartGame();
                        
                        var loop = GameLoopManager.Instance;
                        if (loop != null)
                        {
                            loop.SetPhaseDuration(1.0f);
                            loop.StartGameLoop();
                        }
                        
                        if (UIManager.Instance != null) UIManager.Instance.HideMenu();
                    }
                    catch (System.Exception ex) { Debug.LogError($"Error starting Game: {ex.Message}\n{ex.StackTrace}"); }
                }
            });

            var statsPanel = new GameObject("StatsPanel", typeof(RectTransform));
            statsPanel.transform.SetParent(menuGO.transform, false);
            var statsImg = statsPanel.AddComponent<Image>();
            statsImg.color = new Color(0f,0f,0f,0.95f);
            var statsRT = statsPanel.GetComponent<RectTransform>();
            statsRT.anchorMin = Vector2.zero; statsRT.anchorMax = Vector2.one;
            statsRT.sizeDelta = Vector2.zero;
            statsPanel.SetActive(false);
            var statsTxtGO = new GameObject("StatsText", typeof(RectTransform));
            statsTxtGO.transform.SetParent(statsPanel.transform, false);
            var statsTxt = statsTxtGO.AddComponent<Text>();
            statsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statsTxt.text = "";
            statsTxt.fontSize = 24;
            statsTxt.color = Color.white;
            statsTxt.alignment = TextAnchor.MiddleCenter;
            var statsTxtRT = statsTxt.GetComponent<RectTransform>();
            statsTxtRT.anchorMin = Vector2.zero;
            statsTxtRT.anchorMax = Vector2.one;
            statsTxtRT.offsetMin = Vector2.zero;
            statsTxtRT.offsetMax = Vector2.zero;

            var closeStatsBtn = makeBtn("Close", new Vector2(0f, -180f));
            closeStatsBtn.transform.SetParent(statsPanel.transform, false);
            closeStatsBtn.onClick.AddListener(() => statsPanel.SetActive(false));
            statsBtn.onClick.AddListener(() => {
                if(SaveManager.Instance != null) {
                    statsTxt.text = $"PLAYER STATS\n\nGames Played: {SaveManager.Instance.Stats.GamesPlayed}\nHuman Wins: {SaveManager.Instance.Stats.HumanWins}\nHighest Score: {SaveManager.Instance.Stats.HighestScore}";
                    statsPanel.SetActive(true);
                }
            });

            // Show menu initially and hide board
            menuGO.SetActive(true);
            boardGO.SetActive(false);

            // Use public setter on UIManager to avoid reflection
            try
            {
                uiManager.SetBoardAndMenu(board, menu);
            }
            catch { }

            // Apply runtime bindings to the board so it has all created references
            try
            {
                board.ApplyRuntimeBindings(handDisplays, playZoneGO.transform, phaseText, roundText, scoreTexts, selButtons, rwImg);
                Debug.Log("SceneBootstrapper: Applied runtime bindings to GameBoardUI");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SceneBootstrapper: Failed to apply runtime bindings: {ex}");
            }

            // Initialize menu now
            try
            {
                menu.Initialize();
            }
            catch { }

            // Persistent overlay: always-visible small toolbar for safety
            var overlayGO = new GameObject("UIOverlay");
            overlayGO.transform.SetParent(overlayCanvasGO.transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();

            // Background panel
            var bg = overlayGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.2f);
            overlayRT.anchorMin = new Vector2(0f, 1f);
            overlayRT.anchorMax = new Vector2(1f, 1f);
            overlayRT.pivot = new Vector2(0.5f, 1f);
            overlayRT.sizeDelta = new Vector2(0f, 50f);
            overlayRT.anchoredPosition = new Vector2(0f, 0f);

            // Menu button
            var menuBtnGO = new GameObject("Overlay_MenuBtn");
            menuBtnGO.transform.SetParent(overlayGO.transform, false);
            var menuImg = menuBtnGO.AddComponent<Image>();
            menuImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var menuBtn = menuBtnGO.AddComponent<UnityEngine.UI.Button>();
            var menuRT = menuImg.rectTransform;
            menuRT.anchorMin = new Vector2(0f, 1f);
            menuRT.anchorMax = new Vector2(0f, 1f);
            menuRT.pivot = new Vector2(0f, 1f);
            menuRT.sizeDelta = new Vector2(120f, 36f);
            menuRT.anchoredPosition = new Vector2(10f, -7f);
            var menuLblGO = new GameObject("Label");
            menuLblGO.transform.SetParent(menuBtnGO.transform, false);
            var menuLbl = menuLblGO.AddComponent<Text>();
            menuLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            menuLbl.text = "Menu";
            menuLbl.alignment = TextAnchor.MiddleCenter;
            menuLbl.color = Color.white;
            var menuLblRT = menuLbl.rectTransform;
            menuLblRT.anchorMin = Vector2.zero;
            menuLblRT.anchorMax = Vector2.one;
            menuLblRT.offsetMin = Vector2.zero;
            menuLblRT.offsetMax = Vector2.zero;
            menuBtn.onClick.AddListener(() => { if (UIManager.Instance != null) UIManager.Instance.ShowMenu(); });

            // Pause/Resume button
            var pauseBtnGO = new GameObject("Overlay_PauseBtn");
            pauseBtnGO.transform.SetParent(overlayGO.transform, false);
            var pauseImg = pauseBtnGO.AddComponent<Image>();
            pauseImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var pauseBtn = pauseBtnGO.AddComponent<UnityEngine.UI.Button>();
            var pauseRT = pauseImg.rectTransform;
            pauseRT.anchorMin = new Vector2(0f, 1f);
            pauseRT.anchorMax = new Vector2(0f, 1f);
            pauseRT.pivot = new Vector2(0f, 1f);
            pauseRT.sizeDelta = new Vector2(120f, 36f);
            pauseRT.anchoredPosition = new Vector2(140f, -7f);
            var pauseLblGO = new GameObject("Label");
            pauseLblGO.transform.SetParent(pauseBtnGO.transform, false);
            var pauseLbl = pauseLblGO.AddComponent<Text>();
            pauseLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pauseLbl.text = "Pause";
            pauseLbl.alignment = TextAnchor.MiddleCenter;
            pauseLbl.color = Color.white;
            var pauseLblRT = pauseLbl.rectTransform;
            pauseLblRT.anchorMin = Vector2.zero;
            pauseLblRT.anchorMax = Vector2.one;
            pauseLblRT.offsetMin = Vector2.zero;
            pauseLblRT.offsetMax = Vector2.zero;
            pauseBtn.onClick.AddListener(() =>
            {
                var loop = GameLoopManager.Instance;
                if (loop != null)
                {
                    if (loop.IsGamePaused)
                    {
                        loop.ResumeGameLoop();
                        pauseLbl.text = "Pause";
                    }
                    else
                    {
                        loop.PauseGameLoop();
                        pauseLbl.text = "Resume";
                    }
                }
            });

            // Advance Phase button (for testing)
            var advBtnGO = new GameObject("Overlay_AdvanceBtn");
            advBtnGO.transform.SetParent(overlayGO.transform, false);
            var advImg = advBtnGO.AddComponent<Image>();
            advImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var advBtn = advBtnGO.AddComponent<UnityEngine.UI.Button>();
            var advRT = advImg.rectTransform;
            advRT.anchorMin = new Vector2(0f, 1f);
            advRT.anchorMax = new Vector2(0f, 1f);
            advRT.pivot = new Vector2(0f, 1f);
            advRT.sizeDelta = new Vector2(160f, 36f);
            advRT.anchoredPosition = new Vector2(270f, -7f);
            var advLblGO = new GameObject("Label");
            advLblGO.transform.SetParent(advBtnGO.transform, false);
            var advLbl = advLblGO.AddComponent<Text>();
            advLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            advLbl.text = "Advance Phase";
            advLbl.alignment = TextAnchor.MiddleCenter;
            advLbl.color = Color.white;
            var advLblRT = advLbl.rectTransform;
            advLblRT.anchorMin = Vector2.zero;
            advLblRT.anchorMax = Vector2.one;
            advLblRT.offsetMin = Vector2.zero;
            advLblRT.offsetMax = Vector2.zero;
            advBtn.onClick.AddListener(() => { var loop = GameLoopManager.Instance; if (loop != null) loop.AdvancePhaseManually(); });

            // Reset Game button
            var resetBtnGO = new GameObject("Overlay_ResetBtn");
            resetBtnGO.transform.SetParent(overlayGO.transform, false);
            var resetImg = resetBtnGO.AddComponent<Image>();
            resetImg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
            var resetBtn = resetBtnGO.AddComponent<UnityEngine.UI.Button>();
            var resetRT = resetImg.rectTransform;
            resetRT.anchorMin = new Vector2(0f, 1f);
            resetRT.anchorMax = new Vector2(0f, 1f);
            resetRT.pivot = new Vector2(0f, 1f);
            resetRT.sizeDelta = new Vector2(120f, 36f);
            resetRT.anchoredPosition = new Vector2(440f, -7f);
            var resetLblGO = new GameObject("Label");
            resetLblGO.transform.SetParent(resetBtnGO.transform, false);
            var resetLbl = resetLblGO.AddComponent<Text>();
            resetLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            resetLbl.text = "Reset Game";
            resetLbl.alignment = TextAnchor.MiddleCenter;
            resetLbl.color = Color.white;
            var resetLblRT = resetLbl.rectTransform;
            resetLblRT.anchorMin = Vector2.zero;
            resetLblRT.anchorMax = Vector2.one;
            resetLblRT.offsetMin = Vector2.zero;
            resetLblRT.offsetMax = Vector2.zero;
            resetBtn.onClick.AddListener(() =>
            {
                var loop = GameLoopManager.Instance;
                if (loop != null) loop.StopGameLoop();
                var ui = UIManager.Instance;
                if (ui != null)
                {
                    ui.CancelSelection();
                    ui.ShowMenu();
                }
                var board = Object.FindAnyObjectByType<GameBoardUIEnhanced>();
                if (board != null) board.ClearAllState();
            });
            
            // Reveal All (Debug) Toggle
            var revBtnGO = new GameObject("Overlay_RevealBtn");
            revBtnGO.transform.SetParent(overlayGO.transform, false);
            var revImg = revBtnGO.AddComponent<Image>();
            revImg.color = new Color(0.3f, 0.1f, 0.5f, 0.9f);
            var revBtn = revBtnGO.AddComponent<UnityEngine.UI.Button>();
            var revRT = revImg.rectTransform;
            revRT.anchorMin = new Vector2(1f, 1f);
            revRT.anchorMax = new Vector2(1f, 1f);
            revRT.pivot = new Vector2(1f, 1f);
            revRT.sizeDelta = new Vector2(120f, 36f);
            revRT.anchoredPosition = new Vector2(-10f, -7f);
            var revLblGO = new GameObject("Label");
            revLblGO.transform.SetParent(revBtnGO.transform, false);
            var revLbl = revLblGO.AddComponent<Text>();
            revLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            revLbl.text = "Reveal: OFF";
            revLbl.alignment = TextAnchor.MiddleCenter;
            revLbl.color = Color.white;
            var revLblRT = revLbl.rectTransform;
            revLblRT.anchorMin = Vector2.zero; revLblRT.anchorMax = Vector2.one;
            revLblRT.offsetMin = Vector2.zero; revLblRT.offsetMax = Vector2.zero;
            revBtn.onClick.AddListener(() => {
                if (UIManager.Instance != null) {
                    UIManager.Instance.DebugRevealAll = !UIManager.Instance.DebugRevealAll;
                    revLbl.text = UIManager.Instance.DebugRevealAll ? "Reveal: ON" : "Reveal: OFF";
                    var board = Object.FindAnyObjectByType<GameBoardUIEnhanced>();
                    var state = GameManagerController.Instance?.GetGameState();
                    if (board != null && state != null) board.RefreshAllHands(state.GetAllPlayers());
                }
            });

            // System Clock display
            var clockGO = new GameObject("Overlay_Clock");
            clockGO.transform.SetParent(overlayGO.transform, false);
            var clockTxt = clockGO.AddComponent<Text>();
            clockTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clockTxt.fontSize = 16;
            clockTxt.color = Color.white;
            clockTxt.alignment = TextAnchor.MiddleRight;
            var clockRT = clockTxt.rectTransform;
            clockRT.anchorMin = new Vector2(1f, 0.5f);
            clockRT.anchorMax = new Vector2(1f, 0.5f);
            clockRT.pivot = new Vector2(1f, 0.5f);
            clockRT.sizeDelta = new Vector2(100f, 36f);
            clockRT.anchoredPosition = new Vector2(-380f, 0f); // Positioned left of debug buttons
            clockGO.AddComponent<SystemTimeDisplay>();

        // Rules Button
        var rulesBtnGO = new GameObject("Overlay_RulesBtn");
        rulesBtnGO.transform.SetParent(overlayGO.transform, false);
        var rulesImg = rulesBtnGO.AddComponent<Image>();
        rulesImg.color = new Color(0.8f, 0.6f, 0.1f, 0.9f);
        var rulesBtn = rulesBtnGO.AddComponent<UnityEngine.UI.Button>();
        var rulesRT = rulesImg.rectTransform;
        rulesRT.anchorMin = new Vector2(1f, 1f);
        rulesRT.anchorMax = new Vector2(1f, 1f);
        rulesRT.pivot = new Vector2(1f, 1f);
        rulesRT.sizeDelta = new Vector2(120f, 36f);
        rulesRT.anchoredPosition = new Vector2(-250f, -7f);
        var rulesLblGO = new GameObject("Label");
        rulesLblGO.transform.SetParent(rulesBtnGO.transform, false);
        var rulesLbl = rulesLblGO.AddComponent<Text>();
        rulesLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rulesLbl.text = "Rules";
        rulesLbl.alignment = TextAnchor.MiddleCenter;
        rulesLbl.color = Color.black;
        var rulesLblRT = rulesLbl.rectTransform;
        rulesLblRT.anchorMin = Vector2.zero; rulesLblRT.anchorMax = Vector2.one;
        rulesLblRT.offsetMin = Vector2.zero; rulesLblRT.offsetMax = Vector2.zero;

        var rulesPanel = new GameObject("RulesPanel", typeof(RectTransform));
        rulesPanel.transform.SetParent(overlayCanvasGO.transform, false);
        var rulesPanelImg = rulesPanel.AddComponent<Image>();
        rulesPanelImg.color = new Color(0f,0f,0f,0.95f);
        var rulesPanelRT = rulesPanel.GetComponent<RectTransform>();
        rulesPanelRT.anchorMin = Vector2.zero; rulesPanelRT.anchorMax = Vector2.one;
        rulesPanelRT.sizeDelta = Vector2.zero;
        rulesPanel.SetActive(false);

        var rulesTxtGO = new GameObject("RulesText", typeof(RectTransform));
        rulesTxtGO.transform.SetParent(rulesPanel.transform, false);
        var rulesTxt = rulesTxtGO.AddComponent<Text>();
        rulesTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rulesTxt.text = "<b>OVERVIEW:</b> 4 players, 13 rounds. Win tricks to collect the highest Score cards.\n\n" +
                        "<b>1. REVEAL:</b> Leader chooses a player to reveal one random Hidden card to their Shown hand.\n" +
                        "<b>2. PLAY:</b> Each player plays one card (Hidden or Shown) in turn order.\n" +
                        "<b>3. DETERMINATION:</b> ties resolve twice - on Value, then on Score.\n" +
                        "  - <b>Value ties</b> (before the winner): discard every card sharing its Value with another, whatever the tie size.\n" +
                        "  - <b>Winner:</b> highest remaining <b>Value</b> wins. Winner's card is discarded.\n" +
                        "  - <b>Score ties</b> (after the winner, before the prize): prize candidates sharing a <b>Score</b> are discarded; the next highest is taken.\n" +
                        "  - <b>Prize:</b> remaining candidate with the highest <b>Score</b> goes to the winner's scoring zone.\n" +
                        "<b>4. NEXT ROUND:</b> New leader is one seat clockwise from the previous leader, every round, whatever the outcome.\n\n" +
                        "<b>VALUE (Trick Power) vs. SCORE (Prize Points):</b>\n" +
                        "  - <b>VALUE:</b> (From Hidden) Face Value | (From Shown) 2-10: +2, J:13, Q:14, K:5, A:10\n" +
                        "  - <b>SCORE:</b> Always base Face Value (J:11, Q:12, K:13, A:14)";
        rulesTxt.fontSize = 18;
        rulesTxt.color = Color.white;
        rulesTxt.alignment = TextAnchor.MiddleCenter;
        var rulesTxtRT = rulesTxt.GetComponent<RectTransform>();
        rulesTxtRT.anchorMin = new Vector2(0.1f, 0.1f); rulesTxtRT.anchorMax = new Vector2(0.9f, 0.9f);
        rulesTxtRT.offsetMin = Vector2.zero; rulesTxtRT.offsetMax = Vector2.zero;

        var closeRulesBtn = makeBtn("Close Rules", new Vector2(0f, -300f));
        closeRulesBtn.transform.SetParent(rulesPanel.transform, false);
        closeRulesBtn.onClick.AddListener(() => rulesPanel.SetActive(false));
        rulesBtn.onClick.AddListener(() => rulesPanel.SetActive(true));

            // Speed Toggle
            var spdBtnGO = new GameObject("Overlay_SpeedBtn");
            spdBtnGO.transform.SetParent(overlayGO.transform, false);
            var spdImg = spdBtnGO.AddComponent<Image>();
            spdImg.color = new Color(0.2f, 0.4f, 0.6f, 0.9f);
            var spdBtn = spdBtnGO.AddComponent<UnityEngine.UI.Button>();
            var spdRT = spdImg.rectTransform;
            spdRT.anchorMin = new Vector2(1f, 1f); spdRT.anchorMax = new Vector2(1f, 1f);
            spdRT.pivot = new Vector2(1f, 1f);
            spdRT.sizeDelta = new Vector2(100f, 36f);
            spdRT.anchoredPosition = new Vector2(-140f, -7f);
            var spdLblGO = new GameObject("Label");
            spdLblGO.transform.SetParent(spdBtnGO.transform, false);
            var spdLbl = spdLblGO.AddComponent<Text>();
            spdLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spdLbl.text = "Speed: 1x";
            spdLbl.alignment = TextAnchor.MiddleCenter;
            spdLbl.color = Color.white;
            var spdLblRT = spdLbl.rectTransform;
            spdLblRT.anchorMin = Vector2.zero; spdLblRT.anchorMax = Vector2.one;
            spdLblRT.offsetMin = Vector2.zero; spdLblRT.offsetMax = Vector2.zero;
            spdBtn.onClick.AddListener(() => {
                if (Time.timeScale == 1f) { Time.timeScale = 2.5f; spdLbl.text = "Speed: 2x"; }
                else if (Time.timeScale == 2.5f) { Time.timeScale = 10f; spdLbl.text = "Speed: Max"; }
                else { Time.timeScale = 1f; spdLbl.text = "Speed: 1x"; }
            });

            // overlayGO is parented to overlay canvas which is already preserved.

            // Initialize board if GameManagerController exists
            var gm = GameManagerController.Instance;
            try
            {
                board.Initialize(gm);
            }
            catch { }
        }
    }
}
