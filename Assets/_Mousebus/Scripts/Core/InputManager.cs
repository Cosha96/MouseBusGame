using UnityEngine;
using UnityEngine.InputSystem;

// Static input wrapper using Unity's New Input System.
// All bindings live in the static constructor below — change them here to remap keys.
// For a full remapping UI later, swap in an InputActionAsset — the API stays the same.
public static class InputManager
{
    // ── Actions ───────────────────────────────────────────────────────────
    private static readonly InputAction MoveAction;
    private static readonly InputAction PauseAction;

    // Static constructors run once the first time the class is accessed.
    // We create and enable the actions here so they're ready before any Update runs.
    static InputManager()
    {
        // Move — WASD keys composite + gamepad left stick
        // "2DVector" composite maps four buttons to a Vector2 (e.g. W=up, S=down, A=left, D=right)
        MoveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        MoveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Down",  "<Keyboard>/s")
            .With("Left",  "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        MoveAction.AddBinding("<Gamepad>/leftStick");
        MoveAction.Enable();

        // Pause — Escape key and gamepad Start button
        PauseAction = new InputAction("Pause", InputActionType.Button);
        PauseAction.AddBinding("<Keyboard>/escape");
        PauseAction.AddBinding("<Gamepad>/start");
        PauseAction.Enable();
    }

    // ── Public API ────────────────────────────────────────────────────────

    // Returns 0 when not driving so BusController doesn't need to check state itself
    public static float Horizontal => IsDriving ? MoveAction.ReadValue<Vector2>().x : 0f;
    public static float Vertical   => IsDriving ? MoveAction.ReadValue<Vector2>().y : 0f;

    // .triggered is true for exactly one frame when the button is pressed
    public static bool PausePressed => PauseAction.triggered;

    // Used by CutscenePlayer to detect any skip input
    public static bool AnySkipPressed =>
        (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
        (Gamepad.current  != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                      Gamepad.current.startButton.wasPressedThisFrame));

    // ── State Guard ───────────────────────────────────────────────────────

    // Returns true when driving, OR when there's no GameManager (isolated scene testing)
    private static bool IsDriving =>
        GameManager.Instance == null ||
        GameManager.Instance.CurrentState == GameManager.GameState.Driving;
}
