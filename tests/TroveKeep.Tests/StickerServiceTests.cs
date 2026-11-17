using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Services;
using Xunit;

namespace TroveKeep.Tests;

/// <summary>
/// Business rules of the sticker library: a non-empty name of at most 80 characters and a
/// footprint between 1 and 32 studs on each side. No database: the repository is faked.
/// </summary>
public class StickerServiceTests
{
    private static (StickerService Service, FakeStickerRepository Repo) Build()
    {
        var repo = new FakeStickerRepository();
        return (new StickerService(repo, new FakeImageRepository()), repo);
    }

    [Fact]
    public async Task TrimsTheNameAndAcceptsATileFootprint()
    {
        var (service, _) = Build();

        var created = await service.CreateAsync(new Sticker
        {
            Name = "  Red apple  ",
            WidthStuds = 2,
            DepthStuds = 2,
        });

        Assert.Equal("Red apple", created.Name);
        Assert.Equal(2, created.WidthStuds);
        Assert.Equal(2, created.DepthStuds);
        // A new sticker has no artwork until one is uploaded.
        Assert.False(created.ImageCached);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnEmptyName(string name)
    {
        var (service, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new Sticker { Name = name, WidthStuds = 1, DepthStuds = 1 }));
    }

    [Fact]
    public async Task RefusesANameLongerThanEightyCharacters()
    {
        var (service, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new Sticker
            {
                Name = new string('x', StickerService.MaxNameLength + 1),
                WidthStuds = 1,
                DepthStuds = 1,
            }));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(33, 1)]
    [InlineData(1, 33)]
    [InlineData(-2, 4)]
    public async Task RefusesAFootprintOutsideOneToThirtyTwoStuds(int width, int depth)
    {
        var (service, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new Sticker { Name = "Nope", WidthStuds = width, DepthStuds = depth }));
    }

    [Fact]
    public async Task AcceptsTheExtremesOfTheFootprint()
    {
        var (service, _) = Build();
        var small = await service.CreateAsync(new Sticker { Name = "1", WidthStuds = 1, DepthStuds = 1 });
        var big = await service.CreateAsync(new Sticker { Name = "2", WidthStuds = 32, DepthStuds = 32 });

        Assert.Equal(1, small.WidthStuds);
        Assert.Equal(32, big.DepthStuds);
    }

    [Fact]
    public async Task UpdatingAMissingStickerIsA404()
    {
        var (service, _) = Build();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(Guid.NewGuid(), new Sticker { Name = "X", WidthStuds = 1, DepthStuds = 1 }));
    }

    [Fact]
    public async Task DeletingAStickerAlsoDeletesItsArtwork()
    {
        var repo = new FakeStickerRepository();
        var images = new FakeImageRepository();
        var service = new StickerService(repo, images);

        var sticker = await service.CreateAsync(new Sticker { Name = "Leaf", WidthStuds = 1, DepthStuds = 2 });
        Assert.True(await service.DeleteAsync(sticker.Id));

        Assert.Equal(sticker.Id.ToString(), images.DeletedReference);
        Assert.Equal(ImageReferenceType.Sticker, images.DeletedType);
    }
}

internal sealed class FakeStickerRepository : IStickerRepository
{
    private readonly Dictionary<Guid, Sticker> _rows = new();

    public Task<IEnumerable<Sticker>> GetAllAsync() => Task.FromResult<IEnumerable<Sticker>>(_rows.Values);

    public Task<Sticker?> GetByIdAsync(Guid id) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? row : null);

    public Task<Sticker> CreateAsync(Sticker sticker)
    {
        sticker.Id = sticker.Id == Guid.Empty ? Guid.NewGuid() : sticker.Id;
        sticker.Version = 0;
        _rows[sticker.Id] = sticker;
        return Task.FromResult(sticker);
    }

    public Task<Sticker?> UpdateAsync(Sticker sticker)
    {
        if (!_rows.ContainsKey(sticker.Id)) return Task.FromResult<Sticker?>(null);
        _rows[sticker.Id] = sticker;
        return Task.FromResult<Sticker?>(sticker);
    }

    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(_rows.Remove(id));

    public Task<Sticker?> UpdateImageCachedAsync(Guid id, bool cached)
    {
        if (!_rows.TryGetValue(id, out var row)) return Task.FromResult<Sticker?>(null);
        row.ImageCached = cached;
        return Task.FromResult<Sticker?>(row);
    }
}

internal sealed class FakeImageRepository : IImageRepository
{
    public string? DeletedReference { get; private set; }
    public ImageReferenceType? DeletedType { get; private set; }

    public Task<Image?> GetAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null) =>
        Task.FromResult<Image?>(null);

    public Task StoreAsync(Image image) => Task.CompletedTask;

    public Task DeleteAsync(string referenceNumber, ImageReferenceType referenceType, int? colorId = null)
    {
        DeletedReference = referenceNumber;
        DeletedType = referenceType;
        return Task.CompletedTask;
    }
}
