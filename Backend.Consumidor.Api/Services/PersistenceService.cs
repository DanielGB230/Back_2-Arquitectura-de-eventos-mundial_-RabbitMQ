using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Backend.Consumidor.Api.Data;
using Backend.Consumidor.Api.Data.Models;
using Shared.Contracts.Enums;
using Shared.Contracts.Events;

namespace Backend.Consumidor.Api.Services;

public class PersistenceService : IPersistenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersistenceService> _logger;

    public PersistenceService(IServiceScopeFactory scopeFactory, ILogger<PersistenceService> logger)
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
            _logger.LogWarning("PERSISTENCE-SERVICE: No se pudo determinar el EventType del mensaje.");
            return;
        }

        var eventType = (EventType)eventTypeByte;

        if (!root.TryGetProperty("MatchId", out var matchIdProp) || !Guid.TryParse(matchIdProp.GetString(), out var matchId))
        {
             _logger.LogError("PERSISTENCE-SERVICE: MatchId no válido o ausente.");
             return;
        }

        _logger.LogInformation("PERSISTENCE-SERVICE: Procesando evento: {EventType} para MatchId: {MatchId}", eventType, matchId);

        var matchEvent = new Data.Models.MatchEvent
        {
            EventId = root.GetProperty("EventId").GetGuid(),
            MatchId = matchId,
            EventType = eventType,
            EventTime = root.GetProperty("EventTime").GetDateTime(),
            EventPayload = message
        };

        if (await dbContext.MatchEvents.AnyAsync(e => e.EventId == matchEvent.EventId))
        {
             _logger.LogWarning("PERSISTENCE-SERVICE: Evento {EventId} ya fue procesado. Ignorando.", matchEvent.EventId);
             return;
        }

        switch (eventType)
        {
            case EventType.MatchStarted:
                var existingMatch = await dbContext.Matches.FindAsync(matchId);
                
                if (existingMatch != null)
                {
                    _logger.LogWarning("PERSISTENCE-SERVICE: Match {MatchId} ya existe. Se recibió evento MatchStarted duplicado.", matchId);
                    matchEvent.Match = existingMatch;
                }
                else
                {
                    var startEvent = JsonSerializer.Deserialize<MatchStartedEvent>(message, options)!;
                    
                    var newMatch = new Match
                    {
                        MatchId = matchId,
                        HomeTeamId = startEvent.HomeTeamId,
                        AwayTeamId = startEvent.AwayTeamId,
                        HomeTeamName = startEvent.HomeTeamName,
                        AwayTeamName = startEvent.AwayTeamName,
                        HomeScore = 0,
                        AwayScore = 0,
                        Status = MatchStatus.InProgress,
                        MatchStatistic = new MatchStatistic()
                    };

                    matchEvent.Match = newMatch; 
                    dbContext.Matches.Add(newMatch);
                    _logger.LogInformation("PERSISTENCE-SERVICE: Nuevo Match creado: {MatchId}", matchId);
                }
                
                dbContext.MatchEvents.Add(matchEvent);
                break;

            case EventType.MatchEnded:
                dbContext.MatchEvents.Add(matchEvent);

                var endEvent = JsonSerializer.Deserialize<MatchEndedEvent>(message, options)!;
                var matchToEnd = await dbContext.Matches.FindAsync(matchId);
                if (matchToEnd != null)
                {
                    matchToEnd.Status = MatchStatus.Finished;
                    matchToEnd.HomeScore = endEvent.FinalHomeScore;
                    matchToEnd.AwayScore = endEvent.FinalAwayScore;
                }
                break;

            case EventType.Goal:
                dbContext.MatchEvents.Add(matchEvent);

                var goalEvent = JsonSerializer.Deserialize<GoalEvent>(message, options)!;
                var matchToUpdate = await dbContext.Matches.FindAsync(matchId);
                if (matchToUpdate != null)
                {
                    if (goalEvent.TeamId == matchToUpdate.HomeTeamId)
                    {
                        matchToUpdate.HomeScore++;
                    }
                    else if (goalEvent.TeamId == matchToUpdate.AwayTeamId)
                    {
                        matchToUpdate.AwayScore++;
                    }
                }
                break;

            // --- NUEVO: SOPORTE PARA TARJETAS Y CAMBIOS ---
            case EventType.Card:
            case EventType.Substitution:
                // Solo registramos el evento en la tabla MatchEvents.
                // Las ESTADÍSTICAS (contadores) las actualiza el StatisticsService.
                // Sin embargo, debemos asegurar que el registro de MatchEvent esté vinculado.
                dbContext.MatchEvents.Add(matchEvent);
                _logger.LogInformation("PERSISTENCE-SERVICE: Registrando evento {EventType} en el historial.", eventType);
                break;
                
            default:
                dbContext.MatchEvents.Add(matchEvent);
                break;
        }

        try 
        {
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("PERSISTENCE-SERVICE: Guardado exitoso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PERSISTENCE-SERVICE: Error guardando en BD.");
            throw; 
        }
    }
}
