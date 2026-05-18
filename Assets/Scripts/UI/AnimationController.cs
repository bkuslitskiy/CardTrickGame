using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Master animation controller. Coordinates all visual feedback animations.
/// Ensures smooth, non-blocking animations throughout game flow.
/// </summary>
public class AnimationController : MonoBehaviour
{
    [SerializeField] private float cardPlayDuration = 0.5f;
    [SerializeField] private float trickWinnerDuration = 1f;
    [SerializeField] private float scoreUpdateDuration = 0.5f;
    [SerializeField] private float phaseTransitionDuration = 0.3f;
    [SerializeField] private AnimationCurve easeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve easeInOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    public static AnimationController Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    /// <summary>
    /// Animate card moving from hand to play zone
    /// </summary>
    public void AnimateCardPlay(CardDisplay cardDisplay, Vector3 targetPosition, Action onComplete = null)
    {
        StartCoroutine(AnimateCardPlayCoroutine(cardDisplay, targetPosition, onComplete));
    }
    
    private IEnumerator AnimateCardPlayCoroutine(CardDisplay cardDisplay, Vector3 targetPosition, Action onComplete)
    {
        if (cardDisplay == null) yield break;
        
        RectTransform rectTransform = cardDisplay.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;
        
        Vector3 startPosition = rectTransform.anchoredPosition;
        float elapsedTime = 0f;
        
        while (elapsedTime < cardPlayDuration && rectTransform != null)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / cardPlayDuration);
            float easeValue = easeOutCurve.Evaluate(normalizedTime);
            
            rectTransform.anchoredPosition = Vector3.Lerp(startPosition, targetPosition, easeValue);
            
