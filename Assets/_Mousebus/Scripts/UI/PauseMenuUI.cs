using UnityEngine;
using UnityEngine.UI;

// Attach to: the PauseMenu root in each level scene.
// Handles Escape / gamepad Start to toggle pause.
// Falls back to local Time.timeScale when no GameManager is present (isolated scene testing).
//
// Use Mousebus → Rebuild Pause Menu to auto-create this hierarchy.
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup       canvasGroup;
    [SerializeField] private GameObject        mainPanel;         // the 4 buttons group
    [SerializeField] private Button            resumeButton;
    [SerializeField] private Button            passengersButton;
    [SerializeField] private Button            settingsButton;
    [SerializeField] private Button            mainMenuButton;
    [SerializeField] private PassengerListPanel passengerListPanel;
    [SerializeField] private SettingsPanel      settingsPanel;
    [SerializeField] private float              fadeDuration = 0.15f;

    private bool _standalonePaused;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.OnStateChanged += HandleStateChanged;
    private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;

    private void Start()
    {
        resumeButton?.onClick.AddListener(OnResume);
        passengersButton?.onClick.AddListener(OnPassengers);
        settingsButton?.onClick.AddListener(OnSettings);
        mainMenuButton?.onClick.AddListener(OnMainMenu);

        SetVisible(false, instant: true);
    }

    private void Update()
    {
        if (!InputManager.PausePressed) return;
        if (CutscenePlayer.IsPlaying) return;   // Escape skips cutscenes, not pause

        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause();
        else
            ToggleStandalone();
    }

    // ── State ─────────────────────────────────────────────────────────────

    private void HandleStateChanged(GameManager.GameState state)
    {
        bool show = (state == GameManager.GameState.Paused);
        if (!show) ReturnToMainPanel();
        SetVisible(show, instant: false);
    }

    // Used when no GameManager exists (e.g. Level_Tutorial opened directly)
    private void ToggleStandalone()
    {
        _standalonePaused = !_standalonePaused;
        Time.timeScale    = _standalonePaused ? 0f : 1f;
        if (!_standalonePaused) ReturnToMainPanel();
        SetVisible(_standalonePaused, instant: false);
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (canvasGroup == null) return;
        float target = visible ? 1f : 0f;
        if (instant)
            canvasGroup.alpha = target;
        else
            StartCoroutine(UIAnimator.Fade(canvasGroup, canvasGroup.alpha, target, fadeDuration));
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable   = visible;
    }

    private void ReturnToMainPanel()
    {
        passengerListPanel?.Close();
        settingsPanel?.Close();
        mainPanel?.SetActive(true);
    }

    // ── Buttons ───────────────────────────────────────────────────────────

    private void OnResume()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.TogglePause();
        else
            ToggleStandalone();
    }

    private void OnPassengers()
    {
        mainPanel?.SetActive(false);
        passengerListPanel?.Open(ReturnToMainPanel);
    }

    private void OnSettings()
    {
        mainPanel?.SetActive(false);
        settingsPanel?.Open(ReturnToMainPanel);
    }
    private void OnMainMenu()  { GameManager.Instance?.LoadMainMenu(); }
}
