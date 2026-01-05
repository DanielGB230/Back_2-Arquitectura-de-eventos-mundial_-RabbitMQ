using Backend.Productor.Api.Dtos;

namespace Backend.Productor.Api.Services;

public interface IMatchEventService
{
    void PublishStartMatchEvent(StartMatchRequest request);
    void PublishEndMatchEvent(EndMatchRequest request);
    void PublishGoalEvent(GoalRequest request);
    void PublishCardEvent(CardRequest request);
    void PublishSubstitutionEvent(SubstitutionRequest request);
}
