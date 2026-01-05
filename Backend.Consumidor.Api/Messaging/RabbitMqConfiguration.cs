namespace Backend.Consumidor.Api.Messaging;

// Reutilizamos la misma clase de configuración que en el productor.
public class RabbitMqConfiguration
{
    public string? Url { get; set; }
}
