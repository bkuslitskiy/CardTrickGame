using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Modal Tips window displayed from the main menu or in-game header.
/// Built entirely from code to match the project's runtime-UI pattern.
///
/// Usage:
///   TipsWindowUI.Show(parentCanvas.transform);
///   — or —
///   Wire the static Show() call to a Button.onClick in the scene/MenuUI.
///
/// Scene setup (one-time):
///   Add a Tips button alongside the existing Easy/Medium/Hard/Mixed buttons in
///   MenuUI and call TipsWindowUI.Show(canvas.transform) from its onClick.
///   No prefab required.
/// </summary>
public class TipsWindowUI : MonoBehaviour
{
    // ── Singleton / Static entry point ────────────────────────────────────
    private static TipsWindowUI _instance;

    /// <summary>Show the Tips window, creating it if it doesn't exist yet.</summary>
    public static void Show(Transform canvasParent)
    {
        if (_instance == null)
            _instance = BuildWindow(canvasParent);
        _instance.gameObject.SetActive(true);
    }

    // ── Content ───────────────────────────────────────────────────────────

    private static readonly (string tier, Color header, TipEntry[] tips)[] Sections =
    {
        (
            "🃏  Beginner — Understanding the Basics",
            new Color(0.36f, 0.72f, 0.36f),
            new[]
            {
                new TipEntry("Your Hidden cards are worth their face value when played. A King from the Hidden hand scores 13. Simple."),
                new TipEntry("Shown cards follow different rules. A King from the Shown hand is worth only 5. An Ace Shown is 10. A Queen Shown is 14 — the highest score possible. Learn these before planning your plays."),
                new TipEntry("The final score uses face value only, regardless of which hand the card came from. Win tricks to claim prizes; prizes are worth what's printed on the card."),
                new TipEntry("You don't choose which card gets revealed — that's random. But you do choose whose hand gets targeted. That choice matters a great deal."),
            }
        ),
        (
            "⚡  Intermediate — Playing with Intent",
            new Color(0.94f, 0.68f, 0.30f),
            new[]
            {
                new TipEntry("Target reveals wisely. Forcing a strong card from Hidden into the Shown hand changes its play value — sometimes dramatically. A hidden Ace (14) becomes a Shown Ace (10), while a hidden Queen (12) becomes a Shown Queen (14), which can be even more dangerous in the wrong hands."),
                new TipEntry("Disrupt the leader. Target reveals at whoever is currently ahead in points. Exposing one of their high cards limits how they can play it."),
                new TipEntry("Expose players with few hidden cards. Once a player's entire hand is Shown, all their cards are public knowledge and you can outmanoeuvre them with near-perfect information."),
                new TipEntry("When leading a trick, play a mid-value card. Leading your best card hands the trick to whoever holds a slightly better one — and telegraphs your strength. A mid card baits opponents into committing."),
                new TipEntry("Ties are a weapon. If you can't beat the leader but can match their score, you deny them the trick entirely — both cards are discarded."),
            }
        ),
        (
            "🧠  Advanced — Card Counting & Precision",
            new Color(0.85f, 0.33f, 0.31f),
            new[]
            {
                new TipEntry("Track what has been discarded. There are 52 cards in the deck. As the game progresses you can narrow down what high-value cards are still in play — and whose hands they are most likely in."),
                new TipEntry("When following in a trick, spend the minimum that wins. If the current high scores 11 and you hold cards scoring 12, 13, and 14 — play the 12. Save the others for tricks where winning costs less."),
                new TipEntry("Use the Shown King as a cheap winner. With a play score of only 5, opponents may not bother burning a strong card to beat it. If you need to win a low-stakes trick, spending it here is efficient — and it preserves your high face-value cards for prizes."),
                new TipEntry("When you can't win, protect your prize potential. Throw your lowest face-value card. Even losing the trick, you minimise the prize you're handing the winner — and you keep your high-value cards for tricks where you can claim the prize yourself."),
                new TipEntry("Read the prize pool before deciding. If the remaining cards are low-value, deliberately losing a trick isn't a tragedy. Fighting to win a trick that only yields a 3♣ isn't worth burning an Ace."),
                new TipEntry("Late-game reveals are more powerful. When players have fewer cards, a single reveal can expose a large fraction of someone's remaining hand, giving you near-perfect information for the final tricks."),
            }
        ),
    };

    // ── Builder ───────────────────────────────────────────────────────────

    private static TipsWindowUI BuildWindow(Transform parent)
    {
        // Root overlay — full-screen dark backdrop
        var root = new GameObject("TipsWindow", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.88f);
        var bgRT = bg.rectTransform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Panel — centred card
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.11f, 0.24f, 0.17f, 1f); // dark green
        var panelRT = panelImg.rectTransform;
        panelRT.anchorMin = new Vector2(0.1f, 0.05f);
        panelRT.anchorMax = new Vector2(0.9f, 0.95f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Scroll view inside panel
        var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
        scrollGO.transform.SetParent(panel.transform, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0.08f);
        scrollRT.anchorMax = new Vector2(1f, 0.93f);
        scrollRT.offsetMin = new Vector2(10f, 0f);
        scrollRT.offsetMax = new Vector2(-10f, 0f);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();

        // Viewport
        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;
        viewportGO.AddComponent<Image>().color = Color.clear;

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding   = new RectOffset(16, 16, 16, 16);
        vlg.spacing   = 10f;
        vlg.childControlHeight = true;
        vlg.childControlWidth  = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content  = contentRT;
        scrollRect.viewport = viewportRT;
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        scrollRect.scrollSensitivity = 30f;

        // Title
        AddText(contentGO.transform, "Strategy Tips", 26, Color.white,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0f));

