namespace Shared.Contracts.Events;

public class MatchStartedEvent : BaseMatchEvent
{
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public string HomeTeamName { get; set; }
    public string AwayTeamName { get; set; }

    public MatchStartedEvent()
    {
        EventType = Enums.EventType.MatchStarted;
    }
}
