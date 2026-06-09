using System.Collections;
using UnityEngine;

// Static animation helpers for UI coroutines.
// These are IEnumerators, so callers own the coroutine lifetime:
//   StartCoroutine(UIAnimator.Fade(group, 0f, 1f, 0.3f))
// All animations use unscaledDeltaTime so they work while the game is paused.
public static class UIAnimator
{
    // ── Fade ──────────────────────────────────────────────────────────────

    // Fades a CanvasGroup's alpha from → to over duration seconds.
    public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        group.alpha = from;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        group.alpha = to;
    }

    // ── Slide Panel ───────────────────────────────────────────────────────

    // Slides a RectTransform's anchoredPosition from → to.
    // Uses SmoothStep easing: accelerates at start, decelerates at end.
    // Great for panels sliding in from off-screen.
    public static IEnumerator SlidePanel(RectTransform rect, Vector2 from, Vector2 to, float duration)
    {
        if (rect == null) yield break;

        rect.anchoredPosition = from;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t); // SmoothStep: 3t² - 2t³
            rect.anchoredPosition = Vector2.Lerp(from, to, smooth);
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    // ── Scale Punch ───────────────────────────────────────────────────────

    // Scales up to peakScale then back to original.
    // Use for button confirms, level unlocks, pickups, etc.
    public static IEnumerator Punch(Transform t, float peakScale = 1.15f, float duration = 0.25f)
    {
        if (t == null) yield break;

        Vector3 original = t.localScale;
        float   half     = duration * 0.5f;
        float   elapsed  = 0f;

        // Scale up
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            t.localScale = original * Mathf.Lerp(1f, peakScale, elapsed / half);
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            t.localScale = original * Mathf.Lerp(peakScale, 1f, elapsed / half);
            yield return null;
        }

        t.localScale = original;
    }
}
