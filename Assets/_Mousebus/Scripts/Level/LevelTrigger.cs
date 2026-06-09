using System;
using UnityEngine;

// Attach to any GameObject to make it a level trigger.
// The Bus must have the "Bus" tag for this to fire — set that in the Inspector.
// Run Mousebus → Setup Level Triggers to create the proxy cubes automatically.
[RequireComponent(typeof(Collider))]
public class LevelTrigger : MonoBehaviour
{
    // Fired when the bus enters this trigger — LevelManager listens to this
    public static event Action<LevelTrigger> OnTriggered;

    [Tooltip("'halfway' fires the midpoint cutscene.\n'end' fires the outro and completes the level.")]
    public string triggerId;

    private void Awake()
    {
        // Guarantee the collider is always a trigger regardless of Inspector settings
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Bus")) return;

        // Fire the event — LevelManager decides whether to act based on the current
        // level phase. The phase guards there prevent double-processing, so we don't
        // need to disable ourselves here. Disabling on first contact caused TRG_End to
        // disappear if the bus grazed it on the way out (wrong phase, so LevelManager
        // ignored it, but the trigger was already gone for the return leg).
        OnTriggered?.Invoke(this);
    }
}
