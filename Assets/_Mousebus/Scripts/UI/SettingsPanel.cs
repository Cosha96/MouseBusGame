using System;
using UnityEngine;
using UnityEngine.UI;

// Pause-menu settings sub-panel: master volume, music volume, fullscreen.
// All values persist via PlayerPrefs and are applied immediately on change.
// Use Mousebus → Rebuild Pause Menu to stamp this into the scene.
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button backButton;

    internal const string MasterVolKey = "MasterVolume";
    internal const string MusicVolKey  = "MusicVolume";
    internal const string SfxVolKey    = "SfxVolume";

    private Action _onBack;

    private void Awake()
    {
        // Apply saved master volume immediately, before the panel is ever opened
        AudioListener.volume = PlayerPrefs.GetFloat(MasterVolKey, 1f);

        masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
        fullscreenToggle?.onValueChanged.AddListener(OnFullscreenChanged);
        backButton?.onClick.AddListener(() => { Close(); _onBack?.Invoke(); });

        gameObject.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Open(Action onBack)
    {
        _onBack = onBack;
        gameObject.SetActive(true);

        masterVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterVolKey, 1f));
        musicVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicVolKey,   1f));
        sfxVolumeSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat(SfxVolKey,       1f));
        fullscreenToggle?.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    public void Close() => gameObject.SetActive(false);

    // ── Callbacks ──────────────────────────────────────────────────────────

    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterVolKey, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolKey, value);

        // Update whichever music source is currently active
        FindFirstObjectByType<LevelManager>()?.SetMusicVolume(value);
        FindFirstObjectByType<MainMenuUI>()?.SetMusicVolume(value);

        // Update cutscene music if one is currently playing
        var cutscene = FindFirstObjectByType<CutscenePlayer>();
        if (cutscene != null)
        {
            var audio = cutscene.GetComponent<AudioSource>();
            if (audio != null) audio.volume = value;
        }
    }

    private static void OnSfxVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(SfxVolKey, value);
        FindFirstObjectByType<LevelManager>()?.SetSfxVolume(value);
    }

    private static void OnFullscreenChanged(bool value) => Screen.fullScreen = value;
}
