using Microsoft.EntityFrameworkCore;
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
            return;
        }
        
        if (!root.TryGetProperty("MatchId", out var matchIdElement) || !Guid.TryParse(matchIdElement.GetString(), out var matchId))
        {
            return;
        }

        var eventType = ((Shared.Contracts.Enums.EventType)eventTypeByte).ToString();

        // LOG CRÍTICO: Para saber que el servicio despertó
        _logger.LogInformation("NOTIFICATIONS-SERVICE: Procesando '{EventType}' para MatchId {MatchId}", eventType, matchId);

        // Aumentamos ligeramente el delay para dar tiempo a Persistence y Statistics
        await Task.Delay(350);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MatchDbContext>();

            // Forzamos la recarga desde la DB para evitar caché de EF
            var match = await dbContext.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.MatchId == matchId);
            var stats = await dbContext.MatchStatistics.AsNoTracking().FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null || stats == null)
            {
                _logger.LogWarning("NOTIFICATIONS-SERVICE: Reintentando búsqueda de datos para {MatchId}...", matchId);
                await Task.Delay(500);
                match = await dbContext.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.MatchId == matchId);
                stats = await dbContext.MatchStatistics.AsNoTracking().FirstOrDefaultAsync(m => m.MatchId == matchId);
            }

            if (match == null || stats == null)
            {
                _logger.LogError("NOTIFICATIONS-SERVICE: No hay datos en DB para {MatchId}", matchId);
                return;
            }

            object? latestEventData = null;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            switch (eventType)
            {
                case "Goal":
                    latestEventData = JsonSerializer.Deserialize<Shared.Contracts.Events.GoalEvent>(message, options);
                    break;
                case "Card":
                    latestEventData = JsonSerializer.Deserialize<Shared.Contracts.Events.CardEvent>(message, options);
                    break;
                case "Substitution":
                    latestEventData = JsonSerializer.Deserialize<Shared.Contracts.Events.SubstitutionEvent>(message, options);
                    break;
            }

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
                stats.TotalEvents,
                LatestEvent = latestEventData
            };

            var jsonString = JsonSerializer.Serialize(combinedData);

            if (eventType == "MatchStarted")
            {
                await _hubContext.Clients.All.SendAsync("MatchStarted", jsonString);
                _logger.LogInformation("NOTIFICATIONS-SERVICE: Broadast MatchStarted");
            }
            else if (eventType == "MatchEnded")
            {
                await _hubContext.Clients.All.SendAsync("MatchEnded", jsonString);
                _logger.LogInformation("NOTIFICATIONS-SERVICE: Broadast MatchEnded");
            }
            else
            {
                // ENVÍO A GRUPO Y GLOBAL (Si es gol)
                await _hubContext.Clients.Group(matchId.ToString()).SendAsync("MatchStatsUpdated", jsonString);
                
                if (eventType == "Goal")
                {
                    await _hubContext.Clients.All.SendAsync("ScoreUpdated", jsonString);
                }
                _logger.LogInformation("NOTIFICATIONS-SERVICE: Mensaje enviado a SignalR para {EventType}", eventType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NOTIFICATIONS-SERVICE: Error enviando a SignalR.");
        }
    }
}