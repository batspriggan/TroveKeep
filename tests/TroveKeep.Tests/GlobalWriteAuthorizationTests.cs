using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using TroveKeep.Api.Filters;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Core.Models;
using TroveKeep.Services;
using Xunit;

namespace TroveKeep.Tests;

/// <summary>
/// The write rule of the global sticker library: a row with no collection cannot be authorized
/// against a collection, so the question becomes "may this caller write anywhere?"
/// (<see cref="ICollectionScope.CanWriteAnywhere"/>) and
/// <see cref="CollectionWriteAnywhereFilter"/> turns a "no" into 403.
/// </summary>
public class GlobalWriteAuthorizationTests
{
    private static CollectionScope Scope(FakeCurrentUser user, ICollectionMembershipCache cache) =>
        new(user, cache);

    [Fact]
    public void AdminWritesAnywhereEvenWithoutAnyMembership()
    {
        var scope = Scope(
            new FakeCurrentUser { Role = UserRole.Admin, CollectionIds = Array.Empty<Guid>() },
            FakeMembershipCache.With());

        Assert.True(scope.CanWriteAnywhere);
    }

    [Fact]
    public void AnEditorOrOwnerOfOneCollectionMayWriteTheGlobalLibrary()
    {
        var editor = Scope(
            new FakeCurrentUser { CollectionIds = new[] { Guid.NewGuid() } },
            FakeMembershipCache.With((Guid.NewGuid(), CollectionAccess.Editor)));
        Assert.True(editor.CanWriteAnywhere);

        var owner = Scope(
            new FakeCurrentUser { CollectionIds = new[] { Guid.NewGuid() } },
            FakeMembershipCache.With((Guid.NewGuid(), CollectionAccess.Owner)));
        Assert.True(owner.CanWriteAnywhere);
    }

    [Fact]
    public void AViewerOfEveryCollectionReadsButNeverWrites()
    {
        var collectionId = Guid.NewGuid();
        var scope = Scope(
            new FakeCurrentUser { CollectionIds = new[] { collectionId } },
            FakeMembershipCache.With((collectionId, CollectionAccess.Viewer)));

        Assert.False(scope.CanWriteAnywhere);
        // …and the per-collection rule agrees: a Viewer is read-only there too.
        Assert.False(scope.CanWrite(collectionId));
        Assert.True(scope.CanAccess(collectionId));
    }

    [Fact]
    public void AUserWithoutCollectionsMayNotWriteTheGlobalLibrary()
    {
        var scope = Scope(new FakeCurrentUser(), FakeMembershipCache.With());
        Assert.False(scope.CanWriteAnywhere);
    }

    [Fact]
    public void BeforeTheMembershipCacheIsLoadedNothingIsWritable()
    {
        // A code path outside the request pipeline: no level is known, so no write is allowed.
        var scope = Scope(
            new FakeCurrentUser(),
            new FakeMembershipCache
            {
                IsLoaded = false,
                AccessByCollection = new Dictionary<Guid, CollectionAccess>(),
            });

        Assert.False(scope.CanWriteAnywhere);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AWriteWithoutEditorAccessIsRefusedWith403(string method)
    {
        var (filter, context, nextCalled) = Build(method, canWriteAnywhere: false);
        await filter.OnActionExecutionAsync(context, () => nextCalled.OnActionExecutionAsync(context));

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(nextCalled.Invoked);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task ReadingTheLibraryIsOpenEvenForAViewer(string method)
    {
        var (filter, context, nextCalled) = Build(method, canWriteAnywhere: false);
        await filter.OnActionExecutionAsync(context, () => nextCalled.OnActionExecutionAsync(context));

        Assert.Null(context.Result);
        Assert.True(nextCalled.Invoked);
    }

    [Fact]
    public async Task AWriteByAnEditorReachesTheAction()
    {
        var (filter, context, nextCalled) = Build("POST", canWriteAnywhere: true);
        await filter.OnActionExecutionAsync(context, () => nextCalled.OnActionExecutionAsync(context));

        Assert.Null(context.Result);
        Assert.True(nextCalled.Invoked);
    }

    private static (CollectionWriteAnywhereFilter Filter, ActionExecutingContext Context, ProbeDelegate Next)
        Build(string method, bool canWriteAnywhere)
    {
        var filter = new CollectionWriteAnywhereFilter(
            new FakeScope(canWriteAnywhere),
            NullLogger<CollectionWriteAnywhereFilter>.Instance);

        var http = new DefaultHttpContext();
        http.Request.Method = method;
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());

        return (filter, context, new ProbeDelegate());
    }

    private sealed class FakeScope : ICollectionScope
    {
        public FakeScope(bool canWriteAnywhere) => CanWriteAnywhere = canWriteAnywhere;

        public bool CanAccessAll => false;
        public bool CanWriteAnywhere { get; }
        public bool HasExplicitCollectionSelection => false;
        public IReadOnlyCollection<Guid> VisibleCollectionIds => Array.Empty<Guid>();
        public IReadOnlyCollection<Guid> AccessibleCollectionIds => Array.Empty<Guid>();
        public Guid? DefaultCollectionId => null;
        public bool CanAccess(Guid collectionId) => false;
        public CollectionAccess? AccessFor(Guid collectionId) => null;
        public bool CanWrite(Guid collectionId) => false;
        public bool CanManageMembers(Guid collectionId) => false;
    }

    /// <summary>Records whether the pipeline continued.</summary>
    private sealed class ProbeDelegate
    {
        public bool Invoked { get; private set; }

        public Task<ActionExecutedContext> OnActionExecutionAsync(ActionExecutingContext context)
        {
            Invoked = true;
            return Task.FromResult(new ActionExecutedContext(
                context, context.Filters, context.Controller));
        }
    }
}
