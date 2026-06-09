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

        OnTriggered?.Invoke(this);

        // Disable after firing so it can't be triggered twice
        // (e.g. the bus reversing back through halfway)
        gameObject.SetActive(false);
    }
}
