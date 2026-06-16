using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Manages the stacked passenger card UI.
// Each boarding passenger spawns their own PassengerCard child; cards stack
// with a small downward offset so previous cards peek out from behind the newest.
// Use Mousebus → Setup Passenger Info Panel to create this in the scene.
public class PassengerInfoPanel : MonoBehaviour
{
    [Header("Slide Animation")]
    [SerializeField] private float slideInX   = -380f;  // X when visible (centre-anchored canvas)
    [SerializeField] private float slideOutX  = -900f;  // X when off-screen left
    [SerializeField] private float slideSpeed =  10f;

    [Header("Card Stack")]
    [SerializeField] private float baseY       =  120f;  // Y of first card above screen centre
    [SerializeField] private float stackOffset =  -20f;  // each subsequent card shifts down by this

    private readonly List<PassengerCard> _cards = new();
    private int _cardCount;

    private void OnEnable()
    {
        PassengerAgent.OnPassengerHighlighted += SpawnCard;
        PassengerAgent.OnPanelCleared         += ClearCards;
    }

    private void OnDisable()
    {
        PassengerAgent.OnPassengerHighlighted -= SpawnCard;
        PassengerAgent.OnPanelCleared         -= ClearCards;
    }

    private void SpawnCard(PassengerData data)
    {
        var cardGO = new GameObject($"Card_{_cardCount}");
        cardGO.transform.SetParent(transform, false);

        // Background — adding Image first creates the RectTransform
        cardGO.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.88f);

        var rect = cardGO.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(280f, 340f);
        // Each new card is stackOffset lower; starts off-screen on X
        rect.anchoredPosition = new Vector2(slideOutX, baseY + _cardCount * stackOffset);

        // Render on top of all previous cards
        cardGO.transform.SetAsLastSibling();

        var card = cardGO.AddComponent<PassengerCard>();
        card.Initialize(data, slideOutX, slideInX, slideSpeed);

        _cards.Add(card);
        _cardCount++;
    }

    private void ClearCards()
    {
        foreach (var c in _cards)
            if (c != null) c.SlideOut(slideOutX);
        _cards.Clear();
        _cardCount = 0;
    }
}
