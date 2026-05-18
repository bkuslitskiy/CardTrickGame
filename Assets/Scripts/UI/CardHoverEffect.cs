using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

/// <summary>
/// Card hover effect. Shows visual feedback when mouse hovers over card.
/// Scales card up slightly and adds highlight.
/// </summary>
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.2f;
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private CardDisplay cardDisplay;
    private bool isHovering = false;
    private Coroutine scaleCoroutine;
    
    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        cardDisplay = GetComponent<CardDisplay>();
        originalScale = rectTransform.localScale;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovering) return;
        isHovering = true;
        
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        
        scaleCoroutine = StartCoroutine(ScaleCard(originalScale, originalScale * hoverScale, hoverDuration));
        
        if (cardDisplay != null)
            cardDisplay.Highlight();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering) return;
        isHovering = false;
        
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        
        scaleCoroutine = StartCoroutine(ScaleCard(rectTransform.localScale, originalScale, hoverDuration));
        
        if (cardDisplay != null)
            cardDisplay.RemoveHighlight();
    }
    
    private System.Collections.IEnumerator ScaleCard(Vector3 startScale, Vector3 endScale, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, normalizedTime);
            yield return null;
        }
        rectTransform.localScale = endScale;
    }
}
