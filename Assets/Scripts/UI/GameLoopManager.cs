using UnityEngine;
using System.Collections;

/// <summary>
/// Drives the game loop coroutine with phase-aware timing.
///
/// Timing model:
///   - AI card plays (Play phase, no human waiting): short delay so the board
///     doesn't feel instant but doesn't drag.
///   - Reveal / Determine results: longer display pause so the player can read
///     what happened before the next phase begins.
///   - Score phase bookkeeping: near-instant.
///   - Human input: the loop still ticks at aiTurnDelay, but GameManagerController
///     returns early from ExecutePhase while waiting — so no observable delay.
/// </summary>
public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    [Tooltip("Delay between individual AI card plays (Play phase).")]
    [SerializeField] private float aiTurnDelay = 0.4f;

    [Tooltip("Pause after Reveal and Determine phases so the result is visible.")]
    [SerializeField] private float phaseDisplayDelay = 1.5f;

    [Tooltip("Legacy field — still respected by SetPhaseDuration() callers (e.g. replay fast-forward).")]
    [SerializeField] private float phaseDuration = 1.5f;

    private bool isRunning = false;
    private bool overrideDuration = false; // true when SetPhaseDuration was called explicitly

    public bool IsGamePaused { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Override both delays to a fixed value — used by replay fast-forward.
    /// Call with a negative value to revert to adaptive timing.
    /// </summary>
    public void SetPhaseDuration(float duration)
    {
        phaseDuration = duration;
        overrideDuration = duration >= 0f;
    }

    public float GetPhaseDuration() => phaseDuration;

    public void StartGameLoop()
    {
        if (!isRunning)
        {
            isRunning = true;
            IsGamePaused = false;
            StartCoroutine(LoopCoroutine());
        }
    }

    public void PauseGameLoop()  => IsGamePaused = true;
    public void ResumeGameLoop() => IsGamePaused = false;

    public void StopGameLoop()
    {
        isRunning = false;
        StopAllCoroutines();
    }

    public void AdvancePhaseManually()
    {
        GameManagerController.Instance?.ExecutePhase();
    }

    // ──────────────────────────────────────────────
    // Loop
    // ──────────────────────────────────────────────

    private IEnumerator LoopCoroutine()
    {
        while (isRunning)
        {
            if (!IsGamePaused && GameManagerController.Instance != null)
                GameManagerController.Instance.ExecutePhase();

            yield return new WaitForSeconds(NextTickDelay());
        }
    }

    /// <summary>
    /// Compute how long to wait before the next loop tick.
    /// When an explicit override is active (e.g. replay) use the overridden value.
    /// Otherwise choose based on the current game phase.
    /// </summary>
    private float NextTickDelay()
    {
        if (overrideDuration) return phaseDuration;

        var gmc = GameManagerController.Instance;
        if (gmc == null) return aiTurnDelay;

        var state = gmc.GetGameState();
        if (state == null) return aiTurnDelay;

        return state.CurrentPhase switch
        {
            // Show the reveal result long enough for the player to read it.
            GamePhase.Reveal    => phaseDisplayDelay * 0.75f,
            // Each AI card play gets a short pause; human turns block via
            // PrepareForPhaseIfHuman so the delay here doesn't matter much.
            GamePhase.Play      => aiTurnDelay,
            // Give the player time to see who won and what prize they got.
            GamePhase.Determine => phaseDisplayDelay,
            // Bookkeeping only — advance quickly.
            GamePhase.Score     => aiTurnDelay * 0.5f,
            _                   => aiTurnDelay,
        };
    }
}
