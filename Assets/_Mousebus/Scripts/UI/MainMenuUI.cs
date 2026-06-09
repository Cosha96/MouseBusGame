using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attach to: the MainMenuUI root in the MainMenu scene.
// Wires buttons, manages the level-select slide panel, and fades in after the splash.
//
// Requires in the scene: SplashSequence (to receive OnSplashComplete event).
// Use Mousebus → Setup Main Menu to auto-create the hierarchy.
public class MainMenuUI : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private CanvasGroup mainPanelGroup;
    [SerializeField] private Button      newGameButton;
    [SerializeField] private Button      continueButton;
    [SerializeField] private Button      levelSelectButton;
    [SerializeField] private Button      quitButton;

    [Header("Level Select Panel")]
    [Tooltip("The slide panel RectTransform — positioned off-screen to the right at rest")]
    [SerializeField] private RectTransform levelSelectPanel;
    [SerializeField] private Button        levelSelectCloseButton;
    [Tooltip("Parent container for the generated level buttons")]
    [SerializeField] private Transform     levelButtonContainer;
    [Tooltip("Button prefab with a TMP_Text child for the label")]
    [SerializeField] private Button        levelButtonPrefab;
    [SerializeField] private float         slideInDuration = 0.35f;

    // Positions computed at runtime so they scale with any screen size
    private Vector2 _panelShownPos;
    private Vector2 _panelHiddenPos;
    private bool    _levelSelectOpen;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()  => SplashSequence.OnSplashComplete += HandleSplashComplete;
    private void OnDisable() => SplashSequence.OnSplashComplete -= HandleSplashComplete;

    private void Start()
    {
        // Record the panel's Inspector-set resting position, then park it off-screen right
        if (levelSelectPanel != null)
        {
            _panelShownPos  = levelSelectPanel.anchoredPosition;
            _panelHiddenPos = _panelShownPos + new Vector2(Screen.width, 0f);
            levelSelectPanel.anchoredPosition = _panelHiddenPos;
        }

        // Wire main buttons
        newGameButton?.onClick.AddListener(OnNewGame);
        continueButton?.onClick.AddListener(OnContinue);
        levelSelectButton?.onClick.AddListener(OnLevelSelectOpen);
        quitButton?.onClick.AddListener(OnQuit);
        levelSelectCloseButton?.onClick.AddListener(OnLevelSelectClose);

        // Start invisible — HandleSplashComplete fades it in
        if (mainPanelGroup != null)
        {
            mainPanelGroup.alpha          = 0f;
            mainPanelGroup.interactable   = false;
            mainPanelGroup.blocksRaycasts = false;
        }

        // Continue is only useful if the player has made some progress
        RefreshContinueButton();

        // Populate level select buttons up front
        PopulateLevelSelect();
    }

    // ── Splash callback ───────────────────────────────────────────────────

    private void HandleSplashComplete()
    {
        if (mainPanelGroup == null) return;

        mainPanelGroup.interactable   = true;
        mainPanelGroup.blocksRaycasts = true;
        StartCoroutine(UIAnimator.Fade(mainPanelGroup, 0f, 1f, 0.4f));
    }

    // ── Button Handlers ───────────────────────────────────────────────────

    private void OnNewGame()
    {
        // Loads the Tutorial (index 0). Add a "restart save?" dialog here later.
        GameManager.Instance?.LoadLevelByIndex(0);
    }

    private void OnContinue()
    {
        GameManager.Instance?.ContinueGame();
    }

    private void OnLevelSelectOpen()
    {
        if (_levelSelectOpen || levelSelectPanel == null) return;
        _levelSelectOpen = true;
        StartCoroutine(UIAnimator.SlidePanel(levelSelectPanel, _panelHiddenPos, _panelShownPos, slideInDuration));
    }

    private void OnLevelSelectClose()
    {
        if (!_levelSelectOpen || levelSelectPanel == null) return;
        _levelSelectOpen = false;
        StartCoroutine(UIAnimator.SlidePanel(levelSelectPanel, _panelShownPos, _panelHiddenPos, slideInDuration));
    }

    private void OnQuit() => GameManager.Instance?.QuitGame();

    // ── Level Select ──────────────────────────────────────────────────────

    private void PopulateLevelSelect()
    {
        if (levelButtonContainer == null || levelButtonPrefab == null) return;
        if (GameManager.Instance == null) return;

        // Clear any old buttons first (safe to call more than once)
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < GameManager.Instance.LevelCount; i++)
        {
            int  capturedIndex = i; // closure capture — must be local variable
            bool unlocked      = GameManager.Instance.IsLevelUnlocked(i);

            Button btn   = Instantiate(levelButtonPrefab, levelButtonContainer);
            btn.interactable = unlocked;

            // Label the button with a human-readable name
            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = FormatLevelName(GameManager.Instance.GetLevelSceneName(i));

            if (unlocked)
                btn.onClick.AddListener(() => GameManager.Instance.LoadLevelByIndex(capturedIndex));
        }
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null || GameManager.Instance == null) return;
        // Tutorial is always accessible, so Continue is always valid
        continueButton.interactable = true;
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
