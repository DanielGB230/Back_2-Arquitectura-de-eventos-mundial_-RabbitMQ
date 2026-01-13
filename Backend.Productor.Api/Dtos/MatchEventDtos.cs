using Shared.Contracts.Events;

namespace Backend.Productor.Api.Dtos;

public record StartMatchRequest(Guid? MatchId, int? HomeTeamId, int? AwayTeamId, string HomeTeamName, string AwayTeamName);
public record EndMatchRequest(Guid MatchId, int FinalHomeScore, int FinalAwayScore);
public record GoalRequest(Guid MatchId, int TeamId, int PlayerId, int Minute);
public record CardRequest(Guid MatchId, int TeamId, int PlayerId, CardType CardType, int Minute);
public record SubstitutionRequest(Guid MatchId, int TeamId, int PlayerInId, int PlayerOutId, int Minute);
