using UnityEngine;

/// <summary>
/// Adjusts the RectTransform to stay within the device's Safe Area (avoids notches and rounded corners).
/// Essential for Android/iOS porting.
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    private void Awake()
    {
        RectTransform rt = GetComponent<RectTransform>();
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
    }
}