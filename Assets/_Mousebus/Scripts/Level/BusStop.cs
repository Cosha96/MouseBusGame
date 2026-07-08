using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to: a trigger zone at each bus stop position in the level scene.
// Use Mousebus → Create Bus Stop to stamp one in automatically.
//
// Stop-Board-Go loop:
//   1. Bus enters trigger — nothing happens yet
//   2. Player stops the bus (speed < stoppedThreshold)
//   3. Player presses E / gamepad Y ("Open Doors")
//   4. Passengers board one at a time through the bus door
//   5. Player drives away (bus exits trigger)
//   Same stop fires again on the inbound leg — passengers alight on door press
[RequireComponent(typeof(Collider))]
public class BusStop : MonoBehaviour
{
    [Tooltip("Human-readable label — shown in logs and future UI")]
    public string stopName = "Bus Stop";

    [Tooltip("How many passengers board on the outbound leg")]
    public int waitingPassengers = 5;

    [Tooltip("How many passengers board on the return leg (0 = no return boarding at this stop)")]
    public int waitingReturnPassengers = 0;

    [Tooltip("Global roster to draw passengers from. " +
             "Assign the shared Passengers_Global asset — each stop picks randomly from it.")]
    [SerializeField] private PassengerRoster passengerRoster;

    [Tooltip("Bus must be below this speed (m/s) before doors can be opened")]
    [SerializeField] private float stoppedThreshold = 0.5f;

    [Tooltip("Distance (m) at which the alighting bell rings on the inbound approach")]
    [SerializeField] private float bellRingDistance = 20f;

    // LevelManager subscribes to this to react when doors open
    public static event Action<BusStop> OnBusArrived;

    // BusStopArrivalUI subscribes to this to show the stop name popup
    public static event Action<string> OnBusEnteredStop;

    // LevelManager subscribes to play the bell chime.
    // Fires once per inbound approach when at least one rider is destined for this stop.
    public static event Action OnAlightingStopApproached;

    // HUD reads this to find the nearest upcoming stop
    private static readonly List<BusStop> _activeStops = new();
    public static IReadOnlyList<BusStop> ActiveStops => _activeStops;

    public bool IsFullyCollected => _collectedOutbound && _collectedInbound;

    private bool _collectedOutbound;
    private bool _collectedInbound;
    private bool _bellRungThisApproach;

    // Set in OnTriggerEnter, cleared in OnTriggerExit
    private bool            _busNearby;
    private Transform       _busTransform;
    private BusController   _busController;

    // Prevents the doors firing more than once per trigger visit
    private bool _doorsOpenedThisVisit;

    private readonly List<PassengerAgent>  _agents        = new();  // outbound boarders
    private readonly List<PassengerAgent>  _returnAgents  = new();  // return-leg boarders
    private readonly Queue<PassengerAgent> _boardingQueue = new();
    private readonly Queue<PassengerAgent> _alightQueue   = new();

