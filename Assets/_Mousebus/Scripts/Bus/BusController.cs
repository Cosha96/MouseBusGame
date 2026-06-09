using UnityEngine;

// Attach to: the Bus GameObject
// Requires: a Rigidbody component (added automatically via RequireComponent)
// Setup: run Mousebus → Create Bus (Test Cube) and everything is configured for you
[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float maxForwardSpeed = 12f;  // ~43 km/h — city bus pace
    [SerializeField] private float maxReverseSpeed = 4f;   // buses reverse slowly
    [SerializeField] private float acceleration    = 5f;   // units/s² to reach max speed
    [SerializeField] private float deceleration    = 8f;   // units/s² to stop (braking is snappier than accelerating)

    [Header("Steering")]
    [SerializeField] private float steeringSpeed = 60f;    // degrees per second at full speed

    private Rigidbody _rb;
    private float _currentSpeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Freeze X and Z rotation so physics can't tip the bus over sideways or forward.
        // We leave Y free because our steering code controls it via MoveRotation.
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Interpolate smooths the bus's rendered position between physics steps.
        // Without this, the bus moves in discrete 50Hz jumps while the camera
        // renders at 60Hz+, causing the jitter you feel when driving.
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // FixedUpdate is where all physics changes must happen — never in Update.
    // Unity runs FixedUpdate on a fixed timestep (default 50Hz) separate from the frame rate.
    private void FixedUpdate()
    {
        // Steer first so the velocity is applied in the correct new forward direction
        HandleSteering(InputManager.Horizontal);
        HandleMovement(InputManager.Vertical);
    }

    private void HandleSteering(float steer)
    {
        // Scale turning by current speed fraction — no spinning in place.
        // At full speed you get full steering; at a crawl, very little.
        // This gives the bus the feel of needing momentum to turn.
        float speedFraction = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxForwardSpeed);
        float steerAmount   = steer * steeringSpeed * speedFraction;

        // Flip the steering direction when reversing so it feels natural,
        // the same way a real vehicle steers when backing up.
        if (_currentSpeed < 0f) steerAmount = -steerAmount;

        // MoveRotation is the physics-correct way to rotate a Rigidbody —
        // it plays nicely with the physics engine instead of fighting it.
        Quaternion delta = Quaternion.Euler(0f, steerAmount * Time.fixedDeltaTime, 0f);
        _rb.MoveRotation(_rb.rotation * delta);
    }

    private void HandleMovement(float throttle)
    {
        // Determine where we're trying to get to speed-wise
        float targetSpeed = throttle >= 0f
            ? throttle * maxForwardSpeed
            : throttle * maxReverseSpeed; // throttle is negative, so targetSpeed is negative

        // MoveTowards gives a linear ramp — higher deceleration makes braking
        // feel snappier than accelerating, which matches how a heavy vehicle behaves.
        float rate = Mathf.Abs(throttle) > 0.01f ? acceleration : deceleration;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

        // Only control horizontal movement — preserve the Y velocity that gravity produces.
        // Without this line, we'd zero out gravity every frame and the bus would float.
        Vector3 velocity = transform.forward * _currentSpeed;
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;
    }

    // Exposed for HUD speedometer, level fail conditions, etc.
    public float CurrentSpeed    => _currentSpeed;
    public float MaxForwardSpeed => maxForwardSpeed;
}
