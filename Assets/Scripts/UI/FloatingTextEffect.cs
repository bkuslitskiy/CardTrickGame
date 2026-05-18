using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Floating text effect for score updates. Shows +X text floating up and fading.
/// </summary>
public class FloatingTextEffect : MonoBehaviour
{
    [SerializeField] private Text floatingText;
    [SerializeField] private float floatDuration = 1.5f;
    [SerializeField] private float floatDistance = 100f;
    [SerializeField] private AnimationCurve floatCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        // If no text component assigned, try to find/create one
        if (floatingText == null)
        {
            floatingText = GetComponent<Text>();
            if (floatingText == null)
            {
                floatingText = gameObject.AddComponent<Text>();
                floatingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                floatingText.alignment = TextAnchor.MiddleCenter;
            }
        }
    }
    
    /// <summary>
    /// Play the floating text effect
    /// </summary>
    public void PlayEffect(string text, Color textColor, System.Action onComplete = null)
    {
        if (floatingText != null)
        {
            floatingText.text = text;
            floatingText.color = textColor;
        }
        
        StartCoroutine(FloatCoroutine(onComplete));
    }
    
    private IEnumerator FloatCoroutine(System.Action onComplete)
    {
        Vector3 startPosition = rectTransform.anchoredPosition;
        Vector3 endPosition = startPosition + Vector3.up * floatDistance;
        
        float elapsedTime = 0f;
        while (elapsedTime < floatDuration && rectTransform != null && canvasGroup != null)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = floatCurve.Evaluate(elapsedTime / floatDuration);
            
            rectTransform.anchoredPosition = Vector3.Lerp(startPosition, endPosition, normalizedTime);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, normalizedTime);
            
            yield return null;
        }
        
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