            yield return null;
        }
        
        if (rectTransform != null) rectTransform.anchoredPosition = targetPosition;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate card being revealed (flip effect)
    /// </summary>
    public void AnimateCardReveal(CardDisplay cardDisplay, Action onComplete = null)
    {
        StartCoroutine(AnimateCardRevealCoroutine(cardDisplay, onComplete));
    }
    
    private IEnumerator AnimateCardRevealCoroutine(CardDisplay cardDisplay, Action onComplete)
    {
        if (cardDisplay == null) yield break;
        
        RectTransform rt = cardDisplay.GetComponent<RectTransform>();
        if (rt == null) yield break;
        
        float elapsedTime = 0f;
        float halfDuration = cardPlayDuration / 2f;
        Vector3 origRotation = rt.localEulerAngles;
        
        // Flip away (to 90 degrees)
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / halfDuration;
            rt.localEulerAngles = new Vector3(origRotation.x, Mathf.Lerp(0f, 90f, normalizedTime), origRotation.z);
            yield return null;
        }
        
        rt.localEulerAngles = new Vector3(origRotation.x, 90f, origRotation.z);
        yield return new WaitForSeconds(0.05f);
        
        // Flip back
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / halfDuration;
            rt.localEulerAngles = new Vector3(origRotation.x, Mathf.Lerp(90f, 0f, normalizedTime), origRotation.z);
            yield return null;
        }
        
        rt.localEulerAngles = new Vector3(origRotation.x, 0f, origRotation.z);
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate trick winner highlight and celebration
    /// </summary>
    public void AnimateTrickWinner(Image winnerHighlight, Action onComplete = null)
    {
        StartCoroutine(AnimateTrickWinnerCoroutine(winnerHighlight, onComplete));
    }
    
    private IEnumerator AnimateTrickWinnerCoroutine(Image winnerHighlight, Action onComplete)
    {
        if (winnerHighlight == null) yield break;
        
        winnerHighlight.enabled = true;
        float elapsedTime = 0f;
        
        // Flash 3 times
        for (int i = 0; i < 3; i++)
        {
            // Flash on
            elapsedTime = 0f;
            while (elapsedTime < 0.2f)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / 0.2f;
                Color c = winnerHighlight.color;
                c.a = Mathf.Lerp(0.3f, 1f, normalizedTime);
                winnerHighlight.color = c;
                yield return null;
            }
            
            // Flash off
            elapsedTime = 0f;
            while (elapsedTime < 0.2f)
            {
                elapsedTime += Time.deltaTime;
                float normalizedTime = elapsedTime / 0.2f;
                Color c = winnerHighlight.color;
                c.a = Mathf.Lerp(1f, 0.3f, normalizedTime);
                winnerHighlight.color = c;
                yield return null;
            }
        }
        
        winnerHighlight.enabled = false;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate score text update (scale pulse)
    /// </summary>
    public void AnimateScoreUpdate(Text scoreText, Action onComplete = null)
    {
        StartCoroutine(AnimateScoreUpdateCoroutine(scoreText, onComplete));
    }
    
    private IEnumerator AnimateScoreUpdateCoroutine(Text scoreText, Action onComplete)
    {
        if (scoreText == null) yield break;
        
        RectTransform rectTransform = scoreText.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;
        
        Vector3 startScale = rectTransform.localScale;
        float elapsedTime = 0f;
        
        // Scale up
        while (elapsedTime < scoreUpdateDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / (scoreUpdateDuration / 2f);
            float scale = Mathf.Lerp(1f, 1.3f, easeOutCurve.Evaluate(normalizedTime));
            rectTransform.localScale = startScale * scale;
            yield return null;
        }
        
        // Scale down
        elapsedTime = 0f;
        while (elapsedTime < scoreUpdateDuration / 2f)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / (scoreUpdateDuration / 2f);
            float scale = Mathf.Lerp(1.3f, 1f, easeInOutCurve.Evaluate(normalizedTime));
            rectTransform.localScale = startScale * scale;
            yield return null;
        }
        
        rectTransform.localScale = startScale;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate phase transition (fade text)
    /// </summary>
    public void AnimatePhaseTransition(Text phaseText, Action onComplete = null)
    {
        StartCoroutine(AnimatePhaseTransitionCoroutine(phaseText, onComplete));
    }
    
    private IEnumerator AnimatePhaseTransitionCoroutine(Text phaseText, Action onComplete)
    {
        if (phaseText == null) yield break;
        
        CanvasGroup canvasGroup = phaseText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = phaseText.gameObject.AddComponent<CanvasGroup>();
        }
        
        float elapsedTime = 0f;
        
        // Fade in
        while (elapsedTime < phaseTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / phaseTransitionDuration;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, easeOutCurve.Evaluate(normalizedTime));
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate a simple fade in effect
    /// </summary>
    public void AnimateFadeIn(CanvasGroup canvasGroup, float duration, Action onComplete = null)
    {
        if (canvasGroup != null)
            StartCoroutine(FadeCoroutine(canvasGroup, 0f, 1f, duration, onComplete));
    }
    
    /// <summary>
    /// Animate a simple fade out effect
    /// </summary>
    public void AnimateFadeOut(CanvasGroup canvasGroup, float duration, Action onComplete = null)
    {
        if (canvasGroup != null)
            StartCoroutine(FadeCoroutine(canvasGroup, 1f, 0f, duration, onComplete));
    }
    
    private IEnumerator FadeCoroutine(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, Action onComplete)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, easeOutCurve.Evaluate(normalizedTime));
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Animate a punch scale effect (for emphasis)
    /// </summary>
    public void AnimatePunch(RectTransform target, float strength = 0.1f, float duration = 0.3f, Action onComplete = null)
    {
        StartCoroutine(PunchCoroutine(target, strength, duration, onComplete));
    }
    
    private IEnumerator PunchCoroutine(RectTransform target, float strength, float duration, Action onComplete)
    {
        Vector3 originalScale = target.localScale;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / duration;
            
            // Ease out punch curve
            float punchValue = Mathf.Sin(normalizedTime * Mathf.PI) * strength;
            target.localScale = originalScale * (1f + punchValue);
            
            yield return null;
        }
        
        target.localScale = originalScale;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Alias for AnimatePrizeCard to match earlier implementation requests.
    /// </summary>
    public void AnimatePrizeToWinner(Transform cardTransform, Transform winnerTarget)
    {
        CardDisplay cardDisplay = cardTransform.GetComponent<CardDisplay>();
        AnimatePrizeCard(cardDisplay, winnerTarget);
    }

    /// <summary>
    /// Animate the prize card moving to the winner's score area.
    /// </summary>
    public void AnimatePrizeCard(CardDisplay cardDisplay, Transform scoreTarget, Action onComplete = null)
    {
        StartCoroutine(AnimatePrizeCardCoroutine(cardDisplay, scoreTarget, onComplete));
    }

    private IEnumerator AnimatePrizeCardCoroutine(CardDisplay cardDisplay, Transform scoreTarget, Action onComplete)
    {
        if (cardDisplay == null || scoreTarget == null) yield break;

        RectTransform rectTransform = cardDisplay.GetComponent<RectTransform>();
        Vector3 startPosition = rectTransform.position;
        Vector3 startScale = rectTransform.localScale;
        float duration = 0.8f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            float easeValue = easeOutCurve.Evaluate(normalizedTime);

            rectTransform.position = Vector3.Lerp(startPosition, scoreTarget.position, easeValue);
            rectTransform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, easeValue);
            rectTransform.rotation = Quaternion.Lerp(rectTransform.rotation, Quaternion.Euler(0, 0, 15f), easeValue);

            yield return null;
        }
        onComplete?.Invoke();
    }

    public float GetCardPlayDuration() => cardPlayDuration;
    public float GetTrickWinnerDuration() => trickWinnerDuration;
    public float GetScoreUpdateDuration() => scoreUpdateDuration;
}
