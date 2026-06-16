using TMPro;
using UnityEngine;

// Attach to: the HUD Canvas root in each level scene.
// Displays: speed (km/h), passenger count, and elapsed level time.
// Auto-hides when not in Driving or Paused state, auto-shows when driving starts.
//
// Use Mousebus → Setup Level Scene UI to auto-create the HUD hierarchy.
public class HUD : MonoBehaviour
{
    [Header("Speedometer")]
    [Tooltip("Bottom-right — displays current speed in km/h")]
    [SerializeField] private TMP_Text speedText;

    [Header("Next Stop")]
    [Tooltip("Top-right — shows the name of the nearest uncollected bus stop")]
    [SerializeField] private TMP_Text nextStopText;

    [Header("Bus Count")]
    [Tooltip("Displays current/max passengers, e.g. '12/40'")]
    [SerializeField] private TMP_Text busCountText;
    [SerializeField] private int      maxPassengers = 40;

    [Header("Clock")]
    [Tooltip("Top-left — displays elapsed driving time as MM:SS")]
    [SerializeField] private TMP_Text clockText;

    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Bus Reference")]
    [Tooltip("Leave empty — the HUD finds it automatically at Start")]
    [SerializeField] private BusController busController;

    private float _levelElapsedTime;
    private bool  _clockRunning;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameManager.OnStateChanged           += HandleStateChanged;
        LevelManager.OnPassengerCountChanged += HandlePassengerCountChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged           -= HandleStateChanged;
        LevelManager.OnPassengerCountChanged -= HandlePassengerCountChanged;
    }

    private void Start()
    {
        if (busController == null)
            busController = UnityEngine.Object.FindFirstObjectByType<BusController>();

        _levelElapsedTime = 0f;

        // Start clock running in standalone testing; GameManager pauses it during cutscenes
        _clockRunning = (GameManager.Instance == null);

        UpdatePassengerCount(0);
    }

    private void Update()
    {
        if (busController != null && speedText != null)
        {
            float kmh = Mathf.Abs(busController.CurrentSpeed) * 3.6f; // abs so reverse shows positive
            speedText.text = $"{Mathf.RoundToInt(kmh)}\n<size=60%>km/h</size>";
        }

        if (_clockRunning)
        {
            _levelElapsedTime += Time.deltaTime;
            RefreshClock();
        }

        UpdateNextStop();
    }

    private void UpdateNextStop()
    {
        if (nextStopText == null || busController == null) return;

        Vector3 busPos    = busController.transform.position;
        BusStop nearest   = null;
        float nearestDist = float.MaxValue;

        foreach (var stop in BusStop.ActiveStops)
        {
            if (stop.IsFullyCollected) continue;
            float d = Vector3.Distance(busPos, stop.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = stop; }
        }

        nextStopText.text = nearest != null
            ? $"<size=65%><color=#AAAAAA>NEXT STOP</color></size>\n{nearest.stopName.ToUpper()}"
            : "";
    }

    // ── State ─────────────────────────────────────────────────────────────

    private void HandleStateChanged(GameManager.GameState state)
    {
        bool show = state == GameManager.GameState.Driving ||
                    state == GameManager.GameState.Paused;

        SetCanvasVisible(show, instant: false);

        // Clock pauses when the game pauses, resumes when driving
        _clockRunning = (state == GameManager.GameState.Driving);
    }

    private void SetCanvasVisible(bool visible, bool instant)
    {
        if (canvasGroup == null) return;

        float target = visible ? 1f : 0f;

        if (instant)
            canvasGroup.alpha = target;
        else
            StartCoroutine(UIAnimator.Fade(canvasGroup, canvasGroup.alpha, target, 0.25f));

        canvasGroup.blocksRaycasts = false; // HUD is display-only, never interactive
    }

    // ── Public API ────────────────────────────────────────────────────────

    // Receives live updates from LevelManager whenever a passenger boards
    private void HandlePassengerCountChanged(int current, int max)
    {
        maxPassengers = max;
        if (busCountText != null)
            busCountText.text = $"{current}/{max}";
    }

    // Still available for manual calls from other scripts if needed
    public void UpdatePassengerCount(int current)
    {
        if (busCountText != null)
            busCountText.text = $"{current}/{maxPassengers}";
    }

    // ── Clock ─────────────────────────────────────────────────────────────

    private void RefreshClock()
    {
        if (clockText == null) return;
        int minutes = Mathf.FloorToInt(_levelElapsedTime / 60f);
        int seconds = Mathf.FloorToInt(_levelElapsedTime % 60f);
        clockText.text = $"{minutes:00}:{seconds:00}";
    }
}
