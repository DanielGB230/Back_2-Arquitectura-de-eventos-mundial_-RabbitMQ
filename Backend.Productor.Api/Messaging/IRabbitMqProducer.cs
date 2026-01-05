namespace Backend.Productor.Api.Messaging;

public interface IRabbitMqProducer
{
    void PublishMessage<T>(T message, string exchange, string routingKey);
}
