namespace Shared.Contracts.Events;

public class SubstitutionEvent : BaseMatchEvent
{
    public int TeamId { get; set; }
    public int PlayerInId { get; set; }
    public int PlayerOutId { get; set; }
    public int Minute { get; set; }

    public SubstitutionEvent()
    {
        EventType = Enums.EventType.Substitution;
    }
}
