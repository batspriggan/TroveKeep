using System.IO.Compression;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

public class ArchiveService : IArchiveService
{
    private readonly IColorRepository _colorRepository;
    private readonly ISetArchiveRepository _setRepository;
    private readonly IPartArchiveRepository _partRepository;

    private readonly IPartInventoryArchiveRepository _partInventoryRepository;

    public ArchiveService(
        IColorRepository colorRepository,
         ISetArchiveRepository setRepository,
          IPartArchiveRepository partRepository,
           IPartInventoryArchiveRepository partInventoryRepository)
    {
        _colorRepository = colorRepository;
        _setRepository = setRepository;
        _partRepository = partRepository;
        _partInventoryRepository = partInventoryRepository;
    }

    public async Task<(int count, DateTimeOffset importedAt)> ImportColorsAsync(Stream stream)
    {
        var colors = new List<RebrickableColor>();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside the uploaded zip.");

        using var csvStream = csvEntry.Open();
        using var reader = new StreamReader(csvStream);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty.");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < headers.Length) continue;

            colors.Add(new RebrickableColor
            {
                Id = int.Parse(fields[colIndex["id"]].Trim()),
                Name = fields[colIndex["name"]].Trim(),
                Rgb = fields[colIndex["rgb"]].Trim(),
                IsTrans = fields[colIndex["is_trans"]].Trim().Equals("True", StringComparison.OrdinalIgnoreCase),
                StartYear = ParseNullableInt(fields[colIndex["y1"]].Trim()),
                EndYear = ParseNullableInt(fields[colIndex["y2"]].Trim()),
            });
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

    public Task<IEnumerable<RebrickableColor>> GetColorsAsync()
        => _colorRepository.GetAllAsync();

    public async Task<(int count, DateTimeOffset importedAt)> ImportSetsAsync(Stream stream)
    {
        var sets = new List<RebrickableSet>();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside the uploaded zip.");

        using var csvStream = csvEntry.Open();
        using var reader = new StreamReader(csvStream);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty.");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < headers.Length) continue;

            sets.Add(new RebrickableSet
            {
                SetNum = fields[colIndex["set_num"]].Trim(),
                Name = fields[colIndex["name"]].Trim(),
                Year = int.TryParse(fields[colIndex["year"]].Trim(), out var y) ? y : 0,
                ThemeId = int.TryParse(fields[colIndex["theme_id"]].Trim(), out var t) ? t : 0,
                NumParts = int.TryParse(fields[colIndex["num_parts"]].Trim(), out var n) ? n : 0,
                ImgUrl = fields[colIndex["img_url"]].Trim(),
            });
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

    public Task<IEnumerable<RebrickableSet>> SearchSetsAsync(string query, int limit)
        => _setRepository.SearchAsync(query, limit);

    public async Task<(int count, DateTimeOffset importedAt)> ImportPartsAsync(Stream stream)
    {
        var parts = new List<RebrickablePart>();

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside the uploaded zip.");

        using var csvStream = csvEntry.Open();
        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty.");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < 2) continue;

            parts.Add(new RebrickablePart
            {
                PartNum = fields[colIndex["part_num"]].Trim(),
                Name = fields[colIndex["name"]].Trim(),
            });
        }

        var count = await _partRepository.ReplaceAllAsync(parts);
        var importedAt = await _partRepository.GetLastImportedAtAsync();
        return (count, importedAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<(int count, DateTimeOffset? importedAt)> GetPartsStatusAsync()
    {
        var count = await _partRepository.GetCountAsync();
        var importedAt = await _partRepository.GetLastImportedAtAsync();
        return (count, importedAt);
    }

    public Task<IEnumerable<RebrickablePart>> SearchPartsAsync(string query, int limit)
        => _partRepository.SearchAsync(query, limit);

    public async Task<(int count, DateTimeOffset importedAt)> ImportPartsInventoryAsync(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var csvEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

        if (csvEntry is null)
            throw new InvalidOperationException("No CSV file found inside the uploaded zip.");

        using var csvStream = csvEntry.Open();
        using var reader = new StreamReader(csvStream);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            throw new InvalidOperationException("CSV file is empty.");

        var headers = SplitCsvLine(headerLine);
        var colIndex = BuildColumnIndex(headers);

        string? line;
        var uniqueParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Length < 2) continue;

            var colorId = int.Parse(fields[colIndex["color_id"]].Trim());
            var isSpare = fields[colIndex["is_spare"]].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
            if (colorId != 0 || isSpare)
                continue;
            var partNum = fields[colIndex["part_num"]].Trim();
            if (uniqueParts.ContainsKey(partNum))
                continue;
            uniqueParts[partNum] = fields[colIndex["img_url"]].Trim();
        }

        var count = await _partInventoryRepository.ReplaceAllAsync(uniqueParts.Select(kv => new RebrickablePartInventory
        {
            PartNum = kv.Key,
            ImgUrl = kv.Value,
        }));
        var importedAt = await _partInventoryRepository.GetLastImportedAtAsync();
        return (count, importedAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<(int count, DateTimeOffset? importedAt)> GetPartsInventoryStatusAsync()
    {
        var count = await _partInventoryRepository.GetCountAsync();
        var importedAt = await _partInventoryRepository.GetLastImportedAtAsync();
        return (count, importedAt);
    }

    public Task<IEnumerable<RebrickablePartInventory>> SearchPartsInventoryAsync(string query, int limit)
        => _partInventoryRepository.SearchAsync(query, limit);

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
