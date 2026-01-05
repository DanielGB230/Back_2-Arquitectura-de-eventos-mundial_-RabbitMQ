namespace Shared.Contracts.Events;

public class MatchEndedEvent : BaseMatchEvent
{
    public int FinalHomeScore { get; set; }
    public int FinalAwayScore { get; set; }

    public MatchEndedEvent()
    {
        EventType = Enums.EventType.MatchEnded;
    }
}
