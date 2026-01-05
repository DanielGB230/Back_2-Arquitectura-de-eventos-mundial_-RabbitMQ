namespace Backend.Consumidor.Api.Services;

public interface IStatisticsService
{
    Task HandleEventAsync(string message);
}
