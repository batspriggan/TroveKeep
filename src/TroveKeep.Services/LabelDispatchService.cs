using Microsoft.Extensions.DependencyInjection;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;

namespace TroveKeep.Services;

/// <summary>
/// Builds labels and routes them to their sink. In <b>client</b> mode it returns the
/// JSON (the UI downloads it); in <b>server</b> mode it posts each label to the remote
/// label-tool server, which prints it.
/// </summary>
public class LabelDispatchService : ILabelDispatchService
{
    private readonly ILabelPrintService _labelPrintService;
    private readonly ILabelServerClient _serverClient;
    private readonly ILabelPrintConfigRepository _configRepository;
    private readonly LabelPrintSettings _settings;

    private readonly ILegoSetService _setService;
    private readonly IBulkPieceService _pieceService;
    private readonly IBoxService _boxService;
    private readonly IDrawerContainerService _containerService;
    private readonly IColorRepository _colorRepository;
    private readonly ILabelTargetService _labelTargetService;
    private readonly ILabelImageResolver _serverImageResolver;
    private readonly ILabelImageResolver _clientImageResolver;

    public LabelDispatchService(
        ILabelPrintService labelPrintService,
        ILabelServerClient serverClient,
        ILabelPrintConfigRepository configRepository,
        LabelPrintSettings settings,
        ILegoSetService setService,
        IBulkPieceService pieceService,
        IBoxService boxService,
        IDrawerContainerService containerService,
        IColorRepository colorRepository,
        ILabelTargetService labelTargetService,
        [FromKeyedServices(LabelImageResolver.ServerKey)] ILabelImageResolver serverImageResolver,
        [FromKeyedServices(LabelImageResolver.ClientKey)] ILabelImageResolver clientImageResolver)
    {
        _labelPrintService = labelPrintService;
        _serverClient = serverClient;
        _configRepository = configRepository;
        _settings = settings;
        _setService = setService;
        _pieceService = pieceService;
        _boxService = boxService;
        _containerService = containerService;
        _colorRepository = colorRepository;
        _labelTargetService = labelTargetService;
        _serverImageResolver = serverImageResolver;
        _clientImageResolver = clientImageResolver;
    }

    // ---- Config ----

    public async Task<LabelPrintConfig> GetConfigAsync()
    {
        var persisted = await _configRepository.GetAsync();
        if (persisted is not null) return persisted;

        // No persisted config yet: fall back to the appsettings defaults.
        return new LabelPrintConfig
        {
            Mode = _settings.Mode == LabelSinkMode.Server ? "server" : "client",
            ServerUrl = _settings.ServerUrl,
            ServerToken = _settings.ServerToken,
        };
    }

