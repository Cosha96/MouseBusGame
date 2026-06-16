using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Paginated passenger roll-call shown on the Level Complete screen.
// Reads PassengerAgent.RideLog (static, survives scene transition) so it
// always reflects everyone who boarded during the just-finished level.
//
// Use Mousebus → Setup Level Complete Scene to create the hierarchy.
public class LevelCompletePassengerPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text  headerText;
    [SerializeField] private Transform entryContainer;
    [SerializeField] private Button    prevButton;
    [SerializeField] private Button    nextButton;
    [SerializeField] private TMP_Text  pageLabel;
    [SerializeField] private Button    backButton;

    private const int EntriesPerPage = 6;

    private List<PassengerData> _passengers = new();
    private int _currentPage;
    private System.Action _onBack;

    private void Awake()
    {
        prevButton?.onClick.AddListener(OnPrev);
        nextButton?.onClick.AddListener(OnNext);
        backButton?.onClick.AddListener(OnBack);
        gameObject.SetActive(false);
    }

    public void Open(System.Action onBack)
    {
        _onBack     = onBack;
        _passengers = new List<PassengerData>(PassengerAgent.RideLog);
        _currentPage = 0;

        if (headerText != null)
            headerText.text = $"ON BOARD TODAY  ({_passengers.Count})";

        gameObject.SetActive(true);
        Populate();
    }

    public void Close() => gameObject.SetActive(false);

    private void Populate()
    {
        foreach (Transform child in entryContainer)
            Destroy(child.gameObject);

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)_passengers.Count / EntriesPerPage));
        int start     = _currentPage * EntriesPerPage;
        int end       = Mathf.Min(start + EntriesPerPage, _passengers.Count);

        for (int i = start; i < end; i++)
        {
            var p = _passengers[i];
            if (p == null) continue;

            var go  = new GameObject($"Entry_{i}");
            go.transform.SetParent(entryContainer, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = string.IsNullOrEmpty(p.job)
                ? p.passengerName
                : $"{p.passengerName}  <size=75%><color=#AAAAAA>{p.job}</color></size>";
            tmp.fontSize = 18f;
            tmp.color    = Color.white;
            tmp.alignment            = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping   = false;

            var rect = go.GetComponent<RectTransform>();
            const float h = 48f;
            float y = -(i - start) * h;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(16f, y - h);
            rect.offsetMax = new Vector2(-16f, y);
        }

        if (pageLabel != null) pageLabel.text = $"{_currentPage + 1} / {pageCount}";
        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pageCount - 1;
    }

    private void OnPrev() { if (_currentPage > 0) { _currentPage--; Populate(); } }
    private void OnNext() { _currentPage++; Populate(); }
    private void OnBack() { Close(); _onBack?.Invoke(); }
}
