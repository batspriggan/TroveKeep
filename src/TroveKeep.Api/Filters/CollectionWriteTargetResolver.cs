using Microsoft.AspNetCore.Mvc;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;

namespace TroveKeep.Api.Filters;

/// <summary>
/// Default <see cref="ICollectionWriteTargetResolver"/>: a controller/action table that maps a write
/// request to the collection it modifies.
///
/// <para>
/// <b>Resolution strategy</b> — three cases, in this order:
/// </para>
/// <list type="number">
///   <item>
///     <b>The route names an existing row</b> (<c>/api/sets/{id}</c>, <c>/api/drawers/{containerId}/…</c>):
///     the row is loaded through its repository and its <c>CollectionId</c> is the target. The
///     lookups are the repository <c>GetByIdAsync</c>, which is already scoped to the caller's
///     accessible collections, so a non-member's id resolves to nothing and is left to the action's
///     own 404.
///   </item>
///   <item>
///     <b>The route names no row</b> (a create such as <c>POST /api/sets</c>): the target is
///     <see cref="ICollectionScope.DefaultCollectionId"/> — the explicit
///     <c>X-TroveKeep-Collection</c> selection, or the caller's only collection. No request DTO
///     carries a <c>CollectionId</c>: creation resolves it in the repository through the same
///     helper, so the filter and the write agree by construction. When the default is ambiguous
///     (multi-collection user, no selection) the target stays unknown and the action answers
///     exactly as before (400 from the repository).
///   </item>
///   <item>
///     <b>Neither applies</b>: the action is collection-agnostic and must be listed below.
///   </item>
/// </list>
///
/// <para>
/// <b>Excluded controllers</b> — no collection-scoped enforcement, each for the stated reason:
/// </para>
/// <list type="bullet">
///   <item><c>AuthController</c> — login/logout/change-password act on the caller's own session and credentials.</item>
///   <item><c>SetupController</c> — anonymous first-run setup; guarded by the one-shot setup token, not by a collection.</item>
///   <item><c>ModeController</c> — admin-only switch of process-wide single/multi-user state; not collection data.</item>
///   <item><c>UsersController</c> — admin-only global accounts; a user is not owned by a collection.</item>
///   <item><c>SettingsController</c> — whole-database backup/restore; global by nature (restore is admin-only).</item>
///   <item><c>ArchivesController</c> — global Rebrickable reference data (colors, parts, sets, categories) shared by every collection.</item>
///   <item><c>LabelsController</c> — print dispatch (reads entities, persists nothing) and the global label-server settings.</item>
///   <item><c>ScannerController</c>, <c>SearchController</c> — read-only, no write endpoints.</item>
///   <item>
///     <c>CollectionsController</c> — handled by the controller itself, because the rule is not
///     <see cref="ICollectionScope.CanWrite"/>: collection create/delete/edit is admin-only
///     (<c>[Authorize(Roles = "Admin")]</c>) while member management needs
///     <see cref="ICollectionScope.CanManageMembers"/> (admin or Owner), which an Editor must fail.
///   </item>
/// </list>
///
/// <para>
/// <b>Excluded actions</b> of otherwise guarded controllers:
/// <c>BaseplatesController.Feasibility</c> — a read-only capacity calculation that persists nothing,
/// so a Viewer of the collection may run it (Build Check is a read for the user).
/// </para>
/// </summary>
public sealed class CollectionWriteTargetResolver : ICollectionWriteTargetResolver
{
    private static readonly HashSet<string> ExcludedControllers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Auth", "Setup", "Mode", "Users", "Settings", "Archives", "Labels",
            "Scanner", "Search", "Collections",
        };

    /// <summary>Write-shaped actions that only read, keyed "Controller.Action".</summary>
    private static readonly HashSet<string> ExcludedActions =
        new(StringComparer.OrdinalIgnoreCase) { "Baseplates.Feasibility" };

    private readonly ILegoSetRepository _sets;
    private readonly IBulkPieceRepository _bulkPieces;
    private readonly IBoxRepository _boxes;
    private readonly IDrawerContainerRepository _containers;
    private readonly IRoomRepository _rooms;
    private readonly IBaseplateRepository _baseplates;
    private readonly ITableTemplateRepository _templates;
    private readonly ICollectionScope _scope;
    private readonly ILogger<CollectionWriteTargetResolver> _logger;

    public CollectionWriteTargetResolver(
        ILegoSetRepository sets,
        IBulkPieceRepository bulkPieces,
        IBoxRepository boxes,
        IDrawerContainerRepository containers,
        IRoomRepository rooms,
        IBaseplateRepository baseplates,
        ITableTemplateRepository templates,
        ICollectionScope scope,
        ILogger<CollectionWriteTargetResolver> logger)
    {
        _sets = sets;
        _bulkPieces = bulkPieces;
        _boxes = boxes;
        _containers = containers;
        _rooms = rooms;
        _baseplates = baseplates;
        _templates = templates;
        _scope = scope;
        _logger = logger;
    }

    public async Task<Guid?> ResolveAsync(ActionContext context)
    {
        var controller = RouteValue(context, "controller");
        if (string.IsNullOrEmpty(controller)) return null;

        if (ExcludedControllers.Contains(controller)) return null;

        var action = RouteValue(context, "action");
        if (!string.IsNullOrEmpty(action) && ExcludedActions.Contains($"{controller}.{action}"))
            return null;

        return controller.ToLowerInvariant() switch
        {
            "sets" => await ResolveByIdAsync(context, "id",
                async id => (await _sets.GetByIdAsync(id))?.CollectionId),
            "bulkpieces" => await ResolveByIdAsync(context, "id",
                async id => (await _bulkPieces.GetByIdAsync(id))?.CollectionId),
            "boxes" => await ResolveByIdAsync(context, "id",
                async id => (await _boxes.GetByIdAsync(id))?.CollectionId),
            "drawercontainers" => await ResolveByIdAsync(context, "id",
                async id => (await _containers.GetByIdAsync(id))?.CollectionId),
            // A drawer is embedded in its container (keyed by position, no id of its own): the
            // container carries the CollectionId.
            "drawers" => await ResolveByIdAsync(context, "containerId",
                async id => (await _containers.GetByIdAsync(id))?.CollectionId),
            "rooms" => await ResolveByIdAsync(context, "id",
                async id => (await _rooms.GetByIdAsync(id))?.CollectionId),
            "baseplates" => await ResolveByIdAsync(context, "id",
                async id => (await _baseplates.GetByIdAsync(id))?.CollectionId),
            "tabletemplates" => await ResolveByIdAsync(context, "id",
                async id => (await _templates.GetByIdAsync(id))?.CollectionId),
            _ => Unknown(controller, action),
        };
    }

    /// <summary>
    /// Loads the row named by <paramref name="routeKey"/> and returns its collection, or falls back
    /// to the caller's default collection when the route carries no id (a create).
    /// </summary>
    private async Task<Guid?> ResolveByIdAsync(
        ActionContext context,
        string routeKey,
        Func<Guid, Task<Guid?>> loadCollectionIdAsync)
    {
        if (!TryGetRouteGuid(context, routeKey, out var id))
            return _scope.DefaultCollectionId;

        return await loadCollectionIdAsync(id);
    }

    private Guid? Unknown(string controller, string? action)
    {
        // Not a mapped controller and not a declared exclusion: a new write controller was added
        // without classifying it. Allow the request (so the release does not break) but make the
        // omission loud instead of silently leaving the endpoint unguarded.
        _logger.LogWarning(
            "No collection write target mapping for {Controller}/{Action}: the write is not " +
            "authorized against the collection access level. Classify it in CollectionWriteTargetResolver.",
            controller, action ?? "?");
        return null;
    }

    private static string? RouteValue(ActionContext context, string key) =>
        context.RouteData.Values.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool TryGetRouteGuid(ActionContext context, string key, out Guid id)
    {
        id = Guid.Empty;
        return context.RouteData.Values.TryGetValue(key, out var value)
               && Guid.TryParse(value?.ToString(), out id);
    }
}
