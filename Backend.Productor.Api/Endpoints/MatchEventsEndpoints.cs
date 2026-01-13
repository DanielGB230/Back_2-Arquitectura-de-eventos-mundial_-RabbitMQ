using Backend.Productor.Api.Dtos;
using Backend.Productor.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults; // Necesario para TypedResults si quisieras usarlos

namespace Backend.Productor.Api.Endpoints;

public static class MatchEventsEndpoints
{
    public static void MapMatchEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/match-events")
            .WithTags("Eventos del Partido"); // Agrupa todo bajo este nombre en Swagger

        // 1. INICIO DE PARTIDO
        group.MapPost("/start", (StartMatchRequest request, IMatchEventService service) =>
        {
            // CAMBIO: Lógica para Guid en lugar de int
            // Si viene null, generamos uno nuevo. Ya no comparamos con 0.
            var matchId = request.MatchId ?? Guid.NewGuid();
            
            var homeTeamId = (request.HomeTeamId == null || request.HomeTeamId == 0) ? Random.Shared.Next(1, 1000) : request.HomeTeamId.Value;
            var awayTeamId = (request.AwayTeamId == null || request.AwayTeamId == 0) ? Random.Shared.Next(1, 1000) : request.AwayTeamId.Value;

            var finalRequest = new StartMatchRequest(
                matchId,
                homeTeamId,
                awayTeamId,
                request.HomeTeamName,
                request.AwayTeamName
            );

            service.PublishStartMatchEvent(finalRequest);
            return Results.Accepted($"/api/match-events/{finalRequest.MatchId}", finalRequest);
        })
        .WithName("IniciarPartido")
        .WithSummary("Registrar inicio de partido") 
        .WithDescription("Registra el inicio de un nuevo partido. Esto hace que el sistema guarde sus detalles, avise a quienes siguen el partido y prepare el conteo de estadísticas.");

        // 2. FIN DE PARTIDO
        group.MapPost("/end", (EndMatchRequest request, IMatchEventService service) =>
        {
            service.PublishEndMatchEvent(request);
            return Results.Accepted($"/api/match-events/{request.MatchId}", "MatchEnded event published.");
        })
        .WithName("FinalizarPartido")
        .WithSummary("Registrar fin de partido")
        .WithDescription("Registra el fin de un partido. Esto provoca el envío de un correo (simulado), que se guarden los resultados finales y se avise a los seguidores.");

        // 3. GOL
        group.MapPost("/goal", (GoalRequest request, IMatchEventService service) =>
        {
            service.PublishGoalEvent(request);
            return Results.Accepted($"/api/match-events/{request.MatchId}", "Goal event published.");
        })
        .WithName("RegistrarGol")
        .WithSummary("Registrar un gol")
        .WithDescription("Registra un gol en el partido. Esto activa el envío de un correo (simulado), que se actualice el marcador en la base de datos y se avise a los seguidores, y se actualicen las estadísticas.");

        // 4. TARJETA
        group.MapPost("/card", (CardRequest request, IMatchEventService service) =>
        {
            service.PublishCardEvent(request);
            return Results.Accepted($"/api/match-events/{request.MatchId}", "Card event published.");
        })
        .WithName("RegistrarTarjeta")
        .WithSummary("Registrar tarjeta amarilla/roja")
        .WithDescription("Registra una tarjeta en el partido. Esto hace que el sistema guarde el incidente, avise a los seguidores del partido y actualice las estadísticas de tarjetas.");

        // 5. SUSTITUCIN
        group.MapPost("/substitution", (SubstitutionRequest request, IMatchEventService service) =>
        {
            service.PublishSubstitutionEvent(request);
            return Results.Accepted($"/api/match-events/{request.MatchId}", "Substitution event published.");
        })
        .WithName("RegistrarSustitucion")
        .WithSummary("Registrar cambio de jugador")
        .WithDescription("Registra una sustitucin de jugador y publica el evento SubstitutionEvent.");
    }
}
