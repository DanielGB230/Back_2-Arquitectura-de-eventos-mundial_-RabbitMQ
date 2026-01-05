using System.Text.Json;
using System.Threading.Tasks;
using Backend.Consumidor.Api.Data;
using Backend.Consumidor.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Backend.Consumidor.Api.Services;

public class NotificationsService : INotificationsService
{
    private readonly ILogger<NotificationsService> _logger;
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationsService(ILogger<NotificationsService> logger, IHubContext<NotificationsHub> hubContext, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    public async Task HandleEventAsync(string message)
    {
        using JsonDocument jsonDoc = JsonDocument.Parse(message);
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeElement) || !eventTypeElement.TryGetByte(out var eventTypeByte))
        {
            _logger.LogWarning("NOTIFICATIONS-SERVICE: No se pudo determinar el EventType del mensaje.");
            return;
        }
        
        if (!root.TryGetProperty("MatchId", out var matchIdElement) || !matchIdElement.TryGetInt32(out var matchIdInt))
        {
            _logger.LogWarning("NOTIFICATIONS-SERVICE: No se pudo determinar el MatchId del mensaje.");
            return;
        }

        var eventType = ((Shared.Contracts.Enums.EventType)eventTypeByte).ToString();
        var matchId = matchIdInt;

        _logger.LogInformation("NOTIFICATIONS-SERVICE: Evento '{EventType}' recibido para MatchId {MatchId}. Obteniendo estadísticas actualizadas...", eventType, matchId);

        // Pragmatic delay to increase the chance that other services have finished updating the database.
        // This helps deal with the eventual consistency of the distributed event handling.
        await Task.Delay(250);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchDbContext>();

            var match = await dbContext.Matches.FindAsync(matchId);
            var stats = await dbContext.MatchStatistics.FindAsync(matchId);

            if (match == null || stats == null)
            {
                _logger.LogError("NOTIFICATIONS-SERVICE: No se encontraron datos de partido o estadísticas para MatchId {MatchId} después de un evento. No se enviará notificación.", matchId);
                return;
            }

            // Combine data into a single object, same as the API endpoint.
            var combinedData = new 
            {
                match.MatchId,
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeTeamName,
                match.AwayTeamName,
                match.HomeScore,
                match.AwayScore,
                match.Status,
                stats.TotalGoals,
                stats.TotalYellowCards,
                stats.TotalRedCards,
                stats.TotalSubstitutions,
                stats.TotalEvents
            };

            // Send the complete, updated statistics object to all clients in the group.
            await _hubContext.Clients.Group(matchId.ToString()).SendAsync("MatchStatsUpdated", combinedData, CancellationToken.None);

            _logger.LogInformation("NOTIFICATIONS-SERVICE: Estadísticas actualizadas para MatchId {MatchId} enviadas al grupo SignalR.", matchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NOTIFICATIONS-SERVICE: Error al procesar y enviar notificación de estadísticas para MatchId {MatchId}.", matchId);
        }
    }
}