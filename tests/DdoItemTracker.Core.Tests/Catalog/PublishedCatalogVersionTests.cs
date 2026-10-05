using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class PublishedCatalogVersionTests
{
    [Fact]
    public void For_DescribesTheCatalog()
    {
        var catalog = TestCatalogs.WithItemCount(3) with
        {
            Version = new CatalogVersion("abc123", DateTimeOffset.UnixEpoch.AddDays(1), DateTimeOffset.UnixEpoch.AddDays(2)),
        };

        var version = PublishedCatalogVersion.For(catalog);

        Assert.Equal(new PublishedCatalogVersion(1, "abc123", DateTimeOffset.UnixEpoch.AddDays(1), DateTimeOffset.UnixEpoch.AddDays(2), 3, 1), version);
    }

    [Fact]
    public void ToJson_ThenParse_RoundTrips()
    {
        var version = new PublishedCatalogVersion(1, "abc123", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 10, 2);
        Assert.Equal(version, PublishedCatalogVersion.Parse(version.ToJson()));
    }

    [Fact]
    public void Parse_KeepsANewerFormatVersion()
    {
        var json = """{"FormatVersion":7,"UpstreamCommit":"abc","UpstreamCommitDateUtc":"2026-10-01T00:00:00Z","BuiltUtc":"2026-10-01T00:00:00Z","ItemCount":1,"SetCount":1}""";
        Assert.Equal(7, PublishedCatalogVersion.Parse(json).FormatVersion);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"FormatVersion":1,"UpstreamCommit":""}""")]
    public void Parse_Unusable_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => PublishedCatalogVersion.Parse(json));
    }

    [Fact]
    public void Source_PointsAtTheRollingRelease()
    {
        var source = PublishedCatalogSource.Default;
        Assert.Equal("https://github.com/grimorde/ddo-item-tracker/releases/download/catalog-latest/catalog.json", source.CatalogUrl);
        Assert.Equal("https://github.com/grimorde/ddo-item-tracker/releases/download/catalog-latest/catalog-version.json", source.VersionUrl);
    }
}
