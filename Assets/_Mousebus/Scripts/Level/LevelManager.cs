using UnityEngine;

// Attach to: an empty GameObject in each level scene called "LevelManager"
// Drag in references via the Inspector — run Mousebus → Setup Level Scene to do this automatically
public class LevelManager : MonoBehaviour
{
    [Header("Cutscenes")]
    [SerializeField] private CutscenePlayer cutscenePlayer;
    [SerializeField] private CutsceneData introCutscene;
    [SerializeField] private CutsceneData midpointCutscene;
    [SerializeField] private CutsceneData outroCutscene;

    // ── Level Phase ───────────────────────────────────────────────────────
    // This enum is the spine of the level — every event routes through it.
    // Adding more phases later (e.g. passenger check, branching outros) means
    // extending this enum and adding cases to the switch, nothing else changes.
    private enum LevelPhase
    {
        IntroCutscene,      // opening diary entry
        DrivingToMidpoint,  // player drives forward to the halfway trigger
        MidpointCutscene,   // mid-level cutscene at the turnaround point
        DrivingToEnd,       // player drives back to the end trigger
        OutroCutscene,      // closing cutscene
        Complete            // level finished, handing off to GameManager
    }

    private LevelPhase _phase;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Start()
    {
        // Subscribe to events — these replace polling and keep systems decoupled
        CutscenePlayer.OnCutsceneComplete += HandleCutsceneComplete;
        LevelTrigger.OnTriggered          += HandleTrigger;

        StartIntroCutscene();
    }

    private void OnDestroy()
    {
        // Always unsubscribe in OnDestroy to prevent ghost callbacks if the
        // scene is unloaded while a coroutine or animation is still running
        CutscenePlayer.OnCutsceneComplete -= HandleCutsceneComplete;
        LevelTrigger.OnTriggered          -= HandleTrigger;
    }

    // ── Phase Transitions ─────────────────────────────────────────────────

    private void StartIntroCutscene()
    {
        _phase = LevelPhase.IntroCutscene;

        if (introCutscene != null)
            cutscenePlayer.Play(introCutscene);
        else
            StartDrivingToMidpoint(); // no intro assigned — skip straight to driving
    }

    private void StartDrivingToMidpoint()
    {
        _phase = LevelPhase.DrivingToMidpoint;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Driving);
    }

    private void StartDrivingToEnd()
    {
        _phase = LevelPhase.DrivingToEnd;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Driving);
    }

    private void CompleteLevel()
    {
        _phase = LevelPhase.Complete;

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteLevel(GameManager.Instance.GetCurrentLevelIndex());
        else
            Debug.Log("[LevelManager] Level complete! (No GameManager in scene — fine for isolated testing)");
    }

    // ── Event Handlers ────────────────────────────────────────────────────

    // Called by CutscenePlayer when any cutscene finishes or is skipped
    private void HandleCutsceneComplete()
    {
        switch (_phase)
        {
            case LevelPhase.IntroCutscene:
                StartDrivingToMidpoint();
                break;

            case LevelPhase.MidpointCutscene:
                StartDrivingToEnd();
                break;

            case LevelPhase.OutroCutscene:
                CompleteLevel();
                break;
        }
    }

    // Called by LevelTrigger when the bus enters a trigger zone
    private void HandleTrigger(LevelTrigger trigger)
    {
        switch (trigger.triggerId)
        {
            case "halfway" when _phase == LevelPhase.DrivingToMidpoint:
                _phase = LevelPhase.MidpointCutscene;
                if (midpointCutscene != null)
                    cutscenePlayer.Play(midpointCutscene);
                else
                    StartDrivingToEnd(); // no midpoint cutscene assigned — keep driving
                break;

            case "end" when _phase == LevelPhase.DrivingToEnd:
                _phase = LevelPhase.OutroCutscene;
                if (outroCutscene != null)
                    cutscenePlayer.Play(outroCutscene);
                else
                    CompleteLevel(); // no outro assigned — complete immediately
                break;
        }
    }
}
