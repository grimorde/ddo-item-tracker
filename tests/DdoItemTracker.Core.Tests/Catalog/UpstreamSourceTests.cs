using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class UpstreamSourceTests
{
    [Fact]
    public void Default_PointsAtGearPlannerDataFolder()
    {
        var s = UpstreamSource.Default;
        Assert.Equal("illusionistpm/ddo-gear-planner", s.Repo);
        Assert.Equal("master", s.Branch);
        Assert.Equal("data/items.json", s.ItemsPath);
        Assert.Equal("data/sets.json", s.SetsPath);
    }

    [Fact]
    public void RawUrl_IsPinnedToCommit()
    {
        Assert.Equal(
            "https://raw.githubusercontent.com/illusionistpm/ddo-gear-planner/abc123/data/items.json",
            UpstreamSource.Default.RawUrl("abc123", "data/items.json"));
    }

    [Fact]
    public void LatestCommitApiUrl_FiltersByBranchAndPath()
    {
        Assert.Equal(
            "https://api.github.com/repos/illusionistpm/ddo-gear-planner/commits?sha=master&path=data%2Fitems.json&per_page=1",
            UpstreamSource.Default.LatestCommitApiUrl("data/items.json"));
    }

    [Fact]
    public void CommitApiUrl_AddressesOneCommit()
    {
        Assert.Equal(
            "https://api.github.com/repos/illusionistpm/ddo-gear-planner/commits/abc123",
            UpstreamSource.Default.CommitApiUrl("abc123"));
    }
}
