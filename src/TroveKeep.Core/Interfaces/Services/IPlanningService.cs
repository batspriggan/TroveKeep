using TroveKeep.Core.Models;

namespace TroveKeep.Core.Interfaces.Services;

public interface IPlanningService
{
    Task<BaseplateFeasibilityResult> CalculateFeasibilityAsync(IEnumerable<FeasibilityAggregateRef> aggregates);
}
