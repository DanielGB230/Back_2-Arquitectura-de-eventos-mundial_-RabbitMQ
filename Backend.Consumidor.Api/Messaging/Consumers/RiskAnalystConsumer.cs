using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts.Enums; // Needed for EventType
using Backend.Consumidor.Api.Services; // Needed for IOddsService
using Backend.Consumidor.Api.Messaging; // Needed for RabbitMqConfiguration
using Shared.Contracts.Events; // Needed for BaseMatchEvent (for EventType extraction)

namespace Backend.Consumidor.Api.Messaging.Consumers;

public class RiskAnalystConsumer : BackgroundService
{
    private readonly ILogger<RiskAnalystConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    private const string ExchangeName = "worldcup.events";
    private const string QueueName = "queue.risk_analyst";

    public RiskAnalystConsumer(
        IOptions<RabbitMqConfiguration> rabbitMqOptions,
        IServiceProvider serviceProvider,
        ILogger<RiskAnalystConsumer> logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        var factory = new ConnectionFactory
        {
            Uri = new Uri(rabbitMqOptions.Value.Url ?? throw new InvalidOperationException("RabbitMQ URL is not set.")),
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _logger.LogInformation("RISK-ANALYST-CONSUMER: Conectado a RabbitMQ.");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        
        // Bind to all event types that might affect odds
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.started");
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.ended");
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.goal");
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.card");
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.substitution");
        
        _logger.LogInformation("RISK-ANALYST-CONSUMER: Esperando mensajes en la cola '{QueueName}'...", QueueName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceived;

        _channel.BasicConsume(QueueName, autoAck: false, consumer);

        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs ea)
    {
        var message = Encoding.UTF8.GetString(ea.Body.ToArray());
        _logger.LogInformation("RISK-ANALYST-CONSUMER: Mensaje recibido.");
        
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var oddsService = scope.ServiceProvider.GetRequiredService<IOddsService>();

                // Use the same polymorphic deserialization logic as other consumers
                using JsonDocument jsonDoc = JsonDocument.Parse(message);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("EventType", out var eventTypeElement) || !eventTypeElement.TryGetByte(out var eventTypeByte))
                {
                    _logger.LogWarning("RISK-ANALYST-CONSUMER: No se pudo determinar el EventType del mensaje.");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                // CAMBIO: Extraer MatchId como Guid
                if (!root.TryGetProperty("MatchId", out var matchIdElement) || !Guid.TryParse(matchIdElement.GetString(), out var matchId))
                {
                    _logger.LogWarning("RISK-ANALYST-CONSUMER: No se pudo determinar el MatchId del mensaje (o no es un Guid válido).");
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                var eventType = (EventType)eventTypeByte;

                await oddsService.AdjustOddsForEvent(matchId, eventType, message);
            }
            
            _channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RISK-ANALYST-CONSUMER: Error procesando el mensaje. Enviando a NACK. Mensaje: {Message}", message);
            _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cerrando consumidor Analista de Riesgos...");
        _channel.Close();
        _connection.Close();
        await base.StopAsync(cancellationToken);
    }
}