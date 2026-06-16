using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Mousebus/Passenger Roster")]
public class PassengerRoster : ScriptableObject
{
    public List<PassengerData> passengers = new();

    // Returns count unique random picks. Never returns duplicates within one call.
    // If the roster has fewer entries than requested, returns what's available.
    public List<PassengerData> GetRandomUnique(int count)
    {
        var pool   = new List<PassengerData>(passengers);
        var result = new List<PassengerData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }
}
