using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically disables raycast targets on fully transparent images
/// to prevent them from blocking mouse clicks on cards/players behind them.
/// Runs periodically as a background daemon to catch dynamically spawned UI.
/// </summary>
public class CanvasRaycastFixer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        var go = new GameObject("CanvasRaycastFixer_Daemon");
        go.AddComponent<CanvasRaycastFixer>();
        DontDestroyOnLoad(go);
    }

    private void Start()
    {
        // Run periodically to catch newly instantiated UI elements
        InvokeRepeating(nameof(FixRaycasts), 0.5f, 2f);
    }

    private void FixRaycasts()
    {
        Graphic[] allGraphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include);
        foreach (Graphic graphic in allGraphics)
        {
            // If an image or text is completely transparent and is intercepting clicks
            if (graphic.color.a <= 0.01f && graphic.raycastTarget)
            {
                // Don't disable if it's meant to be an invisible button
                if (graphic.GetComponent<Button>() != null)
                    continue;
                    
                graphic.raycastTarget = false;
            }
        }
    }
}