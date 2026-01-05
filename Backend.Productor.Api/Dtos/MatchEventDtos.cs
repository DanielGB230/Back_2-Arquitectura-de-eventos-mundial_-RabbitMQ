using Shared.Contracts.Events;

namespace Backend.Productor.Api.Dtos;

public record StartMatchRequest(int? MatchId, int? HomeTeamId, int? AwayTeamId, string HomeTeamName, string AwayTeamName);
public record EndMatchRequest(int MatchId, int FinalHomeScore, int FinalAwayScore);
public record GoalRequest(int MatchId, int TeamId, int PlayerId, int Minute);
public record CardRequest(int MatchId, int TeamId, int PlayerId, CardType CardType, int Minute);
public record SubstitutionRequest(int MatchId, int TeamId, int PlayerInId, int PlayerOutId, int Minute);
