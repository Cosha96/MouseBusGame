using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a brief fading name badge on the right side of the HUD whenever a
// passenger steps off the bus. Multiple badges stack upward.
// Use Mousebus → Add Alighting Notice Panel to stamp this into the level scene.
public class AlightingNoticePanel : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("X position of each badge (positive = right of screen centre)")]
    [SerializeField] private float anchorX      = 380f;
    [Tooltip("Y of the first (lowest) badge slot above screen centre")]
    [SerializeField] private float baseY        = -80f;
    [Tooltip("How much each new badge shifts upward relative to the previous")]
    [SerializeField] private float stackStep    =  48f;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 2.0f;
    [SerializeField] private float fadeInTime   = 0.15f;
    [SerializeField] private float fadeOutTime  = 0.5f;

    private int _activeCount;

    private void OnEnable()  => PassengerAgent.OnPassengerAlighting += ShowNotice;
    private void OnDisable() => PassengerAgent.OnPassengerAlighting -= ShowNotice;

    private void ShowNotice(PassengerData data)
    {
        if (CutscenePlayer.IsPlaying) return;

        string name = data != null && !string.IsNullOrEmpty(data.passengerName)
            ? data.passengerName
            : "Passenger";

        // ── Badge background ──────────────────────────────────────────────
        var badgeGO = new GameObject("AlightNotice");
        badgeGO.transform.SetParent(transform, false);

        var bg   = badgeGO.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.10f, 0f);  // start transparent

        var rect = badgeGO.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(200f, 36f);
        rect.anchoredPosition = new Vector2(anchorX, baseY + _activeCount * stackStep);

        // ── Label ─────────────────────────────────────────────────────────
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(badgeGO.transform, false);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = $"↓  {name}";
        tmp.fontSize  = 15f;
        tmp.color     = new Color(1f, 1f, 1f, 0f);  // start transparent
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;

        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        _activeCount++;
        StartCoroutine(AnimateNotice(badgeGO, bg, tmp, () => _activeCount--));
    }

    private IEnumerator AnimateNotice(GameObject go, Image bg, TextMeshProUGUI tmp, System.Action onDone)
    {
        // Fade in
        yield return Fade(bg, tmp, 0f, 1f, fadeInTime);

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        yield return Fade(bg, tmp, 1f, 0f, fadeOutTime);

        onDone?.Invoke();
        Destroy(go);
    }

    private static IEnumerator Fade(Image bg, TextMeshProUGUI tmp, float from, float to, float duration)
    {
        float t = 0f;
        var bgTarget  = new Color(0.06f, 0.06f, 0.10f, to * 0.88f);
        var bgStart   = new Color(0.06f, 0.06f, 0.10f, from * 0.88f);
        var txtStart  = new Color(1f, 1f, 1f, from);
        var txtTarget = new Color(1f, 1f, 1f, to);

        while (t < duration)
        {
            t += Time.deltaTime;
            float p   = Mathf.Clamp01(t / duration);
            bg.color  = Color.Lerp(bgStart,  bgTarget,  p);
            tmp.color = Color.Lerp(txtStart, txtTarget, p);
            yield return null;
        }
        bg.color  = bgTarget;
        tmp.color = txtTarget;
    }
}
