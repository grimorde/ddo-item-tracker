using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    [Fact]
    public void ValidCatalog_Passes()
    {
        var result = CatalogValidator.Validate(TestCatalogs.WithItemCount(100), TestCatalogs.WithItemCount(100));
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void NoItems_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([]), null);
        Assert.Contains("The catalog has no items.", result.Errors);
    }

    [Fact]
    public void NoSets_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([TestCatalogs.Item("A")], []), null);
        Assert.Contains("The catalog has no sets.", result.Errors);
    }

    [Fact]
    public void DuplicateKeyInCatalog_Fails()
    {
        var a = TestCatalogs.Item("A");
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([a, a]), null);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("A|1|Ring"));
    }

    [Fact]
    public void ItemCountBelow95Percent_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.WithItemCount(94), TestCatalogs.WithItemCount(100));
        Assert.Contains("Item count dropped from 100 to 94.", result.Errors);
    }

    [Fact]
    public void ItemCountAtExactly95Percent_Passes()
    {
        Assert.True(CatalogValidator.Validate(TestCatalogs.WithItemCount(95), TestCatalogs.WithItemCount(100)).IsValid);
    }

    [Fact]
    public void NoCurrentCatalog_SkipsCountRule()
    {
        Assert.True(CatalogValidator.Validate(TestCatalogs.WithItemCount(1), null).IsValid);
    }

    [Fact]
    public void MoreThanOnePercentInvalid_Fails()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(98), new ConversionReport(100, 2, 0, [], []));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.Contains("2 of 100 item records are missing a name, level or slot.", result.Errors);
    }

    [Fact]
    public void ExactlyOnePercentInvalid_Passes()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(99), new ConversionReport(100, 1, 0, [], []));
        Assert.True(CatalogValidator.Validate(conversion, null).IsValid);
    }

    [Fact]
    public void ConflictingKeysInReport_Fails()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(10), new ConversionReport(11, 0, 0, ["X|1|Ring"], []));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.Contains(result.Errors, e => e.Contains("X|1|Ring"));
    }

    [Fact]
    public void UnresolvedSetNames_WarnButPass()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(10), new ConversionReport(10, 0, 0, [], ["Oasis of Morality"]));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Oasis of Morality"));
    }

    [Fact]
    public void RealFixture_FailsInvalidRecordRule()
    {
        // The fixture deliberately contains Broken Record: 1 of 9 records invalid is over the 1% limit.
        Assert.False(CatalogValidator.Validate(Fixtures.ConvertSample(), null).IsValid);
    }
}
