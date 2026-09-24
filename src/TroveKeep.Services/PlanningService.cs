using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Computes the "build check": given a set of aggregates (room + representativeId),
/// how many baseplates per catalogue row are needed versus what is available
/// (Quantity - Reservations). Mirrors the BFS aggregate detection of the Vue views.
/// </summary>
public class PlanningService : IPlanningService
{
    private const double AdjacencyToleranceCm = 0.5;

    private readonly IRoomRepository _roomRepo;
    private readonly IBaseplateRepository _baseplateRepo;
    private readonly ITableTemplateRepository _templateRepo;

    public PlanningService(IRoomRepository roomRepo, IBaseplateRepository baseplateRepo,
        ITableTemplateRepository templateRepo)
    {
        _roomRepo = roomRepo;
        _baseplateRepo = baseplateRepo;
        _templateRepo = templateRepo;
    }

    public async Task<BaseplateFeasibilityResult> CalculateFeasibilityAsync(IEnumerable<FeasibilityAggregateRef> aggregates)
    {
        var rooms = (await _roomRepo.GetAllAsync()).ToList();
        var baseplates = (await _baseplateRepo.GetAllAsync()).ToList();
        var templates = (await _templateRepo.GetAllAsync()).ToList();

        var bpById = baseplates.ToDictionary(b => b.Id);
        var tplById = templates.ToDictionary(t => t.Id);

        var need = new Dictionary<Guid, int>();

        foreach (var selection in aggregates)
        {
            var room = rooms.FirstOrDefault(r => r.Id == selection.RoomId);
            if (room is null) continue;

            var representativeId = ResolveRepresentativeId(room, selection.RepresentativeId, tplById);
            var layout = room.AggregateBpLayouts.FirstOrDefault(l => l.RepresentativeId == representativeId);
            if (layout is null) continue;

            foreach (var placed in layout.PlacedBaseplates)
            {
                // Referenced baseplates no longer in the catalogue are skipped.
                if (!bpById.ContainsKey(placed.BaseplateId)) continue;
                need[placed.BaseplateId] = need.GetValueOrDefault(placed.BaseplateId) + 1;
            }
        }

        var lines = new List<BaseplateFeasibilityLine>();
        foreach (var (bpId, count) in need)
        {
            var bp = bpById[bpId];
            var reserved = bp.Reservations.Sum(r => r.Quantity);
            var available = Math.Max(0, bp.Quantity - reserved);
            var deficit = Math.Max(0, count - available);
            lines.Add(new BaseplateFeasibilityLine
            {
                BaseplateId = bp.Id,
                Name = bp.Name,
                Type = bp.Type.ToString(),
                Need = count,
                Quantity = bp.Quantity,
                Reserved = reserved,
                Available = available,
                Deficit = deficit,
                Status = deficit > 0 ? "short" : "ok",
            });
        }

        lines = lines
            .OrderByDescending(l => l.Deficit)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.BaseplateId)
            .ToList();

        return new BaseplateFeasibilityResult
        {
            Lines = lines,
            TotalDeficit = lines.Sum(l => l.Deficit),
            HasShortage = lines.Any(l => l.Deficit > 0),
        };
    }

    /// <summary>Recomputes the BFS aggregate containing the requested representative id.</summary>
    private static string ResolveRepresentativeId(Room room, string requestedRepresentativeId,
        IReadOnlyDictionary<Guid, TableTemplate> tplById)
    {
        foreach (var group in ComputeAggregates(room.Layout, tplById))
        {
            var repId = AggregateRepresentativeId(group);
            if (repId == requestedRepresentativeId) return repId;
        }
        return requestedRepresentativeId;
    }

    private static IEnumerable<List<Guid>> ComputeAggregates(List<PlacedTable> layout,
        IReadOnlyDictionary<Guid, TableTemplate> tplById)
    {
        var n = layout.Count;
        var visited = new bool[n];
        for (var i = 0; i < n; i++)
        {
            if (visited[i]) continue;
            if (!tplById.ContainsKey(layout[i].TemplateId)) { visited[i] = true; continue; }

            var group = new List<Guid>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                group.Add(layout[cur].InstanceId);
                var a = layout[cur];
                if (!tplById.TryGetValue(a.TemplateId, out var tplA)) continue;
                var (wA, hA) = EffectiveSize(a, tplA);

                for (var j = 0; j < n; j++)
                {
                    if (visited[j]) continue;
                    var b = layout[j];
                    if (!tplById.TryGetValue(b.TemplateId, out var tplB)) continue;
                    var (wB, hB) = EffectiveSize(b, tplB);
                    if (AreAdjacent(a, wA, hA, b, wB, hB))
                    {
                        visited[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }
            yield return group;
        }
    }

    private static (double W, double H) EffectiveSize(PlacedTable t, TableTemplate tpl) =>
        t.Rotation % 180 == 0 ? (tpl.WidthCm, tpl.DepthCm) : (tpl.DepthCm, tpl.WidthCm);

    private static bool RangeOverlaps(double a1, double a2, double b1, double b2) =>
        Math.Min(a2, b2) - Math.Max(a1, b1) > 0;

    private static bool AreAdjacent(PlacedTable a, double wA, double hA, PlacedTable b, double wB, double hB)
    {
        var t = AdjacencyToleranceCm;
        var xAdj =
            (Math.Abs(a.XCm + wA - b.XCm) < t || Math.Abs(b.XCm + wB - a.XCm) < t) &&
            RangeOverlaps(a.YCm, a.YCm + hA, b.YCm, b.YCm + hB);
        var yAdj =
            (Math.Abs(a.YCm + hA - b.YCm) < t || Math.Abs(b.YCm + hB - a.YCm) < t) &&
            RangeOverlaps(a.XCm, a.XCm + wA, b.XCm, b.XCm + wB);
        return xAdj || yAdj;
    }

    private static string AggregateRepresentativeId(IEnumerable<Guid> group) =>
        group.Select(g => g.ToString()).OrderBy(s => s, StringComparer.Ordinal).First();
}
