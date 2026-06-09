using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Central authority for game state, scene navigation, and progression.
// Persists across all scene loads via DontDestroyOnLoad.
public class GameManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Game State ────────────────────────────────────────────────────────

    public enum GameState
    {
        MainMenu,   // on the main menu
        Loading,    // scene transition in progress
        Cutscene,   // a PNG cutscene is playing (System 2)
        Driving,    // player is driving the bus (System 3)
        Paused      // game paused — only reachable from Driving
    }

    public GameState CurrentState { get; private set; }

    // Other systems subscribe to react to state changes instead of polling.
    // e.g. PauseMenu shows itself, HUD dims, BusController disables input.
    public static event Action<GameState> OnStateChanged;

    // ── Level Registry ────────────────────────────────────────────────────

    // Ordered play sequence. These strings MUST exactly match your scene names.
    // Index 0 is Tutorial — always the entry point on a fresh save.
    private static readonly string[] LevelSceneNames =
    {
        "Level_Tutorial",       // 0
        "Level_01_June",        // 1
        "Level_02_July",        // 2
        "Level_03_August",      // 3
        "Level_04_September",   // 4
        "Level_05_October",     // 5
        "Level_06_November",    // 6
        "Level_07_December",    // 7
        "Level_08_January",     // 8
        "Level_09_February",    // 9
        "Level_10_March",       // 10
        "Level_11_April",       // 11
        "Level_12_May"          // 12 — completing this triggers the outro
    };

    // Outro auto-plays after Level_12; never appears in level select.
    private const string OutroSceneName        = "Level_Outro";
    // Level complete screen loads between levels (not for the final level → outro).
    private const string LevelCompleteSceneName = "LevelComplete";

    // ── Progression ───────────────────────────────────────────────────────

    // Highest index the player may load. Default 0 = Tutorial only.
    // Example: value of 3 means Tutorial, 01, 02, 03 are all accessible.
    private int _highestUnlockedIndex;
    private const string PrefsHighestLevel = "HighestUnlockedLevel";

    // Set in CompleteLevel(); read by LevelCompleteUI to display the level name.
    private int _lastCompletedLevelIndex = -1;
    public  int LastCompletedLevelIndex  => _lastCompletedLevelIndex;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // If a GameManager already exists (carried over from a previous scene),
        // destroy the new duplicate that just loaded with this scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // also persists all children (loading screen canvas)
        LoadProgress();
    }

    // ── State Management ──────────────────────────────────────────────────

    public void SetState(GameState newState)
    {
        CurrentState = newState;

        // Pausing freezes physics, animation, and any code using Time.deltaTime.
        // Code using Time.unscaledDeltaTime (UI fades, the loading screen) is unaffected.
        Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

        OnStateChanged?.Invoke(newState); // ?. means "only call if someone is listening"
    }

    // Called by InputManager (System 3). Cutscenes cannot be paused.
    public void TogglePause()
    {
        if (CurrentState == GameState.Driving)
            SetState(GameState.Paused);
        else if (CurrentState == GameState.Paused)
            SetState(GameState.Driving);
    }

    // ── Scene Navigation ──────────────────────────────────────────────────

    // "Continue" on main menu — jump straight to the furthest unlocked level.
    // Clamped so that completing the final level doesn't produce an out-of-range index.
    public void ContinueGame() => LoadLevelByIndex(Mathf.Min(_highestUnlockedIndex, LevelSceneNames.Length - 1));

    // Level select uses this. levelIndex maps into LevelSceneNames[].
    public void LoadLevelByIndex(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= LevelSceneNames.Length)
        {
            Debug.LogWarning($"[GameManager] Index {levelIndex} out of range.");
            return;
        }
        if (!IsLevelUnlocked(levelIndex))
        {
            Debug.LogWarning($"[GameManager] Level {levelIndex} not yet unlocked.");
            return;
        }

        SetState(GameState.Loading);
        SceneLoader.Instance.LoadSceneAsync(LevelSceneNames[levelIndex]);
    }

    public void LoadMainMenu()
    {
        SetState(GameState.Loading);
        SceneLoader.Instance.LoadSceneAsync("MainMenu");
    }

    // Called by LevelManager when the player finishes a level.
    public void CompleteLevel(int levelIndex)
    {
        // Store for LevelCompleteUI to display the level name without passing it through scenes.
        _lastCompletedLevelIndex = levelIndex;

        // Only advance the unlock pointer if this is genuinely new progress.
        // Replaying an old level doesn't overwrite a further unlock.
        if (levelIndex >= _highestUnlockedIndex)
        {
            _highestUnlockedIndex = levelIndex + 1;
            SaveProgress();
        }

        bool isFinalLevel = (levelIndex == LevelSceneNames.Length - 1);
        if (isFinalLevel)
        {
            // Final level goes straight to the outro — no level-complete screen.
            SetState(GameState.Loading);
            SceneLoader.Instance.LoadSceneAsync(OutroSceneName);
        }
        else
        {
            // Every other level lands on the Level Complete screen.
            // LevelCompleteUI reads LastCompletedLevelIndex for the level name.
            SetState(GameState.Loading);
            SceneLoader.Instance.LoadSceneAsync(LevelCompleteSceneName);
        }
    }

    // Convenience for LevelManager: "which level am I?" without hardcoding.
    public int GetCurrentLevelIndex()
    {
        string current = SceneManager.GetActiveScene().name;
        for (int i = 0; i < LevelSceneNames.Length; i++)
        {
            if (LevelSceneNames[i] == current) return i;
        }
        return -1; // MainMenu, Outro, or unknown
    }

    // ── Progression Queries (used by Level Select UI) ─────────────────────

    public bool IsLevelUnlocked(int levelIndex) => levelIndex <= _highestUnlockedIndex;
    public int LevelCount => LevelSceneNames.Length;
    public string GetLevelSceneName(int levelIndex) => LevelSceneNames[levelIndex];

    // ── Persistence ───────────────────────────────────────────────────────

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(PrefsHighestLevel, _highestUnlockedIndex);
        PlayerPrefs.Save(); // Explicit flush to disk — important before quit
    }

    private void LoadProgress()
    {
        _highestUnlockedIndex = PlayerPrefs.GetInt(PrefsHighestLevel, 0);
    }

    // Right-click the component header in the Inspector to call this during testing.
    [ContextMenu("DEBUG: Reset Progress")]
    private void DEBUG_ResetProgress()
    {
        PlayerPrefs.DeleteKey(PrefsHighestLevel);
        _highestUnlockedIndex = 0;
        Debug.Log("[GameManager] Progress reset — Tutorial only.");
    }

    // ── Application ───────────────────────────────────────────────────────

    public void QuitGame()
    {
        // Application.Quit() is silently ignored inside the Unity Editor.
        // The #if block compiles different code depending on the build target.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
