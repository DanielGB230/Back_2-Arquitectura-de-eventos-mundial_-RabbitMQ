namespace Shared.Contracts.Events;

public class GoalEvent : BaseMatchEvent
{
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public int Minute { get; set; }

    public GoalEvent()
    {
        EventType = Enums.EventType.Goal;
    }
}
