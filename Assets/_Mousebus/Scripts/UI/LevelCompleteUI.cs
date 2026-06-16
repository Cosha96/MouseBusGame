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
    [SerializeField] private TMP_Text gradeText;      // "● GREEN  87%"
    [SerializeField] private TMP_Text statsText;      // per-stat breakdown, toggled by stats button

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button passengersButton;

    [Header("Passengers Panel")]
    [SerializeField] private LevelCompletePassengerPanel passengerPanel;

    [Header("Animation")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("The 'Level Complete' title card that slides in from above")]
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private float fadeInDuration    = 0.5f;
    [SerializeField] private float titleSlideDuration = 0.35f;

    private bool _statsExpanded = false;

    private void Start()
    {
        continueButton?.onClick.AddListener(OnContinue);
        mainMenuButton?.onClick.AddListener(OnMainMenu);
        statsButton?.onClick.AddListener(OnStats);
        passengersButton?.onClick.AddListener(OnPassengers);

        // Stats breakdown starts hidden — button reveals it
        if (statsText != null) statsText.enabled = false;

        PopulateLevelInfo();
        PopulateScore();
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

    // ── Score Display ─────────────────────────────────────────────────────

    private void PopulateScore()
    {
        var result = ScoreTracker.LastResult;
        if (!result.isValid) return;

        int pct = Mathf.RoundToInt(result.percentage * 100f);

        if (gradeText != null)
        {
            string label = result.grade switch
            {
                ScoreGrade.Green  => "GREEN",
                ScoreGrade.Yellow => "YELLOW",
                ScoreGrade.Red    => "RED",
                _                 => ""
            };
            string hex = result.grade switch
            {
                ScoreGrade.Green  => "#4EAA68",
                ScoreGrade.Yellow => "#F5C842",
                ScoreGrade.Red    => "#E05252",
                _                 => "#FFFFFF"
            };
            gradeText.text = $"<color={hex}>● {label}</color>  {pct}%";
        }

        if (statsText != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var stat in result.breakdown)
            {
                int statPct = Mathf.RoundToInt(stat.ratio * 100f);
                int wPct    = Mathf.RoundToInt(stat.normalizedWeight * 100f);
                sb.AppendLine($"{stat.displayName}:  {(int)stat.earned}/{(int)stat.max}  ({statPct}%)   weight {wPct}%");
            }
            statsText.text = sb.ToString().TrimEnd();
        }

        if (statsButton != null) statsButton.interactable = true;
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

    private void OnPassengers()
    {
        passengerPanel?.Open(() => ShowMainButtons(true));
        ShowMainButtons(false);
    }

    private void ShowMainButtons(bool show = true)
    {
        continueButton?.gameObject.SetActive(show);
        mainMenuButton?.gameObject.SetActive(show);
        statsButton?.gameObject.SetActive(show);
        passengersButton?.gameObject.SetActive(show);
        if (!show && _statsExpanded && statsText != null) statsText.enabled = false;
    }

    private void OnStats()
    {
        if (statsText == null) return;
        _statsExpanded = !_statsExpanded;
        statsText.enabled = _statsExpanded;

        var label = statsButton?.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = _statsExpanded ? "Hide Stats" : "Stats";
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
