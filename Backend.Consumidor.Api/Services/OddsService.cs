using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Consumidor.Api.Data; // Need this for MatchDbContext
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Consumidor.Api.Services;

public class OddsService : IOddsService
{
    private readonly ILogger<OddsService> _logger;
    private readonly IServiceScopeFactory _scopeFactory; // Added

    // Simulate current odds (HomeWin, Draw, AwayWin) stored per MatchId
    private readonly Dictionary<int, (double HomeWin, double Draw, double AwayWin)> _currentOdds = new();

    public OddsService(ILogger<OddsService> logger, IServiceScopeFactory scopeFactory) // Modified constructor
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private (double HomeWin, double Draw, double AwayWin) InitializeOdds(int matchId)
    {
        var defaultOdds = (HomeWin: 2.5, Draw: 3.5, AwayWin: 3.0);
        _currentOdds[matchId] = defaultOdds;
        _logger.LogInformation("ODDS-SERVICE: Cuotas iniciales para MatchId {MatchId}: Local {HomeWin}, Empate {Draw}, Visitante {AwayWin}",
                               matchId, defaultOdds.HomeWin, defaultOdds.Draw, defaultOdds.AwayWin);
        return defaultOdds;
    }

    public async Task AdjustOddsForEvent(int matchId, EventType eventType, string eventDetailsJson)
    {
        // Initialize odds for a new match if not present
        if (!_currentOdds.ContainsKey(matchId))
        {
            InitializeOdds(matchId);
        }

        var (homeWin, draw, awayWin) = _currentOdds[matchId];

        // Ensure odds are always positive and reasonable
        Func<double, double> ensurePositive = (odd) => Math.Max(1.01, odd);

        switch (eventType)
        {
            case EventType.MatchStarted:
                _logger.LogInformation("ODDS-SERVICE: MatchStarted para MatchId {MatchId}. Cuotas actuales: Local {HomeWin:F2}, Empate {Draw:F2}, Visitante {AwayWin:F2}",
                                       matchId, homeWin, draw, awayWin);
                break;

            case EventType.Goal:
                var goalEvent = JsonSerializer.Deserialize<GoalEvent>(eventDetailsJson);
                if (goalEvent == null) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<MatchDbContext>();
                    var match = await dbContext.Matches.FindAsync(matchId);

                    if (match == null)
                    {
                        _logger.LogError("ODDS-SERVICE: No se encontró el partido con MatchId {MatchId} para ajustar cuotas por gol.", matchId);
                        return;
                    }

                    _logger.LogInformation("ODDS-SERVICE: Ajustando cuotas por GOL en MatchId {MatchId}. Marcador anterior: {HomeScore}-{AwayScore} en minuto {Minute}",
                                           matchId, match.HomeScore, match.AwayScore, goalEvent.Minute);

                    // The score in the 'match' object is the score *before* this goal.
                    // The PersistenceService will increment it, but for this calculation, we need the score *after* the goal.
                    int newHomeScore = match.HomeScore;
                    int newAwayScore = match.AwayScore;
                    if (goalEvent.TeamId == match.HomeTeamId) newHomeScore++;
                    else if (goalEvent.TeamId == match.AwayTeamId) newAwayScore++;


                    double goalDifference = newHomeScore - newAwayScore;
                    // Clamp minute to a max of 90 to avoid excessive impact in extra time.
                    double timeFactor = Math.Min(goalEvent.Minute, 90) / 90.0;

                    // More aggressive adjustments
                    double baseWinDecrease = 1.2;
                    double baseLoseIncrease = 1.4;

                    if (goalDifference > 0) // Home team is now winning
                    {
                        double impact = Math.Pow(baseWinDecrease, goalDifference) * (1 + timeFactor);
                        homeWin = ensurePositive(homeWin / impact);
                        awayWin = ensurePositive(awayWin * (impact * baseLoseIncrease / baseWinDecrease));
                        draw = ensurePositive(draw * (impact / 1.5));
                    }
                    else if (goalDifference < 0) // Away team is now winning
                    {
                        double impact = Math.Pow(baseWinDecrease, Math.Abs(goalDifference)) * (1 + timeFactor);
                        awayWin = ensurePositive(awayWin / impact);
                        homeWin = ensurePositive(homeWin * (impact * baseLoseIncrease / baseWinDecrease));
                        draw = ensurePositive(draw * (impact / 1.5));
                    }
                    else // It's a draw now
                    {
                        double impact = 1.1 * (1 + timeFactor);
                        draw = ensurePositive(draw / impact);
                        // When it becomes a draw, both teams' odds to win should increase slightly
                        homeWin = ensurePositive(homeWin * 1.1);
                        awayWin = ensurePositive(awayWin * 1.1);
                    }
                }
                break;

            case EventType.Card:
                var cardEvent = JsonSerializer.Deserialize<CardEvent>(eventDetailsJson);
                if (cardEvent == null) return;

                _logger.LogInformation("ODDS-SERVICE: Ajustando cuotas por TARJETA en MatchId {MatchId} (TeamId: {TeamId}, Tipo: {CardType})",
                                       matchId, cardEvent.TeamId, cardEvent.CardType);

                if (cardEvent.CardType == CardType.Red)
                {
                    homeWin = ensurePositive(homeWin * 1.15);
                    awayWin = ensurePositive(awayWin * 1.15);
                    draw = ensurePositive(draw * 1.1);
                }
                else if (cardEvent.CardType == CardType.Yellow)
                {
                    homeWin = ensurePositive(homeWin * 1.05);
                    awayWin = ensurePositive(awayWin * 1.05);
                    draw = ensurePositive(draw * 1.02);
                }
                break;

            case EventType.Substitution:
                var subEvent = JsonSerializer.Deserialize<SubstitutionEvent>(eventDetailsJson);
                if (subEvent == null) return;
                _logger.LogInformation("ODDS-SERVICE: Ajustando cuotas por SUSTITUCIÓN en MatchId {MatchId} (TeamId: {TeamId})",
                                       matchId, subEvent.TeamId);
                homeWin = ensurePositive(homeWin * 1.02);
                awayWin = ensurePositive(awayWin * 1.02);
                draw = ensurePositive(draw * 1.01);
                break;

            case EventType.MatchEnded:
                var endEvent = JsonSerializer.Deserialize<MatchEndedEvent>(eventDetailsJson);
                if (endEvent == null) return;
                _logger.LogInformation("ODDS-SERVICE: Ajustando cuotas por FIN DE PARTIDO en MatchId {MatchId} (HomeScore: {HomeScore}, AwayScore: {AwayScore})",
                                       matchId, endEvent.FinalHomeScore, endEvent.FinalAwayScore);
                if (endEvent.FinalHomeScore > endEvent.FinalAwayScore)
                {
                    homeWin = 1.01;
                    draw = 1000.0;
                    awayWin = 1000.0;
                }
                else if (endEvent.FinalAwayScore > endEvent.FinalHomeScore)
                {
                    awayWin = 1.01;
                    draw = 1000.0;
                    homeWin = 1000.0;
                }
                else
                {
                    draw = 1.01;
                    homeWin = 1000.0;
                    awayWin = 1000.0;
                }
                break;
        }

