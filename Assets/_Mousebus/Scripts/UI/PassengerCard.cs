using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One passenger card. Created at runtime by PassengerInfoPanel.
// Builds its own child hierarchy, slides in from the left,
// and self-destructs after sliding back out.
public class PassengerCard : MonoBehaviour
{
    private RectTransform _rect;
    private float         _targetX;
    private float         _slideSpeed;
    private bool          _slidingOut;

    public void Initialize(PassengerData data, float startX, float targetX, float speed)
    {
        _rect       = GetComponent<RectTransform>();
        _targetX    = targetX;
        _slideSpeed = speed;

        var pos = _rect.anchoredPosition;
        pos.x = startX;
        _rect.anchoredPosition = pos;

        BuildChildren(data);
    }

    public void SlideOut(float exitX)
    {
        _targetX    = exitX;
        _slidingOut = true;
    }

    private void Update()
    {
        var pos = _rect.anchoredPosition;
        pos.x   = Mathf.Lerp(pos.x, _targetX, _slideSpeed * Time.deltaTime);
        _rect.anchoredPosition = pos;

        if (_slidingOut && Mathf.Abs(pos.x - _targetX) < 2f)
            Destroy(gameObject);
    }

    private void BuildChildren(PassengerData d)
    {
        // "PASSENGER" tab label
        MakeText("Header", "PASSENGER", 11f, new Color(0.5f, 0.75f, 1f),
            new Vector2(14f, -36f), new Vector2(-14f, -8f));

        // Thin divider line below header
        var divGO  = new GameObject("Divider");
        divGO.transform.SetParent(transform, false);
        divGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        var divRect = divGO.GetComponent<RectTransform>();
        divRect.anchorMin        = new Vector2(0f, 1f);
        divRect.anchorMax        = new Vector2(1f, 1f);
        divRect.pivot            = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -40f);
        divRect.sizeDelta        = new Vector2(-28f, 1f);

        // Stat rows
        float y = -48f;
        MakeField("Name",        "Name",         d != null ? d.passengerName          : "Unknown", ref y, 52f);
        MakeField("Age",         "Age",          d != null ? d.age.ToString()         : "—",       ref y, 52f);
        MakeField("Job",         "Job",          d != null ? d.job                    : "—",       ref y, 52f);
        MakeField("Hobbies",     "Hobbies",      d != null ? d.hobbies                : "—",       ref y, 62f);
        MakeField("TimesRidden", "Times Ridden", d != null ? d.timesRidden.ToString() : "—",       ref y, 52f);
    }

    private void MakeField(string goName, string label, string value, ref float y, float h)
    {
        MakeText(goName, $"<size=70%><color=#AAAAAA>{label}</color></size>\n{value}",
            17f, Color.white, new Vector2(14f, y - h), new Vector2(-14f, y));
        y -= h;
    }

    private void MakeText(string goName, string text, float fontSize, Color color,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;

        var rect       = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
