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
    [Tooltip("Displays current speed in km/h")]
    [SerializeField] private TMP_Text speedText;

    [Header("Bus Count")]
    [Tooltip("Displays current/max passengers, e.g. '12/40'")]
    [SerializeField] private TMP_Text busCountText;
    [SerializeField] private int      maxPassengers = 40; // configurable per level later

    [Header("Clock")]
    [Tooltip("Displays elapsed driving time as MM:SS")]
    [SerializeField] private TMP_Text clockText;

    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Bus Reference")]
    [Tooltip("Leave empty — the HUD finds it automatically at Start")]
    [SerializeField] private BusController busController;

    private float _levelElapsedTime;
    private bool  _clockRunning;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.OnStateChanged += HandleStateChanged;
    private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;

    private void Start()
    {
        // Auto-find the bus if not wired in the Inspector
        if (busController == null)
            busController = UnityEngine.Object.FindFirstObjectByType<BusController>();

        _levelElapsedTime = 0f;

        // Start hidden — HandleStateChanged reveals it when driving begins
        SetCanvasVisible(false, instant: true);

        // Initialise the passenger display
        UpdatePassengerCount(0);
    }

    private void Update()
    {
        // Speed display — runs every frame while visible
        if (busController != null && speedText != null)
        {
            float kmh = busController.CurrentSpeed * 3.6f; // m/s → km/h
            speedText.text = $"{Mathf.RoundToInt(kmh)} km/h";
        }

        // Clock only ticks while driving (stops when paused or in cutscene)
        if (_clockRunning)
        {
            _levelElapsedTime += Time.deltaTime;
            RefreshClock();
        }
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

    // Call this when a passenger boards or exits (System 5 — passenger logic)
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
