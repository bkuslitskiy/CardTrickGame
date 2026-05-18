using UnityEngine;
using System.Collections;

public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    [SerializeField] private float phaseDuration = 1.5f;
    private bool isRunning = false;
    public bool IsGamePaused { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetPhaseDuration(float duration)
    {
        phaseDuration = duration;
    }

    public void StartGameLoop()
    {
        if (!isRunning)
        {
            isRunning = true;
            IsGamePaused = false;
            StartCoroutine(LoopCoroutine());
        }
    }

    public void PauseGameLoop()
    {
        IsGamePaused = true;
    }

    public void ResumeGameLoop()
    {
        IsGamePaused = false;
    }

    public void StopGameLoop()
    {
        isRunning = false;
        StopAllCoroutines();
    }

    public void AdvancePhaseManually()
    {
        if (GameManagerController.Instance != null)
        {
             GameManagerController.Instance.ExecutePhase();
        }
    }

    private IEnumerator LoopCoroutine()
    {
        while (isRunning)
        {
            if (!IsGamePaused && GameManagerController.Instance != null)
            {
                GameManagerController.Instance.ExecutePhase();
            }
            yield return new WaitForSeconds(phaseDuration);
        }
    }
}