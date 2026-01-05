using System.Text.Json;
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

        var matchId = root.GetProperty("MatchId").GetInt32();
        _logger.LogInformation("PERSISTENCE-SERVICE: Procesando evento: {EventType} para MatchId: {MatchId}", eventType, matchId);

        // 1. Preparamos el evento, PERO NO LO AGREGAMOS AL CONTEXTO TODAVÍA
        // Nota: No asignamos MatchId aquí si es 0, dejaremos que EF lo resuelva si es un Match nuevo.
        var matchEvent = new Data.Models.MatchEvent
        {
            EventId = root.GetProperty("EventId").GetGuid(),
            // MatchId = matchId,  <-- REMOVIDO: Lo asignaremos condicionalmente abajo
            EventType = eventType,
            EventTime = root.GetProperty("EventTime").GetDateTime(),
            EventPayload = message
        };

        switch (eventType)
        {
            case EventType.MatchStarted:
                var startEvent = JsonSerializer.Deserialize<MatchStartedEvent>(message, options)!;
                
                var newMatch = new Match
                {
                    HomeTeamId = startEvent.HomeTeamId,
                    AwayTeamId = startEvent.AwayTeamId,
                    HomeTeamName = startEvent.HomeTeamName,
                    AwayTeamName = startEvent.AwayTeamName,
                    HomeScore = 0,
                    AwayScore = 0,
                    Status = MatchStatus.InProgress,
                    MatchStatistic = new MatchStatistic()
                };

                // SOLUCIÓN CLAVE: Vinculación por objeto, no por ID
                // Le decimos al evento: "Tu partido es este nuevo objeto que acabo de crear"
                matchEvent.Match = newMatch; 
                
                // Agregamos ambos (aunque al agregar newMatch, EF suele detectar el hijo automáticamente)
                dbContext.Matches.Add(newMatch);
                dbContext.MatchEvents.Add(matchEvent);
                break;

            case EventType.MatchEnded:
                // Para eventos existentes, SÍ usamos el ID que viene en el mensaje
                matchEvent.MatchId = matchId;
                dbContext.MatchEvents.Add(matchEvent);

                var endEvent = JsonSerializer.Deserialize<MatchEndedEvent>(message, options)!;
                var matchToEnd = await dbContext.Matches.FindAsync(endEvent.MatchId);
                if (matchToEnd != null)
                {
                    matchToEnd.Status = MatchStatus.Finished;
                    matchToEnd.HomeScore = endEvent.FinalHomeScore;
                    matchToEnd.AwayScore = endEvent.FinalAwayScore;
                }
                break;

            case EventType.Goal:
                // Para eventos existentes, SÍ usamos el ID que viene en el mensaje
                matchEvent.MatchId = matchId;
                dbContext.MatchEvents.Add(matchEvent);

                var goalEvent = JsonSerializer.Deserialize<GoalEvent>(message, options)!;
                var matchToUpdate = await dbContext.Matches.FindAsync(goalEvent.MatchId);
                if (matchToUpdate != null)
                {
                    _logger.LogInformation("PERSISTENCE-SERVICE: Comparando TeamId del Gol ({GoalTeamId}) con HomeTeamId ({HomeTeamId}) y AwayTeamId ({AwayTeamId}).", 
                                           goalEvent.TeamId, matchToUpdate.HomeTeamId, matchToUpdate.AwayTeamId);

                    if (goalEvent.TeamId == matchToUpdate.HomeTeamId)
                    {
                        matchToUpdate.HomeScore++;
                        _logger.LogInformation("PERSISTENCE-SERVICE: Gol para HomeTeam. Nuevo marcador: {HomeScore}-{AwayScore}", matchToUpdate.HomeScore, matchToUpdate.AwayScore);
                    }
                    else if (goalEvent.TeamId == matchToUpdate.AwayTeamId)
                    {
                        matchToUpdate.AwayScore++;
                        _logger.LogInformation("PERSISTENCE-SERVICE: Gol para AwayTeam. Nuevo marcador: {HomeScore}-{AwayScore}", matchToUpdate.HomeScore, matchToUpdate.AwayScore);
                    }
                    else
                    {
                        _logger.LogWarning("PERSISTENCE-SERVICE: El TeamId {TeamId} del evento de gol no corresponde a ningún equipo en el MatchId {MatchId}.", goalEvent.TeamId, matchToUpdate.MatchId);
                    }
                }
                else
                {
                    _logger.LogError("PERSISTENCE-SERVICE: No se encontró el partido con MatchId {MatchId} para procesar el gol.", goalEvent.MatchId);
                }
                break;
                
            default:
                // Caso por defecto para otros eventos
                matchEvent.MatchId = matchId;
                dbContext.MatchEvents.Add(matchEvent);
                break;
        }

        try 
        {
            await dbContext.SaveChangesAsync();
            _logger.LogInformation("PERSISTENCE-SERVICE: Evento procesado y guardado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PERSISTENCE-SERVICE: Error guardando en BD.");
            throw; // Re-lanzar para que el consumidor haga NACK/Reintento
        }
    }
}
