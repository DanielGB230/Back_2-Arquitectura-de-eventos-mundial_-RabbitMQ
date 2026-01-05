using System.Text.Json;
using Backend.Consumidor.Api.Data;
using Shared.Contracts.Enums;
using Shared.Contracts.Events;

namespace Backend.Consumidor.Api.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(IServiceScopeFactory scopeFactory, ILogger<StatisticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleEventAsync(string message)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MatchDbContext>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        using JsonDocument jsonDoc = JsonDocument.Parse(message);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeElement) || !eventTypeElement.TryGetByte(out var eventTypeByte))
        {
            _logger.LogWarning("STATS-SERVICE: No se pudo determinar el EventType del mensaje.");
            return;
        }

        if (!root.TryGetProperty("MatchId", out var matchIdElement) || !matchIdElement.TryGetInt32(out var matchIdInt))
        {
            _logger.LogWarning("STATS-SERVICE: No se pudo determinar el MatchId del mensaje.");
            return;
        }

        var eventType = (EventType)eventTypeByte;

        // --- CORRECCIÓN AQUÍ ---
        // Si el partido apenas está empezando, no hay estadísticas que actualizar.
        // La persistencia ya se encarga de crear el registro inicial en cero.
        if (eventType == EventType.MatchStarted)
        {
            _logger.LogInformation("STATS-SERVICE: Ignorando evento MatchStarted (La inicialización la maneja Persistence).");
            return; 
        }
        // -----------------------

        var matchId = matchIdInt;

                        var stats = await dbContext.MatchStatistics.FindAsync(matchId);

                        int retries = 3; // Número de reintentos

                        int delayMs = 100; // Retraso entre reintentos en milisegundos

                

                        while (stats is null && retries > 0)

                        {

                            _logger.LogWarning("STATS-SERVICE: No se encontró la entidad de estadísticas para MatchId: {MatchId}. Reintentando en {Delay}ms... ({Retries} reintentos restantes)", matchId, delayMs, retries);

                            await Task.Delay(delayMs);

                            stats = await dbContext.MatchStatistics.FindAsync(matchId); // Reintentar la búsqueda

                            retries--;

                        }

                

                        if (stats is null)

                        {

                            _logger.LogError("STATS-SERVICE: La entidad de estadísticas para MatchId: {MatchId} no se encontró después de varios reintentos. No se actualizarán las estadísticas para este evento.", matchId);

                            return; // No se encontró, abortar procesamiento para este evento

                        }

                        

                        _logger.LogInformation("STATS-SERVICE: Procesando evento {EventType} para MatchId: {MatchId}", eventType, matchId);

        stats.TotalEvents++;

        switch (eventType)
        {
            case EventType.Goal:
                stats.TotalGoals++;
                break;
            case EventType.Card:
                var cardEvent = JsonSerializer.Deserialize<CardEvent>(message, options)!;
                if (cardEvent.CardType == CardType.Yellow)
                {
                    stats.TotalYellowCards++;
                }
                else
                {
                    stats.TotalRedCards++;
                }
                break;
            case EventType.Substitution:
                stats.TotalSubstitutions++;
                break;
        }

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("STATS-SERVICE: Estadísticas actualizadas para MatchId: {MatchId}", matchId);
    }
}
