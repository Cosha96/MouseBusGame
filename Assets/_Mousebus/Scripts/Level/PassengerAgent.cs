using System;
using UnityEngine;

// Spawned at runtime by BusStop — one per waiting passenger.
//
// Boarding is two-phase so all passengers funnel through the same door:
//   1. WalkingToDoor  — approach the bus door in world space (all converge here)
//   2. WalkingToSeat  — after parenting to bus, shuffle to the assigned seat
//
// IMPORTANT — local positions are pre-divided by the test bus scale (2.5 × 2 × 6).
// Update DoorLocalPos and the seat layout in BusStop if the bus model/scale changes.
public class PassengerAgent : MonoBehaviour
{
    public enum State { Waiting, WalkingToDoor, WalkingToSeat, Riding, Alighting, Done }

    [SerializeField] private float moveSpeed   = 5f;
    [SerializeField] private float alightSpeed = 12f;

    // Right-side door in LOCAL space for the test bus (scale 2.5 × 2 × 6):
    //   world offset from bus centre = (1.4 m right, 0, 1.8 m forward)
    //   → local = (1.4/2.5, 0/2, 1.8/6) = (0.56, 0, 0.30) — just outside the right wall
    public static readonly Vector3 DoorLocalPos = new Vector3(0.56f, 0f, 0.30f);

    // ── Passenger Card Events ─────────────────────────────────────────────
    // PassengerInfoPanel subscribes to these to slide the card in/out.
    public static event Action<PassengerData> OnPassengerHighlighted;
    public static event Action                OnPanelCleared;

    // AlightingNoticePanel subscribes to this to show a quick name badge.
    public static event Action<PassengerData> OnPassengerAlighting;

    // Called by BusStop when the queue empties (all passengers seated).
    public static void ClearPanel() => OnPanelCleared?.Invoke();

    // ── Riding Manifest ───────────────────────────────────────────────────
    // All passengers currently seated on the bus. PassengerListPanel reads this.
    private static readonly System.Collections.Generic.List<PassengerAgent> _ridingPassengers = new();
    public  static System.Collections.Generic.IReadOnlyList<PassengerAgent> RidingPassengers => _ridingPassengers;

    // Fires with the new seated count every time someone boards or alights.
    // FloatingPassengerCount subscribes to this for a per-passenger live ticker.
    public static event Action<int> OnRidingCountChanged;

    // Everyone who boarded this run — never removed on alight.
    // Survives scene transitions so LevelCompletePassengerPanel can read it.
    private static readonly System.Collections.Generic.List<PassengerRideRecord> _rideLog = new();
    public  static System.Collections.Generic.IReadOnlyList<PassengerRideRecord> RideLog  => _rideLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetManifest()
    {
        _ridingPassengers.Clear();
        _rideLog.Clear();
        OnPanelCleared         = null;
        OnPassengerHighlighted = null;
        OnPassengerAlighting   = null;
        OnRidingCountChanged   = null;
    }

    // ── Instance Data ─────────────────────────────────────────────────────
    public PassengerData Data            { get; private set; }
    public BusStop       BoardingStop    { get; set; }
    public BusStop       DestinationStop { get; set; }

    public void SetData(PassengerData data) => Data = data;

    private State              _state = State.Waiting;
    private Transform          _bus;
    private Vector3            _seatLocalPos;
    private Vector3            _alightTarget;
    private Action             _onBoarded;
    private Action             _onAlighted;
    private PassengerRideRecord _rideRecord;

    // ── Called by BusStop ─────────────────────────────────────────────────

    public void BoardBus(Transform bus, Vector3 seatLocalPos, Action onBoarded)
    {
        if (_state != State.Waiting) return;
        _bus          = bus;
        _seatLocalPos = seatLocalPos;
        _onBoarded    = onBoarded;
        _state        = State.WalkingToDoor;

        Debug.Log($"[Passenger] Boarding: {(Data != null ? Data.passengerName : "Anonymous")}");
        OnPassengerHighlighted?.Invoke(Data);
    }

    public void AlightBus(Action onAlighted = null, string actualStopName = null)
    {
        if (_state != State.Riding) return;
        _ridingPassengers.Remove(this);
        OnRidingCountChanged?.Invoke(_ridingPassengers.Count);
        OnPassengerAlighting?.Invoke(Data);
        if (_rideRecord != null)
        {
            _rideRecord.AlightedTime = LevelManager.GetCurrentTimeString();
            if (actualStopName != null) _rideRecord.AlightedAtStop = actualStopName;
        }
        _onAlighted = onAlighted;
        transform.SetParent(null);

        float angle   = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist    = UnityEngine.Random.Range(7f, 13f);
        _alightTarget = transform.position + new Vector3(
            Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

        _state = State.Alighting;
    }

    // ── Update ────────────────────────────────────────────────────────────

    private void Update()
    {
        switch (_state)
        {
            case State.WalkingToDoor:
                if (_bus == null) { _state = State.Done; return; }

                // TransformPoint converts the scaled local door pos to world space correctly
                Vector3 doorWorld = _bus.TransformPoint(DoorLocalPos);
                transform.position = Vector3.MoveTowards(
                    transform.position, doorWorld, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, doorWorld) < 0.2f)
                {
                    transform.SetParent(_bus);
                    transform.localPosition = DoorLocalPos;
                    _state = State.WalkingToSeat;
                }
                break;

            case State.WalkingToSeat:
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition, _seatLocalPos, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.localPosition, _seatLocalPos) < 0.1f)
                {
                    transform.localPosition = _seatLocalPos;
                    _state = State.Riding;
                    _ridingPassengers.Add(this);
                    _rideRecord = new PassengerRideRecord
                    {
                        Data          = Data,
                        BoardedAtStop = BoardingStop?.stopName    ?? "Unknown",
                        AlightedAtStop = DestinationStop?.stopName ?? "Unknown",
                        BoardedTime   = LevelManager.GetCurrentTimeString()
                    };
                    _rideLog.Add(_rideRecord);
                    OnRidingCountChanged?.Invoke(_ridingPassengers.Count);
                    _onBoarded?.Invoke();
                }
                break;

            case State.Alighting:
                transform.position = Vector3.MoveTowards(
                    transform.position, _alightTarget, alightSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, _alightTarget) < 0.2f)
                {
                    _state = State.Done;
                    _onAlighted?.Invoke();  // fire before destroy so BusStop can chain the next
                    Destroy(gameObject);
                }
                break;
        }
    }
}
