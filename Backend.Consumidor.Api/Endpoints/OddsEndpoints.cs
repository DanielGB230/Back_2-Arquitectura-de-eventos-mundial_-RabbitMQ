using Backend.Consumidor.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Backend.Consumidor.Api.Data;

namespace Backend.Consumidor.Api.Endpoints;

public static class OddsEndpoints
{
    public static void MapOddsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/odds/{matchId}", async (int matchId, IOddsService oddsService, MatchDbContext dbContext) =>
        {
            var odds = oddsService.GetOdds(matchId);

            if (odds is null)
            {
                return Results.NotFound($"Cuotas para MatchId {matchId} no encontradas. Las cuotas se inicializan con el primer evento del partido.");
            }
            
            var match = await dbContext.Matches.FindAsync(matchId);

            if (match is null)
            {
                // This case is unlikely if odds exist, but good practice to handle.
                return Results.NotFound($"Partido con MatchId {matchId} no encontrado.");
            }

            var response = new 
            {
                MatchId = matchId,
                HomeTeamName = match.HomeTeamName,
                HomeWinOdds = odds.Value.HomeWin,
                DrawOdds = odds.Value.Draw,
                AwayTeamName = match.AwayTeamName,
                AwayWinOdds = odds.Value.AwayWin
            };

            return Results.Ok(response);
        })
        .WithName("ObtenerCuotasPartido")
        .WithSummary("Obtener las cuotas de apuestas en vivo para un partido")
        .WithDescription("Permite consultar las cuotas actuales para la victoria del local (1), empate (X) y victoria del visitante (2), incluyendo los nombres de los equipos.");
    }
}
