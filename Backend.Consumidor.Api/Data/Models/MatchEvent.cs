using System.ComponentModel.DataAnnotations;
using Shared.Contracts.Enums;

namespace Backend.Consumidor.Api.Data.Models;

public class MatchEvent
{
    [Key]
    public Guid EventId { get; set; }
    
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public EventType EventType { get; set; }
    public DateTime EventTime { get; set; }
    
    // Almacenamos el payload del evento como JSON para flexibilidad
    public string EventPayload { get; set; } = string.Empty;
}
