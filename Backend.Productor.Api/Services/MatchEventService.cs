using Backend.Productor.Api.Dtos;
using Backend.Productor.Api.Messaging;
using Shared.Contracts.Enums;
using Shared.Contracts.Events;

namespace Backend.Productor.Api.Services;

public class MatchEventService : IMatchEventService
{
    private readonly IRabbitMqProducer _producer;
    private const string ExchangeName = "worldcup.events";

    public MatchEventService(IRabbitMqProducer producer)
    {
        _producer = producer;
    }

    public void PublishStartMatchEvent(StartMatchRequest request)
    {
        var matchEvent = new MatchStartedEvent
        {
            HomeTeamId = request.HomeTeamId.Value,
            AwayTeamId = request.AwayTeamId.Value,
            HomeTeamName = request.HomeTeamName,
            AwayTeamName = request.AwayTeamName
        };
        var routingKey = $"worldcup.match.{request.MatchId}.{EventType.MatchStarted.ToString().ToLower()}";
        _producer.PublishMessage(matchEvent, ExchangeName, routingKey);
    }

    public void PublishEndMatchEvent(EndMatchRequest request)
    {
        var matchEvent = new MatchEndedEvent
        {
            MatchId = request.MatchId,
            FinalHomeScore = request.FinalHomeScore,
            FinalAwayScore = request.FinalAwayScore
        };
        var routingKey = $"worldcup.match.{request.MatchId}.{EventType.MatchEnded.ToString().ToLower()}";
        _producer.PublishMessage(matchEvent, ExchangeName, routingKey);
    }

    public void PublishGoalEvent(GoalRequest request)
    {
        var matchEvent = new GoalEvent
        {
            MatchId = request.MatchId,
            TeamId = request.TeamId,
            PlayerId = request.PlayerId,
            Minute = request.Minute
        };
        var routingKey = $"worldcup.match.{request.MatchId}.{EventType.Goal.ToString().ToLower()}";
        _producer.PublishMessage(matchEvent, ExchangeName, routingKey);
    }

    public void PublishCardEvent(CardRequest request)
    {
        var matchEvent = new CardEvent
        {
            MatchId = request.MatchId,
            TeamId = request.TeamId,
            PlayerId = request.PlayerId,
            CardType = request.CardType,
            Minute = request.Minute
        };
        var routingKey = $"worldcup.match.{request.MatchId}.{EventType.Card.ToString().ToLower()}";
        _producer.PublishMessage(matchEvent, ExchangeName, routingKey);
    }

    public void PublishSubstitutionEvent(SubstitutionRequest request)
    {
        var matchEvent = new SubstitutionEvent
        {
            MatchId = request.MatchId,
            TeamId = request.TeamId,
            PlayerInId = request.PlayerInId,
            PlayerOutId = request.PlayerOutId,
            Minute = request.Minute
        };
        var routingKey = $"worldcup.match.{request.MatchId}.{EventType.Substitution.ToString().ToLower()}";
        _producer.PublishMessage(matchEvent, ExchangeName, routingKey);
    }
}
