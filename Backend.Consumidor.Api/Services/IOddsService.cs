using Shared.Contracts.Enums;
using System.Threading.Tasks;

namespace Backend.Consumidor.Api.Services;

public interface IOddsService
{
    Task AdjustOddsForEvent(int matchId, EventType eventType, string eventDetailsJson);
    (double HomeWin, double Draw, double AwayWin)? GetOdds(int matchId);
}
