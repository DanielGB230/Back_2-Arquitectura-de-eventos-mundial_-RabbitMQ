using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Backend.Productor.Api.Messaging;

public class RabbitMqProducer : IRabbitMqProducer, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqProducer(IOptions<RabbitMqConfiguration> options)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.Value.Url ?? throw new InvalidOperationException("RabbitMQ URL is not configured.")),
            DispatchConsumersAsync = true // Recomendado para consumidores, buena práctica tenerlo.
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public void PublishMessage<T>(T message, string exchange, string routingKey)
    {
        // Declaramos el exchange para asegurarnos que existe.
        // Es una operación idempotente.
        _channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        
        var jsonMessage = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true; // Para que los mensajes sobrevivan a un reinicio de RabbitMQ

        Console.WriteLine($"--> Publicando mensaje en Exchange '{exchange}' con Routing Key '{routingKey}'");
        
        _channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);
    }

    public void Dispose()
    {
        _channel.Close();
        _connection.Close();
    }
}
