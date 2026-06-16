using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pause-menu sub-panel showing every passenger currently seated on the bus.
// Two screens: a list view and a detail view.
// Use Mousebus → Rebuild Pause Menu to stamp this into the scene automatically.
public class PassengerListPanel : MonoBehaviour
{
    [Header("List Screen")]
    [SerializeField] private GameObject listScreen;
    [SerializeField] private TMP_Text   headerText;
    [SerializeField] private Transform  entryContainer;
    [SerializeField] private Button     prevButton;
    [SerializeField] private Button     nextButton;
    [SerializeField] private TMP_Text   pageLabel;
    [SerializeField] private Button     listBackButton;

    [Header("Detail Screen")]
    [SerializeField] private GameObject detailScreen;
    [SerializeField] private TMP_Text   detailNameText;
    [SerializeField] private TMP_Text   detailAgeText;
    [SerializeField] private TMP_Text   detailJobText;
    [SerializeField] private TMP_Text   detailHobbiesText;
    [SerializeField] private TMP_Text   detailTimesText;
    [SerializeField] private Button     detailBackButton;

    private const int PageSize = 6;

    private Action                    _onBack;
    private int                       _currentPage;
    private readonly List<GameObject> _entryObjects = new();

    private void Awake()
    {
        gameObject.SetActive(false);

        listBackButton?.onClick.AddListener(OnListBack);
        prevButton?.onClick.AddListener(() => { _currentPage--; PopulateEntries(); });
        nextButton?.onClick.AddListener(() => { _currentPage++; PopulateEntries(); });
        detailBackButton?.onClick.AddListener(() =>
        {
            detailScreen.SetActive(false);
            listScreen.SetActive(true);
        });
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void Open(Action onBack)
    {
        _onBack      = onBack;
        _currentPage = 0;
        gameObject.SetActive(true);
        ShowListScreen();
    }

    public void Close() => gameObject.SetActive(false);

    // ── List Screen ───────────────────────────────────────────────────────

    private void ShowListScreen()
    {
        listScreen.SetActive(true);
        detailScreen.SetActive(false);
        PopulateEntries();
    }

    private void PopulateEntries()
    {
        foreach (var g in _entryObjects)
            if (g != null) Destroy(g);
        _entryObjects.Clear();

        var riders    = PassengerAgent.RidingPassengers;
        int total     = riders.Count;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)total / PageSize));
        _currentPage  = Mathf.Clamp(_currentPage, 0, pageCount - 1);

        if (headerText != null) headerText.text = $"ON BUS  ({total})";
        if (pageLabel  != null) pageLabel.text  = $"{_currentPage + 1} / {pageCount}";

        // Arrows always visible — greyed out at the ends
        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pageCount - 1;

        if (total == 0) { SpawnEmptyLabel(); return; }

        int start = _currentPage * PageSize;
        int end   = Mathf.Min(start + PageSize, total);
        for (int i = start; i < end; i++)
            _entryObjects.Add(SpawnEntry(riders[i].Data, i - start));
    }

    private void SpawnEmptyLabel()
    {
        var go  = new GameObject("Empty");
        go.transform.SetParent(entryContainer, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "No passengers on board yet.";
        tmp.fontSize  = 18f;
        tmp.color     = new Color(1f, 1f, 1f, 0.45f);
        tmp.alignment = TextAlignmentOptions.Center;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);
        _entryObjects.Add(go);
    }

    private GameObject SpawnEntry(PassengerData data, int index)
    {
        const float h = 52f;

        var go  = new GameObject($"Entry_{index}");
        go.transform.SetParent(entryContainer, false);

        // Alternating subtle row tint
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, index % 2 == 0 ? 0.04f : 0f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * h);
        rect.sizeDelta        = new Vector2(0f, h);

        // Name (left-aligned)
        AddLabel(go.transform, "Name",
            new Vector2(0f, 0f), new Vector2(0.62f, 1f),
            new Vector2(14f, 0f), Vector2.zero,
            data != null ? data.passengerName : "Unknown",
            18f, Color.white, TextAlignmentOptions.MidlineLeft);

        // Job (right-aligned, dimmer)
        AddLabel(go.transform, "Job",
            new Vector2(0.62f, 0f), Vector2.one,
            Vector2.zero, new Vector2(-14f, 0f),
            data != null ? data.job : "—",
            15f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineRight);

        // Click → detail
        var btn     = go.AddComponent<Button>();
        var colours = btn.colors;
        colours.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        colours.pressedColor     = new Color(1f, 1f, 1f, 0.06f);
        btn.colors = colours;

        var captured = data;
        btn.onClick.AddListener(() => ShowDetailScreen(captured));

        return go;
    }

    // ── Detail Screen ─────────────────────────────────────────────────────

    private void ShowDetailScreen(PassengerData data)
    {
        listScreen.SetActive(false);
        detailScreen.SetActive(true);

        bool has = data != null;
        SetDetailField(detailNameText,   "Name",         has ? data.passengerName          : "Unknown");
        SetDetailField(detailAgeText,    "Age",          has ? data.age.ToString()          : "—");
        SetDetailField(detailJobText,    "Job",          has ? data.job                     : "—");
        SetDetailField(detailHobbiesText,"Hobbies",      has ? data.hobbies                 : "—");
        SetDetailField(detailTimesText,  "Times Ridden", has ? data.timesRidden.ToString()  : "—");
    }

    private static void SetDetailField(TMP_Text label, string fieldName, string value)
    {
        if (label != null)
            label.text = $"<size=70%><color=#AAAAAA>{fieldName}</color></size>\n{value}";
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void OnListBack()
    {
        Close();
        _onBack?.Invoke();
    }

    private static void AddLabel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, float size, Color color, TextAlignmentOptions align)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
