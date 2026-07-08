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

    [Header("Level Music")]
    [SerializeField] private AudioClip levelMusic;
    [SerializeField] private float     musicVolume   = 0.75f;
    [SerializeField] private float     musicFadeTime = 0.6f;

    [Header("Bus")]
    [SerializeField] private int busCapacity = 30;

    [Header("SFX")]
    [SerializeField] private AudioClip alightingBellClip;
    [SerializeField] private float     bellVolume = 0.85f;

    [Header("Day Clock")]
    [Tooltip("In-game hour the shift starts (24 h). Default 8 = 8:00 AM.")]
    [SerializeField] private int   dayStartHour          = 8;
    [Tooltip("How many real seconds equal one in-game hour. Default 90 = 90 s real → 1 h game.")]
    [SerializeField] private float realSecondsPerGameHour = 90f;

    [Header("Scoring")]
    [SerializeField] private LevelScoreConfig scoreConfig;

    // Readable by BusStop (capacity enforcement) and FloatingPassengerCount (display).
    // Defaults to 30 so tests without a LevelManager in the scene still compile.
    public static int          BusCapacity { get; private set; } = 30;
    public static LevelManager Instance    { get; private set; }

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

    private LevelPhase    _phase;
    private BusController _busController;
    private AudioSource   _musicSource;
    private AudioSource   _sfxSource;
    private Coroutine     _musicFade;

    // ── Passengers ────────────────────────────────────────────────────────

    private int _currentPassengers;
    private int _maxPassengers;

    // HUD subscribes to this to update the passenger count display.
    // Fires whenever a passenger boards: (currentCount, maxCount)
    public static event Action<int, int> OnPassengerCountChanged;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Start()
    {
        Instance       = this;
        BusCapacity    = busCapacity;
        _busController = UnityEngine.Object.FindFirstObjectByType<BusController>();

        if (levelMusic != null)
        {
            _musicSource             = gameObject.AddComponent<AudioSource>();
            _musicSource.clip        = levelMusic;
            _musicSource.loop        = true;
            _musicSource.volume      = 0f;
            _musicSource.spatialBlend = 0f;
            _musicSource.playOnAwake = false;
        }

        _sfxSource              = gameObject.AddComponent<AudioSource>();
        _sfxSource.spatialBlend = 0f;
        _sfxSource.playOnAwake  = false;
        _sfxSource.volume       = PlayerPrefs.GetFloat(SettingsPanel.SfxVolKey, 1f);

        // Subscribe to events — these replace polling and keep systems decoupled
        CutscenePlayer.OnCutsceneComplete    += HandleCutsceneComplete;
        LevelTrigger.OnTriggered             += HandleTrigger;
        BusStop.OnBusArrived                 += HandleBusStopArrival;
        BusStop.OnAlightingStopApproached    += PlayAlightingBell;

        // Clear any score data left over from the previous level
        ScoreTracker.Reset();

        // Reset all stops and calculate the max possible passenger count
        InitialisePassengers();

        StartIntroCutscene();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CutscenePlayer.OnCutsceneComplete -= HandleCutsceneComplete;
        LevelTrigger.OnTriggered          -= HandleTrigger;
        BusStop.OnBusArrived             -= HandleBusStopArrival;
        BusStop.OnAlightingStopApproached -= PlayAlightingBell;
    }

    // ── Day Clock ─────────────────────────────────────────────────────────

    // Returns the current in-game time as a string (e.g. "8:02 AM").
    // Called by PassengerAgent at board/alight time to stamp ride records.
    // Returns "" if called outside a level scene.
    public static string GetCurrentTimeString()
    {
        if (Instance == null) return "";
        return Instance.FormatCurrentTime();
    }

    private string FormatCurrentTime()
    {
        float gameHours  = Time.timeSinceLevelLoad / realSecondsPerGameHour;
        int   totalMins  = Mathf.FloorToInt(gameHours * 60f);
        int   hour       = dayStartHour + totalMins / 60;
        int   minute     = totalMins % 60;
        bool  pm         = hour >= 12;
        int   display    = hour % 12;
        if (display == 0) display = 12;
        return $"{display}:{minute:D2} {(pm ? "PM" : "AM")}";
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
            _maxPassengers += stop.waitingPassengers + stop.waitingReturnPassengers;
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
        MusicFadeTo(musicVolume * PlayerPrefs.GetFloat(SettingsPanel.MusicVolKey, 1f));

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Driving);
    }

    private void StartDrivingToEnd()
    {
        _phase = LevelPhase.DrivingToEnd;
        _busController?.FlipForReturn();
        MusicFadeTo(musicVolume * PlayerPrefs.GetFloat(SettingsPanel.MusicVolKey, 1f));

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Driving);
    }

    private void CompleteLevel()
    {
        _phase = LevelPhase.Complete;

        // Hand music off to a persistent carrier instead of fading out —
        // it will keep playing on the Level Complete screen.
        HandOffMusicToNextScene();

        ScoreTracker.Report("passengers", _currentPassengers, _maxPassengers);
        ScoreTracker.FinalizeScore(scoreConfig);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteLevel(GameManager.Instance.GetCurrentLevelIndex());
        else
            SceneManager.LoadScene("LevelComplete");
    }

    private void HandOffMusicToNextScene()
    {
        if (_musicSource == null) return;

        // Stop any in-progress fade so we can set the volume cleanly
        if (_musicFade != null) { StopCoroutine(_musicFade); _musicFade = null; }

        // Resume from Pause (outro cutscene silences it) and restore volume
        float targetVol = musicVolume * PlayerPrefs.GetFloat(SettingsPanel.MusicVolKey, 1f);
        _musicSource.volume = targetVol;
        if (!_musicSource.isPlaying) _musicSource.Play();

        var carrier = new GameObject("LevelMusicCarrier");
        carrier.AddComponent<LevelMusicCarrier>().Initialize(
            _musicSource.clip, targetVol, _musicSource.time, fadeOut: 1.5f);

        _musicSource.Stop();
    }

    public void SetSfxVolume(float sliderValue)
    {
        if (_sfxSource != null) _sfxSource.volume = sliderValue;
    }

    private void PlayAlightingBell()
    {
        if (_sfxSource == null || alightingBellClip == null) return;
        _sfxSource.PlayOneShot(alightingBellClip, bellVolume);
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
                MusicFadeTo(0f);
                if (midpointCutscene != null)
                    cutscenePlayer.Play(midpointCutscene);
                else
                    StartDrivingToEnd();
                break;

            case "end" when _phase == LevelPhase.DrivingToEnd:
                _phase = LevelPhase.OutroCutscene;
                BusStop.ForceAlightAll();  // everyone off at the end of the line
                MusicFadeTo(0f);
                if (outroCutscene != null)
                    cutscenePlayer.Play(outroCutscene);
                else
                    CompleteLevel();
                break;
        }
    }

    // ── Music ─────────────────────────────────────────────────────────────

    // Called by SettingsPanel when the music slider moves
    public void SetMusicVolume(float sliderValue)
    {
        if (_musicSource == null || !_musicSource.isPlaying) return;
        _musicSource.volume = musicVolume * sliderValue;
    }

    private void MusicFadeTo(float target)
    {
        if (_musicSource == null) return;
        if (_musicFade != null) StopCoroutine(_musicFade);
        _musicFade = StartCoroutine(FadeMusic(target));
    }

    private System.Collections.IEnumerator FadeMusic(float target)
    {
        if (target > 0f && !_musicSource.isPlaying)
            _musicSource.Play();

        float start   = _musicSource.volume;
        float elapsed = 0f;
        while (elapsed < musicFadeTime)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(start, target, elapsed / musicFadeTime);
            yield return null;
        }
        _musicSource.volume = target;
        if (target == 0f) _musicSource.Pause();
    }

    // Called by BusStop when the bus enters a stop's trigger zone
    private void HandleBusStopArrival(BusStop stop)
    {
        bool collected = false;

        // Only collect passengers during active driving phases.
        // Arriving at a stop during a cutscene or after completion does nothing.
        int boarded = 0;
        if (_phase == LevelPhase.DrivingToMidpoint)
        {
            collected = stop.TryCollectOutbound();
            if (collected) boarded = stop.waitingPassengers;
        }
        else if (_phase == LevelPhase.DrivingToEnd)
        {
            collected = stop.TryCollectInbound();
            if (collected) boarded = stop.waitingReturnPassengers;
        }

        if (!collected) return;

        _currentPassengers += boarded;
        OnPassengerCountChanged?.Invoke(_currentPassengers, _maxPassengers);

        Debug.Log($"[LevelManager] {stop.stopName}: +{boarded} passengers " +
                  $"({_currentPassengers}/{_maxPassengers})");
    }
}