        _currentOdds[matchId] = (homeWin, draw, awayWin);
        _logger.LogInformation("ODDS-SERVICE: Cuotas ACTUALIZADAS para MatchId {MatchId}: Local {HomeWin:F2}, Empate {Draw:F2}, Visitante {AwayWin:F2}",
                               matchId, homeWin, draw, awayWin);
    }

    public (double HomeWin, double Draw, double AwayWin)? GetOdds(int matchId)
    {
        // If odds are already in memory, return them.
        if (_currentOdds.TryGetValue(matchId, out var odds))
        {
            _logger.LogInformation("ODDS-SERVICE: Cuotas para MatchId {MatchId} encontradas en memoria.", matchId);
            return odds;
        }

        _logger.LogWarning("ODDS-SERVICE: Cuotas para MatchId {MatchId} no encontradas en memoria. Verificando en base de datos...", matchId);
        
        // If not in memory, check the database.
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MatchDbContext>();
        
        var matchExists = dbContext.Matches.Any(m => m.MatchId == matchId);

        if (matchExists)
        {
            _logger.LogInformation("ODDS-SERVICE: Partido con MatchId {MatchId} existe en la BD. Inicializando cuotas por defecto.", matchId);
            // If the match exists, initialize default odds, store, and return them.
            return InitializeOdds(matchId);
        }

        _logger.LogError("ODDS-SERVICE: Partido con MatchId {MatchId} no existe en la BD. No se pueden inicializar cuotas.", matchId);
        // If match doesn't exist, return null.
        return null;
    }
}