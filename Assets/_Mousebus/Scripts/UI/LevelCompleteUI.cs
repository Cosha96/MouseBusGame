using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attach to: the root GameObject in the LevelComplete scene.
// GameManager.CompleteLevel() loads this scene and stores LastCompletedLevelIndex
// so we know which level was just finished.
//
// Use Mousebus → Setup Level Complete Scene to auto-create the hierarchy.
public class LevelCompleteUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text levelNameText;  // e.g. "01 – June"
    [SerializeField] private TMP_Text statsText;      // placeholder for future stats

    [Header("Buttons")]
    [SerializeField] private Button continueButton;   // load next level
    [SerializeField] private Button mainMenuButton;   // return to main menu
    [SerializeField] private Button statsButton;      // placeholder — disabled for now

    [Header("Animation")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("The 'Level Complete' title card that slides in from above")]
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private float fadeInDuration    = 0.5f;
    [SerializeField] private float titleSlideDuration = 0.35f;

    private void Start()
    {
        continueButton?.onClick.AddListener(OnContinue);
        mainMenuButton?.onClick.AddListener(OnMainMenu);
        statsButton?.onClick.AddListener(OnStats);

        // Stats button is a placeholder — unlock once the passenger system is in
        if (statsButton != null) statsButton.interactable = false;
        if (statsText   != null) statsText.text = "";

        PopulateLevelInfo();
        StartCoroutine(AnimateIn());
    }

    // ── Level Info ────────────────────────────────────────────────────────

    private void PopulateLevelInfo()
    {
        if (GameManager.Instance == null) return;

        int index = GameManager.Instance.LastCompletedLevelIndex;

        if (levelNameText != null)
        {
            levelNameText.text = index >= 0
                ? FormatLevelName(GameManager.Instance.GetLevelSceneName(index))
                : "";
        }
    }

    // ── Animate In ────────────────────────────────────────────────────────

    private IEnumerator AnimateIn()
    {
        // Canvas starts invisible
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Park the title card above the screen if assigned
        Vector2 shownPos  = Vector2.zero;
        Vector2 hiddenPos = Vector2.zero;
        if (titleRect != null)
        {
            shownPos  = titleRect.anchoredPosition;
            hiddenPos = shownPos + new Vector2(0f, Screen.height * 0.5f);
            titleRect.anchoredPosition = hiddenPos;
        }

        // Small delay so the scene doesn't pop in immediately after the level
        yield return new WaitForSecondsRealtime(0.2f);

        // Fade the whole canvas in first…
        if (canvasGroup != null)
            yield return StartCoroutine(UIAnimator.Fade(canvasGroup, 0f, 1f, fadeInDuration));

        // …then slide the title down into position
        if (titleRect != null)
            yield return StartCoroutine(UIAnimator.SlidePanel(titleRect, hiddenPos, shownPos, titleSlideDuration));
    }

    // ── Button Handlers ───────────────────────────────────────────────────

    private void OnContinue()
    {
        if (GameManager.Instance == null) return;

        int next = GameManager.Instance.LastCompletedLevelIndex + 1;

        if (next < GameManager.Instance.LevelCount)
            GameManager.Instance.LoadLevelByIndex(next);
        else
            GameManager.Instance.LoadMainMenu(); // completed all levels — back to menu
    }

    private void OnMainMenu() => GameManager.Instance?.LoadMainMenu();

    private void OnStats()
    {
        // Placeholder — populate when the passenger/stats system is built
        Debug.Log("[LevelCompleteUI] Stats panel not yet implemented.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // "Level_Tutorial" → "Tutorial"
    // "Level_01_June"  → "01 – June"
    private static string FormatLevelName(string sceneName)
    {
        if (sceneName == "Level_Tutorial") return "Tutorial";
        string[] parts = sceneName.Split('_');
        return parts.Length >= 3 ? $"{parts[1]} – {parts[2]}" : sceneName;
    }
}
