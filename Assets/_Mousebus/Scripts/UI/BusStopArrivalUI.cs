using System.Collections;
using TMPro;
using UnityEngine;

// Attach to: the BusStopArrivalUI canvas in each level scene.
// Use Mousebus → Setup Level Scene UI to create the hierarchy automatically.
//
// Shows a small fading popup with the stop name each time the bus enters a stop trigger.
public class BusStopArrivalUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text    stopNameText;
    [SerializeField] private float       displayDuration = 2.5f;
    [SerializeField] private float       fadeDuration    = 0.3f;

    private Coroutine _showRoutine;

    private void OnEnable()  => BusStop.OnBusEnteredStop += Show;
    private void OnDisable() => BusStop.OnBusEnteredStop -= Show;

    private void Start()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void Show(string stopName)
    {
        if (_showRoutine != null) StopCoroutine(_showRoutine);
        _showRoutine = StartCoroutine(ShowRoutine(stopName));
    }

    private IEnumerator ShowRoutine(string name)
    {
        if (stopNameText != null)
            stopNameText.text = $"<size=55%><color=#AAAAAA>ARRIVED</color></size>\n{name.ToUpper()}";

        if (canvasGroup != null)
            yield return StartCoroutine(UIAnimator.Fade(canvasGroup, canvasGroup.alpha, 1f, fadeDuration));

        yield return new WaitForSeconds(displayDuration);

        if (canvasGroup != null)
            yield return StartCoroutine(UIAnimator.Fade(canvasGroup, 1f, 0f, fadeDuration));
    }
}
