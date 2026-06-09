using System;
using UnityEngine;

// Attach to: a trigger zone at each bus stop position in the level scene.
// Use Mousebus → Create Bus Stop to stamp one in automatically.
//
// The same stop fires on BOTH the outbound leg (DrivingToMidpoint) and the
// inbound leg (DrivingToEnd) — LevelManager decides whether to count each pass.
[RequireComponent(typeof(Collider))]
public class BusStop : MonoBehaviour
{
    [Tooltip("Human-readable label — shown in logs and future UI")]
    public string stopName = "Bus Stop";

    [Tooltip("How many passengers board when the bus arrives")]
    public int waitingPassengers = 5;

    // LevelManager subscribes to this to react when the bus arrives
    public static event Action<BusStop> OnBusArrived;

    // Each stop tracks whether it has been collected on each leg separately.
    // This prevents the bus reversing back through a stop and double-counting.
    private bool _collectedOutbound;
    private bool _collectedInbound;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Bus")) return;
        OnBusArrived?.Invoke(this);
    }

    // ── Called by LevelManager ────────────────────────────────────────────

    // Returns true (and marks collected) the first time on this leg.
    // Returns false on repeat calls — safe to call more than once.
    public bool TryCollectOutbound()
    {
        if (_collectedOutbound) return false;
        _collectedOutbound = true;
        return true;
    }

    public bool TryCollectInbound()
    {
        if (_collectedInbound) return false;
        _collectedInbound = true;
        return true;
    }

    // Call at level start in case the scene is restarted without reloading
    public void ResetStop()
    {
        _collectedOutbound = false;
        _collectedInbound = false;
    }

    // ── Editor Gizmo ──────────────────────────────────────────────────────

    // Draws a visible marker in the scene view so stops are easy to find and place
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
        Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (transform.localScale.y * 0.5f + 0.5f),
            $"{stopName} ({waitingPassengers}p)"
        );
#endif
    }
}
