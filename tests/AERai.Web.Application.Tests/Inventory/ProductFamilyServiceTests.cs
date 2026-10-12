using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class ProductFamilyServiceTests
{
    private readonly FakeProductFamilyRepository _repository = new();

    public ProductFamilyServiceTests()
    {
        _repository.Skus["MAT-BLK"] = null;
        _repository.Skus["MAT-BLU"] = null;
        _repository.Skus["STRAP"] = null;
    }

    private ProductFamilyService CreateService() => new(_repository, NullLogger<ProductFamilyService>.Instance);

    [Fact]
    public async Task CreateAsync_TidiesTheNameAndRejectsDuplicatesIgnoringCase()
    {
        var service = CreateService();

        var created = await service.CreateAsync("  Yoga   mats ", CancellationToken.None);
        var duplicate = await service.CreateAsync("yoga MATS", CancellationToken.None);

        Assert.Equal("Yoga mats", created.Value);
        Assert.Equal("A family called \"yoga MATS\" already exists.", duplicate.Error);
        Assert.Single(_repository.Families);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateAsync_Blank_Fails(string? name)
    {
        Assert.True((await CreateService().CreateAsync(name, CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task CreateAsync_TooLong_Fails()
    {
        var result = await CreateService().CreateAsync(new string('x', FamilyNames.MaxLength + 1), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RenameAsync_ToAnotherFamilysName_Fails_ButChangingCaseOfItsOwnWorks()
    {
        var service = CreateService();
        await service.CreateAsync("Mats", CancellationToken.None);
        await service.CreateAsync("Straps", CancellationToken.None);
        var mats = _repository.Families.Single(f => f.Value == "Mats").Key;

        Assert.True((await service.RenameAsync(mats, "straps", CancellationToken.None)).IsFailure);
        Assert.Equal("MATS", (await service.RenameAsync(mats, "MATS", CancellationToken.None)).Value);
        Assert.Equal("That family no longer exists.", (await service.RenameAsync(999, "Other", CancellationToken.None)).Error);
    }

    [Fact]
    public async Task DeleteAsync_UnassignsItsSkus()
    {
        var service = CreateService();
        await service.AssignAsync(["MAT-BLK", "MAT-BLU"], "Mats", CancellationToken.None);
        var id = _repository.Families.Single().Key;

        var result = await service.DeleteAsync(id, CancellationToken.None);

        Assert.Equal("Mats", result.Value);
        Assert.All(_repository.Skus.Values, v => Assert.Null(v));
    }

    [Fact]
    public async Task AssignAsync_NewNameCreatesTheFamily_ExistingNameIsReused()
    {
        var service = CreateService();

        var first = await service.AssignAsync(["MAT-BLK", "MAT-BLK", "GHOST"], " Yoga mats ", CancellationToken.None);
        var second = await service.AssignAsync(["MAT-BLU"], "yoga mats", CancellationToken.None);

        Assert.Equal((1, 1), (first.Value, second.Value)); // duplicates and unknown SKUs don't count
        var family = Assert.Single(_repository.Families);
        Assert.Equal("Yoga mats", family.Value);
        Assert.Equal(family.Key, _repository.Skus["MAT-BLU"]);
    }

    [Fact]
    public async Task AssignAsync_BlankName_ClearsTheFamily()
    {
        var service = CreateService();
        await service.AssignAsync(["STRAP"], "Straps", CancellationToken.None);

        await service.AssignAsync(["STRAP"], null, CancellationToken.None);

        Assert.Null(_repository.Skus["STRAP"]);
        Assert.Single(_repository.Families);
    }

    [Fact]
    public async Task AssignAsync_NothingSelected_Fails()
    {
        Assert.Equal("Select at least one SKU.", (await CreateService().AssignAsync([], "Mats", CancellationToken.None)).Error);
    }
}
