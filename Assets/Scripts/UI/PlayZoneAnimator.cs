using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Play zone visual effects. Shows cards being played and removes them after determination.
/// </summary>
public class PlayZoneAnimator : MonoBehaviour
{
    [SerializeField] private Transform playZoneContainer;
    [SerializeField] private float cardEnterDuration = 0.4f;
    [SerializeField] private float cardExitDuration = 0.3f;
    [SerializeField] private AnimationCurve playZoneEasing = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private CanvasGroup playZoneCanvasGroup;
    
    private void Start()
    {
        if (playZoneContainer != null)
        {
            playZoneCanvasGroup = playZoneContainer.GetComponent<CanvasGroup>();
            if (playZoneCanvasGroup == null)
                playZoneCanvasGroup = playZoneContainer.gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    /// <summary>
    /// Animate card entering play zone
    /// </summary>
    public void AnimateCardEnter(CardDisplay cardDisplay, System.Action onComplete = null)
    {
        StartCoroutine(CardEnterCoroutine(cardDisplay, onComplete));
    }
    
    private IEnumerator CardEnterCoroutine(CardDisplay cardDisplay, System.Action onComplete)
    {
        if (cardDisplay == null) yield break;
        
        RectTransform rectTransform = cardDisplay.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = cardDisplay.GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
            canvasGroup = cardDisplay.gameObject.AddComponent<CanvasGroup>();
        
        Vector3 startScale = Vector3.one * 0.5f;
        Vector3 endScale = Vector3.one;
        canvasGroup.alpha = 0f;
        rectTransform.localScale = startScale;
        
        float elapsedTime = 0f;
        while (elapsedTime < cardEnterDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = playZoneEasing.Evaluate(elapsedTime / cardEnterDuration);
            
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, normalizedTime);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, normalizedTime);
            
            yield return null;
        }
        
        rectTransform.localScale = endScale;
        canvasGroup.alpha = 1f;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate play zone clearing after determination
    /// </summary>
    public void AnimatePlayZoneClear(System.Action onComplete = null)
    {
        StartCoroutine(PlayZoneClearCoroutine(onComplete));
    }
    
    private IEnumerator PlayZoneClearCoroutine(System.Action onComplete)
    {
        if (playZoneContainer == null) yield break;
        
        // Fade out all cards
        foreach (Transform child in playZoneContainer)
        {
            CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
            
            StartCoroutine(FadeOutCard(canvasGroup));
        }
        
        yield return new WaitForSeconds(cardExitDuration);
        
        // Clear containers
        foreach (Transform child in playZoneContainer)
        {
            Destroy(child.gameObject);
        }
        
        onComplete?.Invoke();
    }
    
    private IEnumerator FadeOutCard(CanvasGroup canvasGroup)
    {
        float elapsedTime = 0f;
        while (elapsedTime < cardExitDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / cardExitDuration;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, normalizedTime);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
    
    /// <summary>
    /// Highlight play zone when active
    /// </summary>
    public void HighlightPlayZone(bool highlight)
    {
        if (playZoneCanvasGroup != null)
        {
            playZoneCanvasGroup.alpha = highlight ? 1f : 0.7f;
        }
    }
}
