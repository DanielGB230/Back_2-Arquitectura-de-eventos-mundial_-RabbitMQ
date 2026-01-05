namespace Shared.Contracts.Events;

public enum CardType
{
    Yellow,
    Red
}

public class CardEvent : BaseMatchEvent
{
    public int TeamId { get; set; }
    public int PlayerId { get; set; }
    public CardType CardType { get; set; }
    public int Minute { get; set; }

    public CardEvent()
    {
        EventType = Enums.EventType.Card;
    }
}
