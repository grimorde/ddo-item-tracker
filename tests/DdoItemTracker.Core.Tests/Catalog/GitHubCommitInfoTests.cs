using System.Globalization;
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class GitHubCommitInfoTests
{
    private const string Commit = """
        {"sha":"1bc622480c85b5e2740b15562b9cbc5b70ce9e0c","commit":{"committer":{"date":"2026-09-25T10:20:33Z"}}}
        """;

    [Fact]
    public void Parse_SingleCommit()
    {
        var info = GitHubCommitInfo.Parse(Commit);
        Assert.Equal("1bc622480c85b5e2740b15562b9cbc5b70ce9e0c", info.Sha);
        Assert.Equal(DateTimeOffset.Parse("2026-09-25T10:20:33Z", CultureInfo.InvariantCulture), info.CommitDateUtc);
    }

    [Fact]
    public void Parse_ArrayTakesFirst()
    {
        Assert.Equal("1bc622480c85b5e2740b15562b9cbc5b70ce9e0c", GitHubCommitInfo.Parse($"[{Commit}]").Sha);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"message":"API rate limit exceeded"}""")]
    public void Parse_Unusable_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => GitHubCommitInfo.Parse(json));
    }
}
