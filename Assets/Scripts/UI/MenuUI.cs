using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu UI controller. Handles difficulty selection and game start.
/// </summary>
public class MenuUI : MonoBehaviour
{
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button mixedButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Text titleText;
    
    private bool initialized = false;
    
    public void Initialize()
    {
        if (initialized) return;
        
        if (easyButton != null)
            easyButton.onClick.AddListener(() => StartGameWithDifficulty(new[] { Difficulty.Easy, Difficulty.Easy, Difficulty.Easy, Difficulty.Easy }));
        
        if (mediumButton != null)
            mediumButton.onClick.AddListener(() => StartGameWithDifficulty(new[] { Difficulty.Medium, Difficulty.Medium, Difficulty.Medium, Difficulty.Medium }));
        
        if (hardButton != null)
            hardButton.onClick.AddListener(() => StartGameWithDifficulty(new[] { Difficulty.Hard, Difficulty.Hard, Difficulty.Hard, Difficulty.Hard }));
        
        if (mixedButton != null)
            mixedButton.onClick.AddListener(() => StartGameWithDifficulty(new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.Easy }));
        
        if (quitButton != null)
            quitButton.onClick.AddListener(() => Application.Quit());
        
        initialized = true;
    }
    private void StartGameWithDifficulty(Difficulty[] difficulties)
    {
        // Initialize and start the game in the current scene
        var gm = GameManagerController.Instance;
        if (gm == null)
        {
            Debug.LogError("GameManagerController not found! Cannot start game.");
            return;
        }

        // Initialize and start
        gm.InitializeGame(difficulties);
        gm.StartGame();

        // Start the game loop
        var loop = GameLoopManager.Instance;
        if (loop != null)
        {
            loop.SetPhaseDuration(2f);
            loop.StartGameLoop();
        }

        // Hide menu and show game UI
        var ui = UIManager.Instance;
        if (ui != null)
            ui.HideMenu();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Transform stats = transform.Find("StatsPanel");
        if (stats != null) stats.gameObject.SetActive(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
