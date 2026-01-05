namespace Backend.Consumidor.Api.Services;

public interface IPersistenceService
{
    Task HandleEventAsync(string message);
}
