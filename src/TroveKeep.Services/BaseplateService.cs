using System.Text.RegularExpressions;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class BaseplateService : IBaseplateService
{
    private readonly IBaseplateRepository _repo;

    public BaseplateService(IBaseplateRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<Baseplate>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Baseplate> CreateAsync(Baseplate baseplate) => _repo.CreateAsync(baseplate);
    public Task DeleteAsync(Guid id) => _repo.DeleteAsync(id);
    // Matches patterns like "48 x 48", "16 x 32", "24X32", "48×48"
    private static readonly Regex _studPattern =
        new(@"(\d+)\s*[xX×]\s*(\d+)", RegexOptions.Compiled);

    public (int studX, int studY) GuessStudDimensions(string partDescription)
    {
        var match = _studPattern.Match(partDescription);
        if (!match.Success) return (0, 0);
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }
}
