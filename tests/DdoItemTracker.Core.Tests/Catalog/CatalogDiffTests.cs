using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogDiffTests
{
    [Fact]
    public void Compute_ReportsAddedRemovedAndOrphanedCopies()
    {
        var current = TestCatalogs.Catalog([TestCatalogs.Item("A"), TestCatalogs.Item("B")]);
        var candidate = TestCatalogs.Catalog([TestCatalogs.Item("B"), TestCatalogs.Item("C")]);
        string[] owned = ["A|1|Ring", "A|1|Ring", "B|1|Ring"];

        var diff = CatalogDiff.Compute(current, candidate, owned);

        Assert.Equal(["C|1|Ring"], diff.AddedKeys);
        Assert.Equal(["A|1|Ring"], diff.RemovedKeys);
        Assert.Equal(2, diff.OrphanedCopyCount);
    }

    [Fact]
    public void Compute_WithNoCurrentCatalog_TreatsEverythingAsAdded()
    {
        var diff = CatalogDiff.Compute(null, TestCatalogs.Catalog([TestCatalogs.Item("A")]), []);
        Assert.Equal(["A|1|Ring"], diff.AddedKeys);
        Assert.Empty(diff.RemovedKeys);
    }
}
