namespace Backend.Consumidor.Api.Services;

public interface IEmailService
{
    Task HandleEventAsync(string message);
}
