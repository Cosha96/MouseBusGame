using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to: an empty GameObject in each level scene called "LevelManager"
// Drag in references via the Inspector — run Mousebus → Setup Level Scene to do this automatically
public class LevelManager : MonoBehaviour
{
    [Header("Cutscenes")]
    [SerializeField] private CutscenePlayer cutscenePlayer;
    [SerializeField] private CutsceneData introCutscene;
    [SerializeField] private CutsceneData midpointCutscene;
    [SerializeField] private CutsceneData outroCutscene;

    [Header("Scoring")]
    [SerializeField] private LevelScoreConfig scoreConfig;

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

    // Cached at Start — used to flip the bus at the midpoint
    private BusController _busController;

    // ── Passengers ────────────────────────────────────────────────────────

    private int _currentPassengers;
    private int _maxPassengers;

    // HUD subscribes to this to update the passenger count display.
    // Fires whenever a passenger boards: (currentCount, maxCount)
    public static event Action<int, int> OnPassengerCountChanged;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Start()
    {
        // Cache the bus — used later to flip direction at the midpoint
        _busController = UnityEngine.Object.FindFirstObjectByType<BusController>();

        // Subscribe to events — these replace polling and keep systems decoupled
        CutscenePlayer.OnCutsceneComplete += HandleCutsceneComplete;
        LevelTrigger.OnTriggered          += HandleTrigger;
        BusStop.OnBusArrived              += HandleBusStopArrival;

        // Clear any score data left over from the previous level
        ScoreTracker.Reset();

        // Reset all stops and calculate the max possible passenger count
        InitialisePassengers();

        StartIntroCutscene();
    }

    private void OnDestroy()
    {
        // Always unsubscribe in OnDestroy to prevent ghost callbacks if the
        // scene is unloaded while a coroutine or animation is still running
        CutscenePlayer.OnCutsceneComplete -= HandleCutsceneComplete;
        LevelTrigger.OnTriggered          -= HandleTrigger;
        BusStop.OnBusArrived              -= HandleBusStopArrival;
    }

    // ── Passenger Initialisation ──────────────────────────────────────────

    private void InitialisePassengers()
    {
        _currentPassengers = 0;
        _maxPassengers     = 0;

        // Find every BusStop in the scene and tally up the maximum possible passengers.
        // Each stop contributes twice — once on the outbound leg, once on the inbound leg.
        BusStop[] stops = UnityEngine.Object.FindObjectsByType<BusStop>(FindObjectsSortMode.None);
        foreach (BusStop stop in stops)
        {
            stop.ResetStop();
            _maxPassengers += stop.waitingPassengers * 2;
        }

        // Broadcast starting count so HUD initialises correctly (shows "0/30" etc.)
        OnPassengerCountChanged?.Invoke(_currentPassengers, _maxPassengers);
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

        // Snap the bus 180° before driving resumes. If a midpoint cutscene just played,
        // the screen is still fading back in and the player won't see the snap.
        // Speed is zeroed inside FlipForReturn so the bus starts from rest.
        _busController?.FlipForReturn();

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Driving);
    }

    private void CompleteLevel()
    {
        _phase = LevelPhase.Complete;

        // Tally all scoring dimensions and compute the grade before the scene transition.
        // LevelCompleteUI reads ScoreTracker.LastResult after the load.
        ScoreTracker.Report("passengers", _currentPassengers, _maxPassengers);
        ScoreTracker.FinalizeScore(scoreConfig);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteLevel(GameManager.Instance.GetCurrentLevelIndex());
        else
            SceneManager.LoadScene("LevelComplete");
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

    // Called by BusStop when the bus enters a stop's trigger zone
    private void HandleBusStopArrival(BusStop stop)
    {
        bool collected = false;

        // Only collect passengers during active driving phases.
        // Arriving at a stop during a cutscene or after completion does nothing.
        if (_phase == LevelPhase.DrivingToMidpoint)
            collected = stop.TryCollectOutbound();
        else if (_phase == LevelPhase.DrivingToEnd)
            collected = stop.TryCollectInbound();

        if (!collected) return;

        _currentPassengers += stop.waitingPassengers;
        OnPassengerCountChanged?.Invoke(_currentPassengers, _maxPassengers);

        Debug.Log($"[LevelManager] {stop.stopName}: +{stop.waitingPassengers} passengers " +
                  $"({_currentPassengers}/{_maxPassengers})");
    }
}
