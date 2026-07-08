using UnityEngine;

// Attach to: the Bus GameObject
// Requires: a Rigidbody component (added automatically via RequireComponent)
// Setup: run Mousebus → Create Bus (Test Cube) and everything is configured for you
[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float maxForwardSpeed = 16f;  // ~58 km/h — nippy city feel
    [SerializeField] private float maxReverseSpeed = 5f;
    [SerializeField] private float acceleration    = 14f;  // units/s² — reaches speed in ~1s

    [Header("Braking")]
    [SerializeField] private float coastDeceleration = 12f;  // gentle rolldown when no input
    [SerializeField] private float activeBrakeForce  = 85f;  // S pressed while moving forward

    [Header("Steering")]
    [SerializeField] private float steeringSpeed       = 85f;   // degrees per second at full speed
    [Tooltip("Steering authority at a standstill (0 = no turning, 1 = full). " +
             "Higher = more car-like, lower = more bus-like.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSteerFraction    = 0.4f;  // 40% steering even from rest

    public static BusController Instance { get; private set; }

    private Rigidbody _rb;
    private float _currentSpeed;

    private void Awake()
    {
        Instance = this;
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
        // Lerp from minSteerFraction → 1 as speed increases.
        // This gives responsive steering from a standstill (car-like) while still
        // feeling more planted at high speed. Set minSteerFraction to 0 for
        // classic bus handling where you need momentum to turn.
        float speedFraction = Mathf.Lerp(minSteerFraction, 1f,
            Mathf.Clamp01(Mathf.Abs(_currentSpeed) / maxForwardSpeed));
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
        float targetSpeed;
        float rate;

        if (throttle > 0.01f)
        {
            // Accelerating forward
            targetSpeed = throttle * maxForwardSpeed;
            rate        = acceleration;
        }
        else if (throttle < -0.01f && _currentSpeed > 0.5f)
        {
            // S pressed while still rolling forward — active brake (punchy)
            targetSpeed = 0f;
            rate        = activeBrakeForce;
        }
        else if (throttle < -0.01f)
        {
            // S pressed from (near) rest — reverse
            targetSpeed = throttle * maxReverseSpeed;
            rate        = acceleration;
        }
        else
        {
            // No input — gentle coast to a stop
            targetSpeed = 0f;
            rate        = coastDeceleration;
        }

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

        Vector3 velocity = transform.forward * _currentSpeed;
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;
    }

    // ── Public API ────────────────────────────────────────────────────────

    // Exposed for HUD speedometer, level fail conditions, etc.
    public float CurrentSpeed    => _currentSpeed;
    public float MaxForwardSpeed => maxForwardSpeed;

    // Called by LevelManager at the midpoint to face the bus back toward the start.
    // Speed is zeroed so the bus starts from rest in the new direction.
    // This is safe to call while a cutscene is covering the screen — the snap is invisible.
    public void FlipForReturn()
    {
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, 180f, 0f));
        _currentSpeed = 0f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
