using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class ArchiveService : IArchiveService
{
    private readonly IColorRepository _colorRepository;
    private readonly ISetArchiveRepository _setRepository;
    private readonly string _archivesPath;

    public ArchiveService(IColorRepository colorRepository, ISetArchiveRepository setRepository, IConfiguration configuration, IHostEnvironment env)
    {
        _colorRepository = colorRepository;
        _setRepository = setRepository;
        var relative = configuration["ArchivesPath"] ?? "archives";
        _archivesPath = Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(env.ContentRootPath, relative);
    }

    public async Task<(int count, DateTimeOffset importedAt)> ImportColorsAsync()
    {
        var zipPath = Path.Combine(_archivesPath, "colors.csv.zip");
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("colors.csv.zip not found in archives folder", zipPath);

        var colors = new List<RebrickableColor>();

        using var archive = ZipFile.OpenRead(zipPath);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside colors.csv.zip");

        using var stream = csvEntry.Open();
        using var reader = new StreamReader(stream);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < headers.Length) continue;

            var color = new RebrickableColor
            {
                Id = int.Parse(fields[colIndex["id"]].Trim()),
                Name = fields[colIndex["name"]].Trim(),
                Rgb = fields[colIndex["rgb"]].Trim(),
                IsTrans = fields[colIndex["is_trans"]].Trim().Equals("True", StringComparison.OrdinalIgnoreCase),
                StartYear = ParseNullableInt(fields[colIndex["y1"]].Trim()),
                EndYear = ParseNullableInt(fields[colIndex["y2"]].Trim()),
            };
            colors.Add(color);
        }

        var count = await _colorRepository.ReplaceAllAsync(colors);
        var importedAt = await _colorRepository.GetLastImportedAtAsync();
        return (count, importedAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<(int count, DateTimeOffset? importedAt)> GetColorsStatusAsync()
    {
        var colors = await _colorRepository.GetAllAsync();
        var count = colors.Count();
        var importedAt = await _colorRepository.GetLastImportedAtAsync();
        return (count, importedAt);
    }

    public Task<IEnumerable<RebrickableColor>> GetColorsAsync() =>
        _colorRepository.GetAllAsync();

    public async Task<(int count, DateTimeOffset importedAt)> ImportSetsAsync()
    {
        var zipPath = Path.Combine(_archivesPath, "sets.csv.zip");
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("sets.csv.zip not found in archives folder", zipPath);

        var sets = new List<RebrickableSet>();

        using var archive = ZipFile.OpenRead(zipPath);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside sets.csv.zip");

        using var stream = csvEntry.Open();
        using var reader = new StreamReader(stream);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < headers.Length) continue;

            var set = new RebrickableSet
            {
                SetNum = fields[colIndex["set_num"]].Trim(),
                Name = fields[colIndex["name"]].Trim(),
                Year = int.TryParse(fields[colIndex["year"]].Trim(), out var y) ? y : 0,
                ThemeId = int.TryParse(fields[colIndex["theme_id"]].Trim(), out var t) ? t : 0,
                NumParts = int.TryParse(fields[colIndex["num_parts"]].Trim(), out var n) ? n : 0,
                ImgUrl = fields[colIndex["img_url"]].Trim(),
            };
            sets.Add(set);
        }

        var count = await _setRepository.ReplaceAllAsync(sets);
        var importedAt = await _setRepository.GetLastImportedAtAsync();
        return (count, importedAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<(int count, DateTimeOffset? importedAt)> GetSetsStatusAsync()
    {
        var sets = await _setRepository.GetAllAsync();
        var count = sets.Count();
        var importedAt = await _setRepository.GetLastImportedAtAsync();
        return (count, importedAt);
    }

    public Task<IEnumerable<RebrickableSet>> SearchSetsAsync(string query, int limit) =>
        _setRepository.SearchAsync(query, limit);

    private static Dictionary<string, int> BuildColumnIndex(string[] headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
            index[headers[i].Trim()] = i;
        return index;
    }

    private static int? ParseNullableInt(string value) =>
        string.IsNullOrEmpty(value) ? null : int.TryParse(value, out var n) ? n : null;

    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
            else { current.Append(c); }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
