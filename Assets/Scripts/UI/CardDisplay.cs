using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Fully self-contained CardDisplay that auto-binds its components.
/// Handles both TextMeshPro and Legacy Text seamlessly.
/// Requires zero Unity Editor setup.
/// </summary>
public class CardDisplay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Card cardData;
    private int ownerIndex;
    private Button button;
    private Outline outline;
    
    [Header("Visual Settings")]
    [SerializeField] private Color faceUpColor = Color.white;
    [SerializeField] private Color faceDownColor = Color.grey;
    [SerializeField] private Color redSuitColor = new Color(0.8f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color blackSuitColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    // Fallbacks for both UI text systems
    private Text legacyRankText;
    private Text legacySuitText;
    private Text legacyModifierText;
    private TMP_Text tmproRankText;
    private TMP_Text tmproSuitText;
    private Image cardBackground;

    private Vector3 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;

    private void Awake()
    {
        AutoBind();
    }

    private void AutoBind()
    {
        if (cardBackground == null) cardBackground = GetComponent<Image>();
        if (button == null) button = GetComponent<Button>();
        if (outline == null) outline = GetComponent<Outline>();

        Transform rankTransform = transform.Find("RankText");
        if (rankTransform == null)
        {
            GameObject rankGO = new GameObject("RankText", typeof(RectTransform));
            rankGO.transform.SetParent(transform, false);
            legacyRankText = rankGO.AddComponent<Text>();
            legacyRankText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacyRankText.alignment = TextAnchor.UpperLeft;
            legacyRankText.fontSize = 32;
            legacyRankText.raycastTarget = false;
            legacyRankText.resizeTextForBestFit = true;
            legacyRankText.resizeTextMinSize = 14;
            legacyRankText.resizeTextMaxSize = 40;
            
            RectTransform rt = rankGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(6, 6);
            rt.offsetMax = new Vector2(-6, -6);
        }
        else
        {
            legacyRankText = rankTransform.GetComponent<Text>();
            tmproRankText = rankTransform.GetComponent<TMP_Text>();
        }
        
        Transform suitTransform = transform.Find("SuitText");
        if (suitTransform == null)
        {
            GameObject suitGO = new GameObject("SuitText", typeof(RectTransform));
            suitGO.transform.SetParent(transform, false);
            legacySuitText = suitGO.AddComponent<Text>();
            legacySuitText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacySuitText.alignment = TextAnchor.UpperLeft;
            legacySuitText.fontSize = 32;
            legacySuitText.raycastTarget = false;
            legacySuitText.resizeTextForBestFit = true;
            legacySuitText.resizeTextMinSize = 14;
            legacySuitText.resizeTextMaxSize = 40;
            
            RectTransform rt = suitGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(6, 6);
            rt.offsetMax = new Vector2(-6, -34);
        }
        else
        {
            legacySuitText = suitTransform.GetComponent<Text>();
            tmproSuitText = suitTransform.GetComponent<TMP_Text>();
        }

        Transform modifierTransform = transform.Find("ModifierText");
        if (modifierTransform == null)
        {
            GameObject modGO = new GameObject("ModifierText", typeof(RectTransform));
            modGO.transform.SetParent(transform, false);
            legacyModifierText = modGO.AddComponent<Text>();
            legacyModifierText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacyModifierText.alignment = TextAnchor.LowerCenter;
            legacyModifierText.fontSize = 18;
            legacyModifierText.raycastTarget = false;
            legacyModifierText.fontStyle = FontStyle.BoldAndItalic;
            
            RectTransform rt = modGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -20);
        }

        if (cardBackground != null)
        {
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.4f);
                outline.effectDistance = new Vector2(2, -2);
            }
        }
    }

    public Card GetCard() => cardData;

    public void SetCard(Card card, HandSection? section = null)
    {
        this.cardData = card;
        SetCardData(card, false, section);
    }

    public void SetCardBack(Card card)
    {
        this.cardData = card;
        SetCardData(card, true);
    }

    public void SetOwnerIndex(int index) => ownerIndex = index;
    public int GetOwnerIndex() => ownerIndex;

    public void MakeClickable(UnityEngine.Events.UnityAction action)
    {
        AutoBind();
        if (button == null) button = gameObject.AddComponent<Button>();
        
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.interactable = true;
    }

    public void MakeUnclickable()
    {
        AutoBind();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }
    }

    public void SetHighlightColor(Color color)
    {
        AutoBind();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(4, -4);
        }
        outline.effectColor = color;
    }

    public void Highlight()
    {
        AutoBind();
        if (outline != null) outline.enabled = true;
    }

    public void RemoveHighlight()
    {
        AutoBind();
        if (outline != null) outline.enabled = false;
    }

    public void SetCardData(Card cardData, bool isHidden, HandSection? section = null)
    {
        AutoBind();
        if (isHidden || cardData == null)
        {
            SetText("", "", Color.black, "");
            if (cardBackground != null) cardBackground.color = faceDownColor;
        }
        else
        {
            string rankStr = cardData.GetFaceValue().ToString();
            if (rankStr == "11" || rankStr == "Jack") rankStr = "J";
            else if (rankStr == "12" || rankStr == "Queen") rankStr = "Q";
            else if (rankStr == "13" || rankStr == "King") rankStr = "K";
            else if (rankStr == "14" || rankStr == "Ace") rankStr = "A";
            
            string suitStr = cardData.Suit.ToString();
            if (suitStr == "Hearts") suitStr = "♥";
            else if (suitStr == "Diamonds") suitStr = "♦";
            else if (suitStr == "Clubs") suitStr = "♣";
            else if (suitStr == "Spades") suitStr = "♠";

            bool isRed = (suitStr == "♥" || suitStr == "♦" || cardData.Suit.ToString() == "Hearts" || cardData.Suit.ToString() == "Diamonds");
            Color suitColor = isRed ? redSuitColor : blackSuitColor;

            string modifier = "";
            if (section == HandSection.Shown)
            {
                modifier = "SHOWN";
                if (cardBackground != null) cardBackground.color = new Color(1f, 0.95f, 0.8f); // Light gold tint
            }
            else
            {
                if (cardBackground != null) cardBackground.color = faceUpColor;
            }

            SetText(rankStr, suitStr, suitColor, modifier);
        }
    }

    private void SetText(string rankStr, string suitStr, Color color, string modifier)
    {
        if (legacyRankText != null) { legacyRankText.text = rankStr; legacyRankText.color = color; }
        if (tmproRankText != null) { tmproRankText.text = rankStr; tmproRankText.color = color; }
        
        if (legacySuitText != null) { legacySuitText.text = suitStr; legacySuitText.color = color; }
        if (tmproSuitText != null) { tmproSuitText.text = suitStr; tmproSuitText.color = color; }

        if (legacyModifierText != null) { legacyModifierText.text = modifier; legacyModifierText.color = new Color(0.1f, 0.4f, 0.8f); }
    }

    // --- Drag and Drop Implementation ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        
        originalPosition = transform.position;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        
        // Break out of the layout group to drag freely over the canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            transform.SetParent(canvas.transform, true);
            
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // Allow drops to pass through this object
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = true;
        
        // Intuitive "Flick to play" mechanic: if dragged upwards significantly, invoke the click!
        if (transform.position.y > originalPosition.y + 75f)
        {
            button.onClick.Invoke();
        }
        
        // Snap back to hand visually (will be overridden by the play animation if the move was valid)
        if (originalParent != null)
        {
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.position = originalPosition;
        }
    }
}