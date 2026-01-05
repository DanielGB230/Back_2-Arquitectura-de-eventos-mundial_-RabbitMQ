namespace Backend.Consumidor.Api.Services;

public interface INotificationsService
{
    Task HandleEventAsync(string message);
}
