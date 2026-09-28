using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesContent()
    {
        var catalog = Fixtures.ConvertSample().Catalog;
        var json = CatalogSerializer.Serialize(catalog);
        var back = CatalogSerializer.Deserialize(json);

        Assert.Equal(json, CatalogSerializer.Serialize(back));
        Assert.Equal(catalog.Items.Count, back.Items.Count);
        Assert.Equal(catalog.Version, back.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public void DamagedFile_ThrowsInvalidDataException(string json)
    {
        var ex = Assert.Throws<InvalidDataException>(() => CatalogSerializer.Deserialize(json));
        Assert.Equal("The catalog file is damaged.", ex.Message);
    }
}
