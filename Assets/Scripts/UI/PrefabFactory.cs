using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

/// <summary>
/// Creates runtime placeholder prefabs for UI components when real prefabs are missing.
/// This ensures consistent visuals even if `Resources/Prefabs` are not present.
/// </summary>
public static class PrefabFactory
{
    private static GameObject cardPrototypeGO;
    private static GameObject handPrototypeGO;

    public static CardDisplay CreateCardDisplayInstance()
    {
        if (cardPrototypeGO == null)
        {
            cardPrototypeGO = new GameObject("CardDisplay_Prototype");
            cardPrototypeGO.AddComponent<RectTransform>();
            var img = cardPrototypeGO.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            var btn = cardPrototypeGO.AddComponent<Button>();
            btn.targetGraphic = img;
            cardPrototypeGO.AddComponent<CanvasGroup>();

            // Add CardDisplay component -- it will create fallback visuals if needed
            cardPrototypeGO.AddComponent<CardDisplay>();

            // Minimal child texts so prototype looks sensible when instantiated
            var rankGO = new GameObject("RankText", typeof(RectTransform));
            rankGO.transform.SetParent(cardPrototypeGO.transform, false);
            var rankText = rankGO.AddComponent<Text>();
            var suitFont = Resources.Load<Font>("Fonts/SuitFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rankText.font = suitFont;
            rankText.fontSize = 28;
            rankText.alignment = TextAnchor.UpperLeft;
            rankText.color = Color.black;
            var rrt = rankText.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(6f, -6f);

            var suitGO = new GameObject("SuitText", typeof(RectTransform));
            suitGO.transform.SetParent(cardPrototypeGO.transform, false);
            var suitText = suitGO.AddComponent<Text>();
            suitText.font = suitFont;
            suitText.fontSize = 28;
            suitText.alignment = TextAnchor.UpperLeft;
            suitText.color = Color.black;
            var srt = suitText.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(6f, -34f);

            // Ensure prototype has a reasonable size so instances are visible
            var rt = cardPrototypeGO.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta == Vector2.zero)
                rt.sizeDelta = new Vector2(100f, 140f);

            cardPrototypeGO.SetActive(false);
            Object.DontDestroyOnLoad(cardPrototypeGO);
        }

        var inst = Object.Instantiate(cardPrototypeGO);
        inst.name = "CardDisplay_Instance";
        inst.SetActive(true);
        return inst.GetComponent<CardDisplay>();
    }

    public static HandDisplay CreateHandDisplayInstance()
    {
        if (handPrototypeGO == null)
        {
            handPrototypeGO = new GameObject("HandDisplay_Prototype");
            handPrototypeGO.AddComponent<RectTransform>();
            var hd = handPrototypeGO.AddComponent<HandDisplay>();

            var shown = new GameObject("ShownHand", typeof(RectTransform));
            shown.transform.SetParent(handPrototypeGO.transform, false);
            var hidden = new GameObject("HiddenHand", typeof(RectTransform));
            hidden.transform.SetParent(handPrototypeGO.transform, false);

            // Set private serialized fields via reflection
            var hdType = typeof(HandDisplay);
            var hiddenField = hdType.GetField("hiddenHandContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            var shownField = hdType.GetField("shownHandContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hiddenField != null)
                hiddenField.SetValue(hd, hidden.transform);
            if (shownField != null)
                shownField.SetValue(hd, shown.transform);

            handPrototypeGO.SetActive(false);
            Object.DontDestroyOnLoad(handPrototypeGO);
        }

        var inst = Object.Instantiate(handPrototypeGO);
        inst.name = "HandDisplay_Instance";
        inst.SetActive(true);
        return inst.GetComponent<HandDisplay>();
    }
}
