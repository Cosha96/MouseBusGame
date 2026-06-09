using UnityEngine;
using UnityEngine.UI;

// Attach to: the PauseMenu root in each level scene.
// Handles Escape / gamepad Start to toggle pause, and shows/hides via GameManager state.
//
// Use Mousebus → Setup Level Scene UI to auto-create this hierarchy.
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button      resumeButton;
    [SerializeField] private Button      mainMenuButton;
    [SerializeField] private Button      quitButton;
    [SerializeField] private float       fadeDuration = 0.2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.OnStateChanged += HandleStateChanged;
    private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;

    private void Start()
    {
        resumeButton?.onClick.AddListener(OnResume);
        mainMenuButton?.onClick.AddListener(OnMainMenu);
        quitButton?.onClick.AddListener(OnQuit);

        // Start hidden
        SetVisible(false, instant: true);
    }

    private void Update()
    {
        // Toggle pause on Escape or gamepad Start.
        // Only toggle if we're in a state that allows pausing (Driving or Paused).
        // The null check on GameManager handles isolated scene testing.
        if (InputManager.PausePressed)
            GameManager.Instance?.TogglePause();
    }

    // ── State ─────────────────────────────────────────────────────────────

    private void HandleStateChanged(GameManager.GameState state)
    {
        bool show = (state == GameManager.GameState.Paused);
        SetVisible(show, instant: false);
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

    // ── Buttons ───────────────────────────────────────────────────────────

    private void OnResume()   => GameManager.Instance?.TogglePause();
    private void OnMainMenu() => GameManager.Instance?.LoadMainMenu();
    private void OnQuit()     => GameManager.Instance?.QuitGame();
}
