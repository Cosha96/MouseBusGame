using TMPro;
using UnityEngine;

// Attach to: a child GameObject on the Bus, positioned above it.
// Displays the live passenger count (e.g. "12/30") in world space above the bus.
// Use Mousebus → Add Floating Passenger Label to set this up automatically.
[RequireComponent(typeof(TextMeshPro))]
public class FloatingPassengerCount : MonoBehaviour
{
    private TextMeshPro _tmp;

    private void Awake()
    {
        _tmp      = GetComponent<TextMeshPro>();
        _tmp.text = $"0/{LevelManager.BusCapacity}";
    }

    private void OnEnable()
    {
        PassengerAgent.OnRidingCountChanged += UpdateDisplay;
        GameManager.OnStateChanged          += HandleStateChanged;
    }

    private void OnDisable()
    {
        PassengerAgent.OnRidingCountChanged -= UpdateDisplay;
        GameManager.OnStateChanged          -= HandleStateChanged;
    }

    private void Start()
    {
        // Sync visibility with whatever state we're already in when the scene loads.
        // Without this, the label stays visible during the intro cutscene because
        // it only reacts to future state changes, not the current one.
        if (GameManager.Instance != null)
            HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void LateUpdate()
    {
        // Billboard: always face the same direction as the camera so the text
        // is readable from any angle without flipping or distorting.
        if (Camera.main != null)
            transform.forward = Camera.main.transform.forward;
    }

    private void UpdateDisplay(int riding)
    {
        _tmp.text = $"{riding}/{LevelManager.BusCapacity}";
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        // Only show while the player can see the world (hide during cutscenes/menus).
        // We toggle the TMP component instead of SetActive() — deactivating the GameObject
        // would trigger OnDisable() and unsubscribe our events, so we'd never hear
        // the Driving state change that brings the label back.
        bool show = state == GameManager.GameState.Driving ||
                    state == GameManager.GameState.Paused;
        _tmp.enabled = show;
    }
}
