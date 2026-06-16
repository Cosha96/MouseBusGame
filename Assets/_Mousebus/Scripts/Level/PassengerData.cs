using UnityEngine;

[CreateAssetMenu(menuName = "Mousebus/Passenger Data")]
public class PassengerData : ScriptableObject
{
    public string passengerName = "Unknown";
    public int    age           = 30;
    public string job           = "Unknown";
    [TextArea(1, 3)]
    public string hobbies       = "Unknown";
    public int    timesRidden   = 0;
}