    static readonly Color[] _colours =
    {
        new Color(1.00f, 0.72f, 0.42f),  // peach
        new Color(0.95f, 0.52f, 0.35f),  // coral
        new Color(0.42f, 0.78f, 0.65f),  // teal
        new Color(0.78f, 0.65f, 0.95f),  // lavender
    };

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _activeStops.Add(this);
    }

    private void OnDestroy() => _activeStops.Remove(this);

    private void Update()
    {
        // Pre-arrival bell: rings when riders destined here are on board, regardless of leg.
        // Outbound approach: catches passengers from an earlier stop heading here.
        // Inbound approach: standard alighting warning.
        if (!_bellRungThisApproach && !IsFullyCollected && BusController.Instance != null)
        {
            float dist = Vector3.Distance(BusController.Instance.transform.position, transform.position);
            if (dist <= bellRingDistance)
            {
                foreach (var agent in PassengerAgent.RidingPassengers)
                {
                    if (agent.DestinationStop == this)
                    {
                        _bellRungThisApproach = true;
                        OnAlightingStopApproached?.Invoke();
                        break;
                    }
                }
            }
        }

        // Gate: bus must be in trigger, doors not yet opened this visit,
        // and at least one leg still uncollected
        if (!_busNearby || _doorsOpenedThisVisit) return;
        if (_collectedOutbound && _collectedInbound) return;

        // Gate: bus must be (nearly) stopped
        if (_busController != null && Mathf.Abs(_busController.CurrentSpeed) > stoppedThreshold)
            return;

        // Gate: player must press Open Doors
        if (!InputManager.OpenDoorsPressed) return;

        _doorsOpenedThisVisit = true;
        OnBusArrived?.Invoke(this);  // LevelManager calls TryCollectOutbound/Inbound in response
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Bus")) return;

        _busTransform  = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;
        _busController        = _busTransform.GetComponent<BusController>();
        _busNearby            = true;
        _doorsOpenedThisVisit = false;

        OnBusEnteredStop?.Invoke(stopName);
        Debug.Log($"[BusStop] {stopName} — bus arrived. Stop when ready and press E to open doors.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Bus")) return;
        _busNearby = false;
    }

    // ── Called by LevelManager ────────────────────────────────────────────

    public bool TryCollectOutbound()
    {
        if (_collectedOutbound) return false;
        _collectedOutbound    = true;
        _bellRungThisApproach = false;  // reset so the bell can ring again on the return approach

        // Alight any rider already on the bus who is heading to this stop,
        // then board outbound passengers — both queues run simultaneously.
        AlightPassengersDestinedHere();
        StartBoardingQueue(_agents);
        return true;
    }

    public bool TryCollectInbound()
    {
        if (_collectedInbound) return false;
        _collectedInbound = true;

        // Reveal return passengers now that the bus is here with doors open.
        foreach (var a in _returnAgents) if (a != null) a.gameObject.SetActive(true);

        // Alight passengers destined here and board return-leg passengers simultaneously.
        AlightPassengersDestinedHere();
        StartBoardingQueue(_returnAgents);
        return true;
    }

    // Forces every remaining rider off the bus — called at the route's final trigger
    // so no one rides past the end of the line regardless of their destination.
    public static void ForceAlightAll()
    {
        var remaining = new List<PassengerAgent>(PassengerAgent.RidingPassengers);
        foreach (var agent in remaining)
            agent.AlightBus(null, "End of Line");
    }

    private void AlightPassengersDestinedHere()
    {
        _alightQueue.Clear();
        foreach (var agent in PassengerAgent.RidingPassengers)
            if (agent.DestinationStop == this)
                _alightQueue.Enqueue(agent);
        if (_alightQueue.Count > 0) AlightNext();
    }

    public void ResetStop()
    {
        _collectedOutbound    = false;
        _collectedInbound     = false;
        _doorsOpenedThisVisit = false;
        _busNearby            = false;
        _bellRungThisApproach = false;
        SpawnPassengers();
    }

    // ── Seat Layout ───────────────────────────────────────────────────────
    // 2 + 2 bus layout — 6 rows, 4 seats per row = 24 seats.
    // Values are LOCAL positions pre-divided by the test bus scale (2.5 × 2 × 6):
    //   x / 2.5  |  y / 2  |  z / 6
    // Left window  (-0.85 m): local x = -0.34
    // Left aisle   (-0.35 m): local x = -0.14
    // Right aisle  ( 0.35 m): local x =  0.14
    // Right window ( 0.85 m): local x =  0.34
    // Seat height  (-0.35 m below centre): local y = -0.175
    // Rows front→back (world z 1.2 → -2.7 m): local z 0.20 → -0.45
    // Update these if the bus model or scale changes.
    static readonly Vector3[] SeatLayout =
    {
        // Row 1 — just behind the door (local z = 0.20)
        new Vector3(-0.34f, -0.175f,  0.20f), new Vector3(-0.14f, -0.175f,  0.20f),
        new Vector3( 0.14f, -0.175f,  0.20f), new Vector3( 0.34f, -0.175f,  0.20f),
        // Row 2 (local z = 0.05)
        new Vector3(-0.34f, -0.175f,  0.05f), new Vector3(-0.14f, -0.175f,  0.05f),
        new Vector3( 0.14f, -0.175f,  0.05f), new Vector3( 0.34f, -0.175f,  0.05f),
        // Row 3 (local z = -0.10)
        new Vector3(-0.34f, -0.175f, -0.10f), new Vector3(-0.14f, -0.175f, -0.10f),
        new Vector3( 0.14f, -0.175f, -0.10f), new Vector3( 0.34f, -0.175f, -0.10f),
        // Row 4 (local z = -0.25)
        new Vector3(-0.34f, -0.175f, -0.25f), new Vector3(-0.14f, -0.175f, -0.25f),
        new Vector3( 0.14f, -0.175f, -0.25f), new Vector3( 0.34f, -0.175f, -0.25f),
        // Row 5 (local z = -0.40)
        new Vector3(-0.34f, -0.175f, -0.40f), new Vector3(-0.14f, -0.175f, -0.40f),
        new Vector3( 0.14f, -0.175f, -0.40f), new Vector3( 0.34f, -0.175f, -0.40f),
        // Row 6 — back row (local z = -0.45)
        new Vector3(-0.34f, -0.175f, -0.45f), new Vector3(-0.14f, -0.175f, -0.45f),
        new Vector3( 0.14f, -0.175f, -0.45f), new Vector3( 0.34f, -0.175f, -0.45f),
    };

    // Shared across ALL stops — tracks the next free seat on the bus.
    // Persists from stop to stop so passengers fill in order and never double-up.
    // Resets to 0 when the bus empties (AlightAll) or the level reloads.
    private static int _busNextSeatIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _busNextSeatIndex          = 0;
        _activeStops.Clear();
        OnAlightingStopApproached  = null;
    }

    // ── Boarding Queue ────────────────────────────────────────────────────

    private void StartBoardingQueue(List<PassengerAgent> agents)
    {
        if (agents.Count == 0) return;
        _boardingQueue.Clear();

        // Destinations: every active stop except this one.
        // Falls back to this stop in single-stop levels so no one rides forever.
        var destinations = new List<BusStop>();
        foreach (var s in _activeStops)
            if (s != this) destinations.Add(s);
        if (destinations.Count == 0) destinations.Add(this);

        foreach (var a in agents)
        {
            if (a == null) continue;
            a.BoardingStop    = this;
            a.DestinationStop = destinations[UnityEngine.Random.Range(0, destinations.Count)];
            _boardingQueue.Enqueue(a);
        }
        BoardNext();
    }

    private void BoardNext()
    {
        if (_boardingQueue.Count == 0 || _busTransform == null)
        {
            PassengerAgent.ClearPanel();
            return;
        }

        // Enforce bus capacity — don't board anyone once the bus is full
        if (PassengerAgent.RidingPassengers.Count >= LevelManager.BusCapacity)
        {
            PassengerAgent.ClearPanel();
            return;
        }

        var agent = _boardingQueue.Dequeue();
        if (agent == null) { BoardNext(); return; }

        Vector3 seat = SeatLayout[_busNextSeatIndex % SeatLayout.Length];
        _busNextSeatIndex++;

        agent.BoardBus(_busTransform, seat, BoardNext);
    }

    private void AlightNext()
    {
        if (_alightQueue.Count == 0) return;
        var agent = _alightQueue.Dequeue();
        if (agent == null) { AlightNext(); return; }
        agent.AlightBus(AlightNext);
    }

    // ── Passenger Spawning ────────────────────────────────────────────────

    private void SpawnPassengers()
    {
        foreach (var a in _agents)       if (a != null) Destroy(a.gameObject);
        foreach (var a in _returnAgents) if (a != null) Destroy(a.gameObject);
        _agents.Clear();
        _returnAgents.Clear();

        var dataSet = passengerRoster != null
            ? passengerRoster.GetRandomUnique(waitingPassengers + waitingReturnPassengers)
            : new System.Collections.Generic.List<PassengerData>();

        SpawnGroup(_agents,       waitingPassengers,       xOffset:  3f, namePrefix: "Out", dataSet, startIndex: 0);
        SpawnGroup(_returnAgents, waitingReturnPassengers, xOffset: -3f, namePrefix: "Ret", dataSet, startIndex: waitingPassengers);

        // Return passengers wait hidden until the bus arrives on the inbound leg.
        foreach (var a in _returnAgents) if (a != null) a.gameObject.SetActive(false);
    }

    private void SpawnGroup(List<PassengerAgent> list, int count, float xOffset,
                            string namePrefix, System.Collections.Generic.List<PassengerData> dataSet, int startIndex)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 rand = UnityEngine.Random.insideUnitCircle * 1.5f;
            Vector3 pos  = transform.position + new Vector3(rand.x + xOffset, 0.35f, rand.y);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"{stopName}_{namePrefix}_{i}";
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.SetParent(transform);

            Destroy(go.GetComponent<Collider>());
            int di  = startIndex + i;
            var col = _colours[di % _colours.Length];
            var mat = go.GetComponent<MeshRenderer>().material;
            mat.color = col;
            mat.SetColor("_BaseColor", col);

            var agent = go.AddComponent<PassengerAgent>();
            agent.SetData(di < dataSet.Count ? dataSet[di] : null);
            list.Add(agent);
        }
    }

    // ── Editor Gizmo ──────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
        Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(transform.position, transform.localScale);

#if UNITY_EDITOR
        string label = waitingReturnPassengers > 0
            ? $"{stopName} ({waitingPassengers}↑ {waitingReturnPassengers}↓)"
            : $"{stopName} ({waitingPassengers}p)";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (transform.localScale.y * 0.5f + 0.5f), label);
#endif
    }
}