    public async Task<LabelPrintConfig> SaveConfigAsync(LabelPrintConfig config)
    {
        var mode = string.Equals(config.Mode, "server", StringComparison.OrdinalIgnoreCase) ? "server" : "client";
        var normalized = new LabelPrintConfig
        {
            Mode = mode,
            ServerUrl = string.IsNullOrWhiteSpace(config.ServerUrl) ? null : config.ServerUrl.Trim().TrimEnd('/'),
            ServerToken = string.IsNullOrWhiteSpace(config.ServerToken) ? null : config.ServerToken.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _configRepository.SaveAsync(normalized);
        return normalized;
    }

    public async Task<LabelServerHealth?> GetServerHealthAsync()
    {
        var config = await GetConfigAsync();
        return config.ServerUrl is null
            ? null
            : await _serverClient.GetHealthAsync(config.ServerUrl, config.ServerToken);
    }

    public async Task<IReadOnlyList<LabelJob>?> GetJobsAsync(string? state = null, int limit = 100)
    {
        var config = await GetConfigAsync();
        return config.ServerUrl is null
            ? null
            : await _serverClient.GetJobsAsync(config.ServerUrl, config.ServerToken, state, limit);
    }

    public async Task<LabelJob?> RetryJobAsync(string jobId)
    {
        var config = await GetConfigAsync();
        return config.ServerUrl is null
            ? null
            : await _serverClient.RetryJobAsync(config.ServerUrl, config.ServerToken, jobId);
    }

    public async Task<IReadOnlyList<LabelFormat>?> GetFormatsAsync()
    {
        var config = await GetConfigAsync();
        return config.ServerUrl is null
            ? null
            : await _serverClient.GetFormatsAsync(config.ServerUrl, config.ServerToken);
    }

    // ---- Build + dispatch ----

    public async Task<PrintOutcome> PrintSetAsync(Guid setId, int? copies = null, string? size = null)
    {
        var set = await _setService.GetByIdAsync(setId);
        if (set is null) return NotFound("set");

        var config = await GetConfigAsync();
        var resolver = ResolverFor(config);
        var json = await _labelPrintService.BuildLegoSetLabel(set, resolver, copies, size);
        var label = new BuiltLabel(json, _labelPrintService.GetLegoSetFileName(set), size ?? _settings.DefaultSize);
        return await DispatchAsync(config, [label]);
    }

    public async Task<PrintOutcome> PrintBulkPieceAsync(Guid pieceId, int? copies = null)
    {
        var piece = await _pieceService.GetByIdAsync(pieceId);
        if (piece is null) return NotFound("bulk piece");

        var config = await GetConfigAsync();
        var labels = await BuildBulkPieceLocationLabelsAsync(piece, config, copies);
        return await DispatchAsync(config, labels);
    }

    public async Task<PrintOutcome> PrintBoxSummaryAsync(Guid boxId, int? copies = null)
    {
        var box = await _boxService.GetByIdWithContentsAsync(boxId);
        if (box is null) return NotFound("box");

        var config = await GetConfigAsync();
        var json = _labelPrintService.BuildBoxSummaryLabel(box, copies);
        return await DispatchAsync(config, [new BuiltLabel(json, _labelPrintService.GetBoxSummaryFileName(box), "large")]);
    }

    public async Task<PrintOutcome> PrintBoxQrAsync(Guid boxId, int? copies = null)
    {
        var box = await _boxService.GetByIdAsync(boxId);
        if (box is null) return NotFound("box");

        var config = await GetConfigAsync();
        var json = _labelPrintService.BuildBoxQrLabel(box, copies);
        return await DispatchAsync(config, [new BuiltLabel(json, _labelPrintService.GetBoxQrFileName(box), "small")]);
    }

    public async Task<PrintOutcome> PrintBoxPieceLabelsAsync(Guid boxId)
    {
        var box = await _boxService.GetByIdWithContentsAsync(boxId);
        if (box is null) return NotFound("box");

        var config = await GetConfigAsync();
        var resolver = ResolverFor(config);
        var colors = (await _colorRepository.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
        var boxKey = await _labelTargetService.GetOrCreateStorageKeyAsync(StorageType.Box, boxId);

        var labels = new List<BuiltLabel>();
        var index = 0;
        foreach (var piece in box.BulkPieces ?? [])
        {
            index++;
            colors.TryGetValue(piece.LegoColorId, out var colorName);
            var json = await _labelPrintService.BuildBulkPieceLocationLabel(piece, colorName, box.Name, resolver, qrValue: boxKey);
            labels.Add(new BuiltLabel(json, _labelPrintService.GetBulkPieceLocationFileName(piece, index), "small"));
        }

        return await DispatchAsync(config, labels);
    }

    public async Task<PrintOutcome> PrintContainerPieceLabelsAsync(Guid containerId)
    {
        var container = await _containerService.GetByIdWithDrawersAsync(containerId);
        if (container is null) return NotFound("container");

        var config = await GetConfigAsync();
        var resolver = ResolverFor(config);
        var colors = (await _colorRepository.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
        var labels = new List<BuiltLabel>();
        var index = 0;

        foreach (var drawer in container.Drawers ?? [])
        {
            var drawerKey = await _labelTargetService.GetOrCreateStorageKeyAsync(StorageType.Drawer, containerId, drawer.Position);
            var locationLine = $"{container.Name} - {drawer.Position}";

            // Group pieces in this drawer by design id: one label when the same design appears
            // in multiple colors here (shown as "Multiple").
            foreach (var group in (drawer.BulkPieces ?? []).GroupBy(p => p.LegoId))
            {
                index++;
                var rep = group.First();
                var distinctColors = group.Select(x => x.LegoColorId).Distinct().ToList();
                string colorName;
                if (distinctColors.Count == 1)
                {
                    colors.TryGetValue(distinctColors[0], out var single);
                    colorName = single ?? string.Empty;
                }
                else
                {
                    colorName = "Multiple";
                }

                var json = await _labelPrintService.BuildBulkPieceLocationLabel(rep, colorName, locationLine, resolver, qrValue: drawerKey);
                labels.Add(new BuiltLabel(json, _labelPrintService.GetBulkPieceLocationFileName(rep, index), "small"));
            }
        }

        return await DispatchAsync(config, labels);
    }

    private async Task<List<BuiltLabel>> BuildBulkPieceLocationLabelsAsync(BulkPiece piece, LabelPrintConfig config, int? copies)
    {
        var resolver = ResolverFor(config);
        var allocations = piece.StorageAllocations ?? [];
        var colors = (await _colorRepository.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
        colors.TryGetValue(piece.LegoColorId, out var colorName);

        var boxIds = allocations.Where(a => a.StorageType == StorageType.Box).Select(a => a.StorageId).Distinct().ToList();
        var containerIds = allocations.Where(a => a.StorageType == StorageType.Drawer).Select(a => a.StorageId).Distinct().ToList();
        var boxes = boxIds.Count > 0
            ? (await _boxService.GetAllAsync()).Where(b => boxIds.Contains(b.Id)).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Box>();
        var containerList = containerIds.Count > 0
            ? (await _containerService.GetAllAsync()).Where(c => containerIds.Contains(c.Id)).ToDictionary(c => c.Id)
            : new Dictionary<Guid, DrawerContainer>();

        var labels = new List<BuiltLabel>();
        var index = 0;
        foreach (var allocation in allocations)
        {
            index++;
            string locationLine;
            string qrValue;
            if (allocation.StorageType == StorageType.Box)
            {
                locationLine = boxes.GetValueOrDefault(allocation.StorageId)?.Name ?? "(unknown box)";
                qrValue = await _labelTargetService.GetOrCreateStorageKeyAsync(StorageType.Box, allocation.StorageId);
            }
            else
            {
                var container = containerList.GetValueOrDefault(allocation.StorageId);
                locationLine = $"{container?.Name ?? "(unknown container)"} - {allocation.StoragePosition}";
                qrValue = await _labelTargetService.GetOrCreateStorageKeyAsync(StorageType.Drawer, allocation.StorageId, allocation.StoragePosition);
            }

            var json = await _labelPrintService.BuildBulkPieceLocationLabel(piece, colorName, locationLine, resolver, copies, qrValue);
            labels.Add(new BuiltLabel(json, _labelPrintService.GetBulkPieceLocationFileName(piece, index), "small"));
        }

        return labels;
    }

    /// <summary>
    /// In server mode the label must be self-sufficient, so images are embedded inline
    /// (base64). In client mode the image is referenced by URL (existing behaviour).
    /// </summary>
    private ILabelImageResolver? ResolverFor(LabelPrintConfig config) =>
        config.Mode == "server" && config.ServerUrl is not null
            ? _serverImageResolver
            : _clientImageResolver;

    // ---- Dispatch ----

    private async Task<PrintOutcome> DispatchAsync(LabelPrintConfig config, IReadOnlyList<BuiltLabel> labels)
    {
        if (config.Mode != "server" || config.ServerUrl is null)
        {
            return new PrintOutcome(
                Sink: "client",
                Sent: 0,
                Rejected: 0,
                Uncertain: 0,
                Items: labels.Select(l => new PrintItemResult(l.FileName, "download", null, null)).ToList(),
                ServerUrl: config.ServerUrl,
                PrinterAvailable: null,
                Message: null);
        }

        var health = await _serverClient.GetHealthAsync(config.ServerUrl, config.ServerToken);

        var items = new List<PrintItemResult>();
        var sent = 0;
        var rejected = 0;
        var uncertain = 0;

        foreach (var label in labels)
        {
            var result = await _serverClient.PrintAsync(config.ServerUrl, config.ServerToken, label.Json);
            switch (result.Outcome)
            {
                case LabelDispatchOutcome.Accepted:
                    sent++;
                    items.Add(new PrintItemResult(label.FileName, "queued", result.JobId, null));
                    break;
                case LabelDispatchOutcome.Invalid:
                    rejected++;
                    items.Add(new PrintItemResult(label.FileName, "rejected", null, result.Error));
                    break;
                case LabelDispatchOutcome.Transient:
                case LabelDispatchOutcome.Uncertain:
                case LabelDispatchOutcome.Unreachable:
                    uncertain++;
                    items.Add(new PrintItemResult(label.FileName, "uncertain", null, result.Error));
                    break;
            }
        }

        var message = health is { Printer: null }
            ? "server raggiunto ma nessuna stampante collegata: i job verranno stampati quando la stampante torna disponibile"
            : null;

        return new PrintOutcome(
            Sink: "server",
            Sent: sent,
            Rejected: rejected,
            Uncertain: uncertain,
            Items: items,
            ServerUrl: config.ServerUrl,
            PrinterAvailable: health?.Printer is not null,
            Message: message);
    }

    private static PrintOutcome NotFound(string entity) =>
        new("none", 0, 0, 0, [], null, null, $"{entity} non trovato");
}
