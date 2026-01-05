using Backend.Consumidor.Api.Data;
using Backend.Consumidor.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Consumidor.Api.Endpoints;

public static class MatchStatsEndpoints
{
    public static void MapMatchStatsEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => "Backend.Consumidor.Api is running.")
            .WithName("VerificarEstadoAPI")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Verificar Estado de la API";
                operation.Description = "Este endpoint simplemente confirma que la API del consumidor está en funcionamiento y escuchando eventos.";
                return operation;
            });

        app.MapGet("/api/match-statistics/{matchId}", async (int matchId, MatchDbContext dbContext) =>
        {
            var match = await dbContext.Matches.FindAsync(matchId);
            var stats = await dbContext.MatchStatistics.FindAsync(matchId);

            if (match is null && stats is null)
            {
                return Results.NotFound($"Estadísticas para MatchId {matchId} no encontradas.");
            }
            
            // Combine match and stats data
            var combinedData = new 
            {
                match?.MatchId,
                match?.HomeTeamId,
                match?.AwayTeamId,
                match?.HomeTeamName,
                match?.AwayTeamName,
                match?.HomeScore,
                match?.AwayScore,
                match?.Status,
                stats?.TotalGoals,
                stats?.TotalYellowCards,
                stats?.TotalRedCards,
                stats?.TotalSubstitutions,
                stats?.TotalEvents
            };

            return Results.Ok(combinedData);
        })
        .WithName("ObtenerEstadisticasPartido")
        .WithSummary("Obtener las estadísticas de un partido por su ID")
        .WithDescription("Permite consultar las estadísticas acumuladas de un partido específico, incluyendo goles, tarjetas, etc.");

        app.MapGet("/api/matches/status/{status}", async (string status, MatchDbContext dbContext) =>
        {
            if (!Enum.TryParse<MatchStatus>(status, true, out var matchStatus))
            {
                return Results.BadRequest($"Estado de partido '{status}' inválido. Valores permitidos: InProgress, Finished.");
            }

            var matches = await dbContext.Matches
                .Where(m => m.Status == matchStatus)
                .Select(m => new { m.MatchId, m.HomeTeamName, m.AwayTeamName, m.HomeScore, m.AwayScore, m.Status })
                .ToListAsync();

            return Results.Ok(matches);
        })
        .WithName("ListarPartidosPorEstado")
        .WithSummary("Lista partidos por su estado (InProgress, Finished)")
        .WithDescription("Permite obtener un listado de partidos que se encuentran en un estado específico.");
    }
}
