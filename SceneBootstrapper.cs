using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Reflection;

/// <summary>
/// Automatically generates the entire GameBoard UI hierarchy at runtime.
/// Bypasses the need to manually drag-and-drop GameObjects in the Unity Scene editor.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    // This runs automatically when you press Play in Unity
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoBootstrap()
    {
        // Only run if we are in an empty scene / no Canvas exists yet
        if (FindObjectOfType<Canvas>() == null)
        {
            var bootstrapper = new GameObject("SceneBootstrapper").AddComponent<SceneBootstrapper>();
            bootstrapper.BuildGameScene();
        }
    }

    public void BuildGameScene()
    {
        Debug.Log("Bootstrapping runtime UI...");

        // 1. Input & Event System (Required for clicking/dragging)
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<StandaloneInputModule>();

        // 2. Main Canvas setup
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // 3. Global Managers
        var managersGO = new GameObject("Managers");
        managersGO.AddComponent<GameManagerController>();
        managersGO.AddComponent<GameLoopManager>();
        var uiManager = managersGO.AddComponent<UIManager>();
        managersGO.AddComponent<AnimationController>();
        managersGO.AddComponent<PrefabFactory>();

        // 4. Game Board Controller
        var boardGO = new GameObject("GameBoardUI");
        boardGO.transform.SetParent(canvasGO.transform, false);
        var rectBoard = boardGO.AddComponent<RectTransform>();
        rectBoard.anchorMin = Vector2.zero;
        rectBoard.anchorMax = Vector2.one;
        rectBoard.sizeDelta = Vector2.zero;
        var gameBoardUI = boardGO.AddComponent<GameBoardUI>();

        // UI Styling Defaults
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 5. Header (Phase & Round Texts)
        var phaseText = CreateText(boardGO.transform, "PhaseText", "Phase: Setup", defaultFont, new Vector2(0.02f, 0.95f), new Vector2(0.3f, 0.98f));
        var roundText = CreateText(boardGO.transform, "RoundText", "Round: 1 / 13", defaultFont, new Vector2(0.85f, 0.95f), new Vector2(0.98f, 0.98f));
        roundText.alignment = TextAnchor.UpperRight;

        // 6. Play Zone (Center trick area)
        var playZone = new GameObject("PlayZoneContainer", typeof(RectTransform));
        playZone.transform.SetParent(boardGO.transform, false);
        var playZoneRT = playZone.GetComponent<RectTransform>();
        playZoneRT.anchorMin = new Vector2(0.3f, 0.3f);
        playZoneRT.anchorMax = new Vector2(0.7f, 0.7f);
        playZoneRT.offsetMin = Vector2.zero;
        playZoneRT.offsetMax = Vector2.zero;

        CreateSlot(playZone.transform, "PlayerSlot_1", new Vector2(0.5f, 0.1f));
        CreateSlot(playZone.transform, "PlayerSlot_2", new Vector2(0.1f, 0.5f));
        CreateSlot(playZone.transform, "PlayerSlot_3", new Vector2(0.5f, 0.9f));
        CreateSlot(playZone.transform, "PlayerSlot_4", new Vector2(0.9f, 0.5f));

        // 7. Player Hands & UI (4 Players)
        HandDisplay[] hands = new HandDisplay[4];
        Text[] scores = new Text[4];
        Button[] buttons = new Button[4];

        // Layout Anchors: Bottom (P1), Left (P2), Top (P3), Right (P4)
        Vector2[] anchors = {
            new Vector2(0.5f, 0.1f),
            new Vector2(0.1f, 0.5f),
            new Vector2(0.5f, 0.9f),
            new Vector2(0.9f, 0.5f)
        };

        for (int i = 0; i < 4; i++)
        {
            int playerId = i + 1;
            var panel = new GameObject($"PlayerPanel_{playerId}", typeof(RectTransform));
            panel.transform.SetParent(boardGO.transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchors[i];
            rt.anchorMax = anchors[i];
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = (i % 2 == 0) ? new Vector2(600, 180) : new Vector2(180, 600);

            // Score Text
            scores[i] = CreateText(panel.transform, "ScoreText", $"P{playerId} Score: 0", defaultFont, new Vector2(0, 0), new Vector2(1, 0.2f));
            scores[i].alignment = TextAnchor.MiddleCenter;

            // Selection Button
            var btnGO = new GameObject("SelectButton", typeof(RectTransform));
            btnGO.transform.SetParent(panel.transform, false);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
            buttons[i] = btnGO.AddComponent<Button>();
            buttons[i].targetGraphic = btnImg;
            btnGO.SetActive(false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.2f, 0.2f);
            btnRT.anchorMax = new Vector2(0.8f, 0.8f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;

            var btnText = CreateText(btnGO.transform, "Text", "Select", defaultFont, Vector2.zero, Vector2.one);
            btnText.alignment = TextAnchor.MiddleCenter;

            // Hand Display Containers
            hands[i] = panel.AddComponent<HandDisplay>();
            var hidden = new GameObject("HiddenHand", typeof(RectTransform));
            hidden.transform.SetParent(panel.transform, false);
            var shown = new GameObject("ShownHand", typeof(RectTransform));
            shown.transform.SetParent(panel.transform, false);

            // Use Reflection to assign private fields inside HandDisplay safely
            var hdType = typeof(HandDisplay);
            hdType.GetField("hiddenHandContainer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(hands[i], hidden.transform);
            hdType.GetField("shownHandContainer", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(hands[i], shown.transform);
        }

        // 8. Round Winner Indicator
        var winnerGO = new GameObject("RoundWinnerIndicator", typeof(RectTransform));
        winnerGO.transform.SetParent(boardGO.transform, false);
        var winnerImg = winnerGO.AddComponent<Image>();
        winnerImg.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        winnerImg.enabled = false;
        var winnerRT = winnerGO.GetComponent<RectTransform>();
        winnerRT.sizeDelta = new Vector2(150, 150);

        // 9. Bind everything to UI Controllers
        gameBoardUI.ApplyRuntimeBindings(hands, playZone.transform, phaseText, roundText, scores, buttons, winnerImg);
        uiManager.SetBoardAndMenu(gameBoardUI, null);
        
        Debug.Log("Runtime UI Bootstrap Complete! UI Hierarchy successfully generated.");
    }

    private Text CreateText(Transform parent, string name, string content, Font font, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = 24;
        text.color = Color.white;
        return text;
    }

    private void CreateSlot(Transform parent, string name, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100, 140);
    }
}