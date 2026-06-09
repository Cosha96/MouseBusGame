using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to: SplashSequence GameObject in the MainMenu scene.
// Drives the opening sequence: black screen → Studio logo → Publisher logo → fade to menu.
//
// Setup:
//   1. Create a full-screen black Image with a CanvasGroup (the "SplashOverlay").
//   2. For each logo: create a UI Image, add a CanvasGroup, assign to Logos array.
//   3. Assign the overlay CanvasGroup to Splash Overlay.
//   4. MainMenuUI subscribes to OnSplashComplete to animate the menu in.
public class SplashSequence : MonoBehaviour
{
    [Serializable]
    public struct SplashLogo
    {
        [Tooltip("CanvasGroup on the logo Image")]
        public CanvasGroup group;
        [Tooltip("Seconds to fade in and out")]
        public float fadeDuration;
        [Tooltip("Seconds to hold at full opacity")]
        public float holdDuration;
    }

    [Header("Logos (shown in order)")]
    [SerializeField] private SplashLogo[] logos;

    [Header("Overlay")]
    [Tooltip("Full-screen black CanvasGroup that hides the menu until the splash finishes")]
    [SerializeField] private CanvasGroup splashOverlay;
    [SerializeField] private float revealDuration = 0.6f;

    // MainMenuUI subscribes to this to fade the main panel in after the splash
    public static event Action OnSplashComplete;

    // Stays true for the entire play session once the splash has run once.
    // Static so it survives scene reloads — resets automatically on game restart.
    private static bool _hasPlayedThisSession;

    private bool _skipRequested;

    private void Start()
    {
        // If we've already shown the splash this session (e.g. returning from a level),
        // skip straight to revealing the menu — no logos, no delay.
        if (_hasPlayedThisSession)
        {
            if (splashOverlay != null)
            {
                splashOverlay.alpha          = 0f;
                splashOverlay.blocksRaycasts = false;
            }
            OnSplashComplete?.Invoke();
            return;
        }

        // Start fully black with logos hidden
        if (splashOverlay != null) splashOverlay.alpha = 1f;
        foreach (var logo in logos)
            if (logo.group != null) logo.group.alpha = 0f;

        StartCoroutine(RunSplash());
    }

    private void Update()
    {
        // Any keyboard key or the south gamepad button skips the whole splash
        bool keyboardSkip = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool padSkip      = Gamepad.current  != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (keyboardSkip || padSkip)
            _skipRequested = true;
    }

    private IEnumerator RunSplash()
    {
        yield return new WaitForSecondsRealtime(0.4f); // brief black before first logo

        foreach (var logo in logos)
        {
            if (_skipRequested) break;
            if (logo.group == null) continue;

            // Fade in
            yield return StartCoroutine(UIAnimator.Fade(logo.group, 0f, 1f, logo.fadeDuration));

            // Hold
            float held = 0f;
            while (held < logo.holdDuration && !_skipRequested)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_skipRequested) break;

            // Fade out
            yield return StartCoroutine(UIAnimator.Fade(logo.group, 1f, 0f, logo.fadeDuration));
        }

        // Hide any logos that were left visible by the skip
        foreach (var logo in logos)
            if (logo.group != null) logo.group.alpha = 0f;

        // Fade out the black overlay → main menu is now visible
        yield return StartCoroutine(UIAnimator.Fade(splashOverlay, 1f, 0f, revealDuration));

        if (splashOverlay != null)
            splashOverlay.blocksRaycasts = false;

        _hasPlayedThisSession = true;
        OnSplashComplete?.Invoke();
    }
}
