// Immutable record of one passenger's journey during a level run.
// Created when the passenger reaches their seat; alight fields filled when they step off.
// Stored in PassengerAgent.RideLog and read by LevelCompletePassengerPanel.
public class PassengerRideRecord
{
    public PassengerData Data;
    public string        BoardedAtStop;
    public string        AlightedAtStop;
    public string        BoardedTime;
    public string        AlightedTime;
}
