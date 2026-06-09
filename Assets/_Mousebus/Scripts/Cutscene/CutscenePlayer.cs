using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Attach to: the CutscenePlayer prefab (Mousebus → Create Cutscene Prefab)
// Called by: LevelManager.cs (System 4) via Play(CutsceneData)
// To preview in the editor: assign a CutsceneData to the Test Cutscene field
public class CutscenePlayer : MonoBehaviour
{
    // ── UI References ─────────────────────────────────────────────────────
    // These are wired automatically when you run Mousebus → Create Cutscene Prefab
    [SerializeField] private Image slideImage;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup canvasGroup;

    // ── Testing ───────────────────────────────────────────────────────────
    // Assign any CutsceneData here to preview it in Play mode without needing LevelManager.
    // LevelManager will call Play() directly in System 4 — this field is just for testing.
    [SerializeField] private CutsceneData testCutscene;

    // ── Events ────────────────────────────────────────────────────────────
    // LevelManager subscribes to this to know when to start gameplay
    public static event Action OnCutsceneComplete;

    private bool _skipRequested;
    private AudioSource _audioSource;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        subtitleText.text = "";

        // Only auto-play the test cutscene if there's no LevelManager in the scene.
        // When LevelManager is present it owns playback — the test field is just for
        // previewing cutscenes in isolation without the full level setup.
        bool levelManagerPresent = UnityEngine.Object.FindFirstObjectByType<LevelManager>() != null;
        if (testCutscene != null && !levelManagerPresent)
            Play(testCutscene);
    }

    private void Update()
    {
        bool inCutscene = GameManager.Instance == null ||
                          GameManager.Instance.CurrentState == GameManager.GameState.Cutscene;

        if (inCutscene && InputManager.AnySkipPressed)
            _skipRequested = true;
    }

    // ── Public API ────────────────────────────────────────────────────────

    // Called by LevelManager at the start and end of each level
    public void Play(CutsceneData data)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        _skipRequested = false;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameManager.GameState.Cutscene);

        if (data.music != null && _audioSource != null)
        {
            _audioSource.clip = data.music;
            _audioSource.volume = data.musicVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        StartCoroutine(PlayRoutine(data));
    }

    // ── Playback ──────────────────────────────────────────────────────────

    private IEnumerator PlayRoutine(CutsceneData data)
    {
        subtitleText.text = "";

        foreach (CutsceneSlide slide in data.slides)
        {
            if (_skipRequested) break;
            yield return StartCoroutine(PlaySlide(slide));
        }

        Complete();
    }

    private IEnumerator PlaySlide(CutsceneSlide slide)
    {
        slideImage.sprite = slide.image;
        subtitleText.text = "";

        float elapsed = 0f;
        int nextSubtitle = 0;

        while (elapsed < slide.duration && !_skipRequested)
        {
            // Show the next subtitle(s) when their timestamp is reached.
            // Using while instead of if handles edge cases where elapsed
            // jumps past multiple timestamps in one frame.
            while (nextSubtitle < slide.subtitles.Length &&
                   elapsed >= slide.subtitles[nextSubtitle].showAtTime)
            {
                subtitleText.text = slide.subtitles[nextSubtitle].text;
                nextSubtitle++;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        subtitleText.text = "";
    }

    private void Complete()
    {
        subtitleText.text = "";
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        if (_audioSource != null && _audioSource.isPlaying)
            StartCoroutine(FadeOutMusic(_skipRequested ? 0.15f : 0.5f));

        // Notify subscribers (LevelManager listens here to start driving)
        OnCutsceneComplete?.Invoke();
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        _audioSource.Stop();
        _audioSource.volume = startVolume; // reset so next Play() starts at full volume
    }
}
