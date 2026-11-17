using Microsoft.AspNetCore.Mvc;
using TroveKeep.Api.Controllers;
using TroveKeep.Api.DTOs.Requests;
using TroveKeep.Api.DTOs.Responses;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Models;
using TroveKeep.Services;
using Xunit;

namespace TroveKeep.Tests;

/// <summary>
/// Business rules of the sticker feature: a name of at most 80 characters for images and projects,
/// a footprint between 1 and 32 studs per side, 1..999 copies, no duplicate footprint for the same
/// artwork inside a project, no reference to artwork that does not exist, no deleting artwork a
/// project still prints, <see cref="StickerFit.Contain"/> as the default fit, and
/// <see cref="StickerOrientation.Auto"/> as the default artwork orientation.
/// No database: the repositories are faked.
/// </summary>
public class StickerServiceTests
{
    // ---- image library ------------------------------------------------------------------------

    [Fact]
    public async Task TrimsTheImageNameAndStartsWithoutArtwork()
    {
        var images = new FakeStickerImageRepository();
        var service = BuildImageService(images);

        var created = await service.CreateAsync(new StickerImage { Name = "  Red apple  " });

        Assert.Equal("Red apple", created.Name);
        Assert.False(created.ImageCached);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnEmptyImageName(string name)
    {
        var service = BuildImageService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerImage { Name = name }));
    }

    [Fact]
    public async Task RefusesAnImageNameLongerThanEightyCharacters()
    {
        var service = BuildImageService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerImage
            {
                Name = new string('x', StickerRules.MaxNameLength + 1),
            }));
    }

    [Fact]
    public async Task DeletingAnImageAlsoDeletesItsArtwork()
    {
        var images = new FakeImageRepository();
        var repo = new FakeStickerImageRepository();
        var service = new StickerImageService(repo, new FakeStickerProjectRepository(), images);

        var image = await service.CreateAsync(new StickerImage { Name = "Leaf" });
        Assert.True(await service.DeleteAsync(image.Id));

        Assert.Equal(image.Id.ToString(), images.DeletedReference);
        Assert.Equal(ImageReferenceType.StickerImage, images.DeletedType);
    }

    [Fact]
    public async Task RefusesToDeleteAnImageThatAProjectItemStillPrints()
    {
        var repo = new FakeStickerImageRepository();
        var projects = new FakeStickerProjectRepository();
        var service = new StickerImageService(repo, projects, new FakeImageRepository());

        var image = await service.CreateAsync(new StickerImage { Name = "Leaf" });
        projects.Rows.Add(new StickerProject
        {
            Id = Guid.NewGuid(),
            Name = "Sheet",
            Items =
            [
                new StickerItem { Id = Guid.NewGuid(), ImageId = image.Id, WidthStuds = 1, DepthStuds = 2 },
                new StickerItem { Id = Guid.NewGuid(), ImageId = image.Id, WidthStuds = 2, DepthStuds = 2 },
            ],
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(image.Id));
        Assert.Contains(image.Id.ToString(), error.Message);
        Assert.Contains("2 project item(s)", error.Message);
        // The row is untouched: the user removes the items first.
        Assert.NotNull(await service.GetByIdAsync(image.Id));
    }

    [Fact]
    public async Task UpdatingAMissingImageIsA404()
    {
        var service = BuildImageService();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(Guid.NewGuid(), new StickerImage { Name = "X" }));
    }

    // ---- projects -----------------------------------------------------------------------------

    [Fact]
    public async Task TrimsTheProjectNameAndAcceptsAnEmptyItemList()
    {
        var (service, _) = BuildProjectService();

        var created = await service.CreateAsync(new StickerProject { Name = "  Kitchen tiles  " });

        Assert.Equal("Kitchen tiles", created.Name);
        Assert.Empty(created.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnEmptyProjectName(string name)
    {
        var (service, _) = BuildProjectService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject { Name = name }));
    }

    [Fact]
    public async Task RefusesAProjectNameLongerThanEightyCharacters()
    {
        var (service, _) = BuildProjectService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = new string('x', StickerRules.MaxNameLength + 1),
            }));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(33, 1)]
    [InlineData(1, 33)]
    [InlineData(-2, 4)]
    public async Task RefusesAnItemFootprintOutsideOneToThirtyTwoStuds(int width, int depth)
    {
        var (service, images) = BuildProjectService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = "Nope",
                Items = [new StickerItem { ImageId = images.Add("Art"), WidthStuds = width, DepthStuds = depth }],
            }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(-1)]
    public async Task RefusesACopyCountOutsideOneToNineHundredNinetyNine(int copies)
    {
        var (service, images) = BuildProjectService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = "Nope",
                Items =
                [
                    new StickerItem
                    {
                        ImageId = images.Add("Art"),
                        WidthStuds = 1,
                        DepthStuds = 1,
                        Copies = copies,
                    },
                ],
            }));
    }

    [Fact]
    public async Task AcceptsTheExtremesOfTheFootprintAndTheCopyCount()
    {
        var (service, images) = BuildProjectService();

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Extremes",
            Items =
            [
                new StickerItem { ImageId = images.Add("A"), WidthStuds = 1, DepthStuds = 1, Copies = 1 },
                new StickerItem { ImageId = images.Add("B"), WidthStuds = 32, DepthStuds = 32, Copies = 999 },
            ],
        });

        Assert.Equal(2, created.Items.Count);
        Assert.Equal(1, created.Items[0].WidthStuds);
        Assert.Equal(999, created.Items[1].Copies);
    }

    [Fact]
    public async Task RejectsAnItemWhoseImageDoesNotExist()
    {
        var (service, _) = BuildProjectService();
        var missing = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = "Broken",
                Items = [new StickerItem { ImageId = missing, WidthStuds = 1, DepthStuds = 1 }],
            }));

        Assert.Contains(missing.ToString(), error.Message);
    }

    [Fact]
    public async Task RejectsASecondItemWithTheSameImageAndFootprint()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = "Twice",
                Items =
                [
                    new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 4 },
                    new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 4, Copies = 3 },
                ],
            }));

        Assert.Contains("already has an item", error.Message);
    }

    [Fact]
    public async Task AcceptsTheSameImageWithADifferentFootprint()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Two sizes",
            Items =
            [
                new StickerItem { ImageId = imageId, WidthStuds = 1, DepthStuds = 1 },
                new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 4 },
            ],
        });

        Assert.Equal(2, created.Items.Count);
    }

    [Fact]
    public async Task FitDefaultsToContain()
    {
        var (service, images) = BuildProjectService();

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Default fit",
            Items = [new StickerItem { ImageId = images.Add("Art"), WidthStuds = 1, DepthStuds = 1 }],
        });

        Assert.Equal(StickerFit.Contain, created.Items[0].Fit);
    }

    [Fact]
    public async Task KeepsSuppliedItemIdsAndGivesFreshOnesToNewItems()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");
        var supplied = Guid.NewGuid();

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Ids",
            Items =
            [
                new StickerItem { Id = supplied, ImageId = imageId, WidthStuds = 1, DepthStuds = 1 },
                new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 2 },
            ],
        });

        Assert.Equal(supplied, created.Items[0].Id);
        Assert.NotEqual(Guid.Empty, created.Items[1].Id);
    }

    [Fact]
    public async Task UpdatingAProjectReplacesTheItemListWholesale()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Before",
            Items = [new StickerItem { ImageId = imageId, WidthStuds = 1, DepthStuds = 1 }],
        });

        var updated = await service.UpdateAsync(created.Id, new StickerProject
        {
            Name = "After",
            Items = [new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 2, Copies = 4 }],
        });

        Assert.Equal("After", updated.Name);
        Assert.Single(updated.Items);
        Assert.Equal(2, updated.Items[0].WidthStuds);
        Assert.Equal(4, updated.Items[0].Copies);
    }

    [Fact]
    public async Task UpdatingAMissingProjectIsA404()
    {
        var (service, _) = BuildProjectService();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(Guid.NewGuid(), new StickerProject { Name = "X" }));
    }

    [Fact]
    public async Task TrimsTheItemNotesAndKeepsNullForBlank()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Notes",
            Items =
            [
                new StickerItem { ImageId = imageId, WidthStuds = 1, DepthStuds = 1, Notes = "  door  " },
                new StickerItem { ImageId = imageId, WidthStuds = 2, DepthStuds = 2, Notes = "   " },
            ],
        });

        Assert.Equal("door", created.Items[0].Notes);
        Assert.Null(created.Items[1].Notes);
    }

    // ---- artwork orientation ------------------------------------------------------------------

    [Theory]
    [InlineData("Auto", StickerOrientation.Auto)]
    [InlineData("Landscape", StickerOrientation.Landscape)]
    [InlineData(" Portrait ", StickerOrientation.Portrait)]
    [InlineData("portrait", StickerOrientation.Portrait)]
    [InlineData("", StickerOrientation.Auto)]
    [InlineData(null, StickerOrientation.Auto)]
    public void ParsesTheOrientationNameAndFallsBackToAuto(string? value, StickerOrientation expected)
    {
        Assert.Equal(expected, StickerRules.ParseOrientation(value));
    }

    [Fact]
    public void RefusesAnUnknownOrientationName()
    {
        var error = Assert.Throws<InvalidOperationException>(() => StickerRules.ParseOrientation("Sideways"));
        Assert.Contains("Sideways", error.Message);
        Assert.Contains("Auto", error.Message);
    }

    [Fact]
    public async Task OrientationDefaultsToAutoAndKeepsTheSuppliedValue()
    {
        var (service, images) = BuildProjectService();
        var imageId = images.Add("Art");

        var created = await service.CreateAsync(new StickerProject
        {
            Name = "Orientation",
            Items =
            [
                new StickerItem { ImageId = imageId, WidthStuds = 1, DepthStuds = 2 },
                new StickerItem
                {
                    ImageId = imageId,
                    WidthStuds = 2,
                    DepthStuds = 4,
                    Orientation = StickerOrientation.Portrait,
                },
            ],
        });

        Assert.Equal(StickerOrientation.Auto, created.Items[0].Orientation);
        Assert.Equal(StickerOrientation.Portrait, created.Items[1].Orientation);
    }

    [Fact]
    public async Task RefusesAnOrientationThatIsNotAStickerOrientation()
    {
        var (service, images) = BuildProjectService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new StickerProject
            {
                Name = "Garbage",
                Items =
                [
                    new StickerItem
                    {
                        ImageId = images.Add("Art"),
                        WidthStuds = 1,
                        DepthStuds = 1,
                        Orientation = (StickerOrientation)42,
                    },
                ],
            }));

        Assert.Contains("orientation", error.Message);
    }

    [Fact]
    public async Task AnUnknownOrientationInTheRequestIsABadRequest()
    {
        var (controller, images) = BuildProjectController();

        var result = await controller.Create(new CreateStickerProjectRequest("Sheet",
            [new StickerItemRequest(null, images.Add("Art"), 1, 2, 1, "Contain", "Sideways")]));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Sideways", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task OrientationTravelsFromTheRequestToTheResponseOnCreateAndUpdate()
    {
        var (controller, images) = BuildProjectController();
        var imageId = images.Add("Art");

        // Omitted orientation → Auto, i.e. the artwork is printed as uploaded.
        var createdAt = await controller.Create(new CreateStickerProjectRequest("Sheet",
            [new StickerItemRequest(null, imageId, 1, 2)]));
        var created = Assert.IsType<StickerProjectResponse>(
            Assert.IsType<ObjectResult>(createdAt).Value);
        var item = Assert.Single(created.Items);
        Assert.Equal("Auto", item.Orientation);

        // The orientation is metadata about the artwork, so the label keeps its millimetres.
        Assert.Equal(1 * StickerItem.StudMm, item.WidthMm);
        Assert.Equal(2 * StickerItem.StudMm, item.DepthMm);

        var updatedAt = await controller.Update(created.Id, new UpdateStickerProjectRequest("Sheet",
            [new StickerItemRequest(item.Id, imageId, 1, 2, 2, "Cover", "Portrait", "windscreen")],
            created.Version));
        var updated = Assert.IsType<StickerProjectResponse>(
            Assert.IsType<OkObjectResult>(updatedAt).Value);
        var updatedItem = Assert.Single(updated.Items);
        Assert.Equal("Portrait", updatedItem.Orientation);
        Assert.Equal("Cover", updatedItem.Fit);
        Assert.Equal(item.WidthMm, updatedItem.WidthMm);
        Assert.Equal(item.DepthMm, updatedItem.DepthMm);

        // …and it is read back from the same response mapping.
        var readAt = await controller.GetById(created.Id);
        var read = Assert.IsType<StickerProjectResponse>(
            Assert.IsType<OkObjectResult>(readAt).Value);
        Assert.Equal("Portrait", Assert.Single(read.Items).Orientation);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static StickerImageService BuildImageService(FakeStickerImageRepository? repo = null) =>
        new(repo ?? new FakeStickerImageRepository(),
            new FakeStickerProjectRepository(),
            new FakeImageRepository());

    private static (StickerProjectService Service, FakeStickerImageRepository Images) BuildProjectService()
    {
        var projects = new FakeStickerProjectRepository();
        var images = new FakeStickerImageRepository();
        return (new StickerProjectService(projects, images), images);
    }

    private static (StickerProjectsController Controller, FakeStickerImageRepository Images) BuildProjectController()
    {
        var (service, images) = BuildProjectService();
        return (new StickerProjectsController(service, images), images);
    }
}

internal sealed class FakeStickerImageRepository : IStickerImageRepository
{
    private readonly List<StickerImage> _rows = [];

    public IReadOnlyList<StickerImage> Rows => _rows;

    /// <summary>Registers an image and returns its id, for tests that only care about the reference.</summary>
    public Guid Add(string name)
    {
        var image = new StickerImage { Id = Guid.NewGuid(), Name = name };
        _rows.Add(image);
        return image.Id;
    }

    public Task<IEnumerable<StickerImage>> GetAllAsync() => Task.FromResult<IEnumerable<StickerImage>>(_rows);

    public Task<StickerImage?> GetByIdAsync(Guid id) =>
        Task.FromResult(_rows.FirstOrDefault(r => r.Id == id));

    public Task<StickerImage> CreateAsync(StickerImage image)
    {
        image.Id = image.Id == Guid.Empty ? Guid.NewGuid() : image.Id;
        image.Version = 0;
        _rows.Add(image);
        return Task.FromResult(image);
    }

    public Task<StickerImage?> UpdateAsync(StickerImage image)
    {
        var index = _rows.FindIndex(r => r.Id == image.Id);
        if (index < 0) return Task.FromResult<StickerImage?>(null);
        _rows[index] = image;
        return Task.FromResult<StickerImage?>(image);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var removed = _rows.RemoveAll(r => r.Id == id) > 0;
        return Task.FromResult(removed);
    }

    public Task<StickerImage?> UpdateImageCachedAsync(Guid id, bool cached)
    {
        var row = _rows.FirstOrDefault(r => r.Id == id);
        if (row is null) return Task.FromResult<StickerImage?>(null);
        row.ImageCached = cached;
        return Task.FromResult<StickerImage?>(row);
    }
}

internal sealed class FakeStickerProjectRepository : IStickerProjectRepository
{
    public List<StickerProject> Rows { get; } = [];

    public Task<IEnumerable<StickerProject>> GetAllAsync() => Task.FromResult<IEnumerable<StickerProject>>(Rows);

    public Task<StickerProject?> GetByIdAsync(Guid id) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

    public Task<StickerProject> CreateAsync(StickerProject project)
    {
        project.Id = project.Id == Guid.Empty ? Guid.NewGuid() : project.Id;
        project.Version = 0;
        Rows.Add(project);
        return Task.FromResult(project);
    }

    public Task<StickerProject?> UpdateAsync(StickerProject project)
    {
        var index = Rows.FindIndex(r => r.Id == project.Id);
        if (index < 0) return Task.FromResult<StickerProject?>(null);
        Rows[index] = project;
        return Task.FromResult<StickerProject?>(project);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var removed = Rows.RemoveAll(r => r.Id == id) > 0;
        return Task.FromResult(removed);
    }

    public Task<int> CountUsagesAsync(Guid imageId) =>
        Task.FromResult(Rows.Sum(p => p.Items.Count(i => i.ImageId == imageId)));
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
