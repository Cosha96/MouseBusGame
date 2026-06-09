using UnityEngine;

// Attach to: the Main Camera in each level scene
// Set the Target field to the Bus GameObject
// Run Mousebus → Create Bus (Test Cube) and this is set up automatically
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position")]
    [Tooltip("Camera position relative to the bus. Z is behind (negative), Y is above.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -10f);
    [Tooltip("How quickly the camera closes the gap to its target position. Lower = snappier.")]
    [SerializeField] private float positionSmoothTime = 0.2f;

    [Header("Turn Smoothing")]
    [Tooltip("How slowly the camera swings behind the bus when it turns. " +
             "Higher = less sway, more cinematic lag. Try 0.1 (tight) to 0.5 (floaty).")]
    [SerializeField] private float yawSmoothTime = 0.25f;

    [Header("Rotation")]
    [Tooltip("How quickly the camera rotates to look at the bus. Lower = more lag.")]
    [SerializeField] private float rotationSmoothSpeed = 5f;

    // Internal SmoothDamp state — Unity updates these references each frame
    private Vector3 _positionVelocity = Vector3.zero;
    private float   _smoothedYaw;
    private float   _yawVelocity;

    private void Start()
    {
        // Initialise to the bus's current angle so the camera doesn't
        // sweep in from the wrong direction on the first frame
        if (target != null)
            _smoothedYaw = target.eulerAngles.y;
    }

    // LateUpdate runs after ALL other Update calls have finished for the frame.
    // Using Update instead would cause a one-frame jitter because the camera
    // might move before the bus does on any given frame.
    private void LateUpdate()
    {
        if (target == null) return;

        FollowPosition();
        FollowRotation();
    }

    private void FollowPosition()
    {
        // The old approach used TransformPoint(offset), which converts the offset
        // using the bus's CURRENT rotation. That means every time the bus turns,
        // the camera's target position immediately arcs to the new angle and
        // SmoothDamp chases it — producing the sway you felt.
        //
        // Fix: smooth the yaw angle SEPARATELY before applying the offset.
        // The camera now drifts to face the new direction instead of swinging hard.
        _smoothedYaw = Mathf.SmoothDampAngle(
            _smoothedYaw,
            target.eulerAngles.y,
            ref _yawVelocity,
            yawSmoothTime
        );

        Quaternion smoothedRotation = Quaternion.Euler(0f, _smoothedYaw, 0f);
        Vector3 targetPosition = target.position + smoothedRotation * offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _positionVelocity,
            positionSmoothTime
        );
    }

    private void FollowRotation()
    {
        Vector3 directionToBus = target.position - transform.position;
        if (directionToBus == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToBus);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    // LevelManager can call this at runtime to assign the bus after it spawns
    public void SetTarget(Transform newTarget) => target = newTarget;
}