        // Tier sections
        foreach (var section in Sections)
        {
            AddText(contentGO.transform, section.tier, 18, section.header,
                    FontStyle.Bold, TextAnchor.MiddleLeft);

            foreach (var tip in section.tips)
                AddTipRow(contentGO.transform, tip.Text);

            AddSpacer(contentGO.transform, 8f);
        }

        // Close button
        AddCloseButton(panel.transform, root);

        // Header label
        AddText(panel.transform, "Tips & Strategy", 20, new Color(1f, 0.84f, 0f),
                FontStyle.Bold, TextAnchor.MiddleCenter, topAnchorMin: new Vector2(0f, 0.93f),
                topAnchorMax: Vector2.one);

        var comp = root.AddComponent<TipsWindowUI>();
        root.SetActive(false);
        return comp;
    }

    // ── UI Building Helpers ───────────────────────────────────────────────

    private static GameObject AddText(
        Transform parent,
        string text,
        int fontSize,
        Color color,
        FontStyle style = FontStyle.Normal,
        TextAnchor anchor = TextAnchor.UpperLeft,
        Color? background = null,
        Vector2? topAnchorMin = null,
        Vector2? topAnchorMax = null)
    {
        var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(12, text.Length)),
                                 typeof(RectTransform));
        go.transform.SetParent(parent, false);

        if (background.HasValue)
        {
            var bg = go.AddComponent<Image>();
            bg.color = background.Value;
        }

        var t = go.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text      = text;
        t.fontSize  = fontSize;
        t.color     = color;
        t.fontStyle = style;
        t.alignment = anchor;
        t.supportRichText = true;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Absolute-positioned labels (header/close) need manual anchors
        if (topAnchorMin.HasValue && topAnchorMax.HasValue)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = topAnchorMin.Value;
            rt.anchorMax = topAnchorMax.Value;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        return go;
    }

    private static void AddTipRow(Transform parent, string tipText)
    {
        var row = new GameObject("TipRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding   = new RectOffset(4, 4, 2, 2);
        hlg.spacing   = 8f;
        hlg.childControlHeight = true;
        hlg.childControlWidth  = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var rowCSF = row.AddComponent<ContentSizeFitter>();
        rowCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        rowCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Bullet
        var bulletGO = new GameObject("Bullet", typeof(RectTransform));
        bulletGO.transform.SetParent(row.transform, false);
        var bulletText = bulletGO.AddComponent<Text>();
        bulletText.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bulletText.text     = "•";
        bulletText.fontSize = 16;
        bulletText.color    = new Color(1f, 0.84f, 0f);
        bulletText.alignment = TextAnchor.UpperCenter;
        var bulletLE = bulletGO.AddComponent<LayoutElement>();
        bulletLE.preferredWidth  = 16f;
        bulletLE.flexibleWidth   = 0f;

        // Tip body
        var bodyGO = new GameObject("TipBody", typeof(RectTransform));
        bodyGO.transform.SetParent(row.transform, false);
        var bodyText = bodyGO.AddComponent<Text>();
        bodyText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bodyText.text      = tipText;
        bodyText.fontSize  = 14;
        bodyText.color     = new Color(0.9f, 0.9f, 0.9f);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.supportRichText = true;

        var bodyCSF = bodyGO.AddComponent<ContentSizeFitter>();
        bodyCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        bodyCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var bodyLE = bodyGO.AddComponent<LayoutElement>();
        bodyLE.flexibleWidth = 1f;
    }

    private static void AddSpacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth   = 1f;
    }

    private static void AddCloseButton(Transform panel, GameObject root)
    {
        var btnGO = new GameObject("CloseButton", typeof(RectTransform));
        btnGO.transform.SetParent(panel, false);

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.75f, 0.18f, 0.18f);

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => root.SetActive(false));

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.3f, 0f);
        rt.anchorMax = new Vector2(0.7f, 0.08f);
        rt.offsetMin = new Vector2(0f, 6f);
        rt.offsetMax = new Vector2(0f, -4f);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var lbl = labelGO.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.text      = "Close";
        lbl.fontSize  = 16;
        lbl.color     = Color.white;
        lbl.fontStyle = FontStyle.Bold;
        lbl.alignment = TextAnchor.MiddleCenter;
        var lblRT = lbl.rectTransform;
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
    }

    // ── Data ──────────────────────────────────────────────────────────────
    private readonly struct TipEntry
    {
        public readonly string Text;
        public TipEntry(string text) { Text = text; }
    }
}
