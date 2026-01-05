using Shared.Contracts.Enums;

namespace Shared.Contracts.Events;

public abstract class BaseMatchEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public int MatchId { get; set; }
    public EventType EventType { get; protected set; }
    public DateTime EventTime { get; set; } = DateTime.UtcNow;
}
