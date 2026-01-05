using System.Text;
using Backend.Consumidor.Api.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Backend.Consumidor.Api.Messaging.Consumers;

public class StatisticsConsumer : BackgroundService
{
    private readonly ILogger<StatisticsConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    private const string ExchangeName = "worldcup.events";
    private const string QueueName = "queue.statistics";

    public StatisticsConsumer(
        IOptions<RabbitMqConfiguration> rabbitMqOptions,
        IServiceProvider serviceProvider,
        ILogger<StatisticsConsumer> logger)
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
        _logger.LogInformation("STATS-CONSUMER: Conectado a RabbitMQ.");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(QueueName, ExchangeName, "worldcup.match.*.*");
        
        _logger.LogInformation("STATS-CONSUMER: Esperando mensajes en la cola '{QueueName}'...", QueueName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceived;

        _channel.BasicConsume(QueueName, autoAck: false, consumer);

        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs ea)
    {
        var message = Encoding.UTF8.GetString(ea.Body.ToArray());
        _logger.LogInformation("STATS-CONSUMER: Mensaje recibido.");

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();
                await statisticsService.HandleEventAsync(message);
            }
            
            _channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STATS-CONSUMER: Error procesando el mensaje. Enviando a NACK. Mensaje: {Message}", message);
            _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cerrando consumidor de estadísticas...");
        _channel.Close();
        _connection.Close();
        await base.StopAsync(cancellationToken);
    }
}
