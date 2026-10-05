using System.Net;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogUpdateCheckerTests
{
    private static readonly DateTimeOffset Old = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset New = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly PublishedCatalogSource Source = PublishedCatalogSource.Default;

    /// <summary>Answers each URL with a fixed response, and records which URLs were asked for.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Dictionary<string, Func<HttpResponseMessage>> Responses { get; } = [];
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            return Task.FromResult(Responses.TryGetValue(url, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public void Serve(string url, string body) =>
            Responses[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private static ItemCatalog Catalog(string commit, DateTimeOffset date, params string[] names) =>
        TestCatalogs.Catalog(names.Select(n => TestCatalogs.Item(n))) with { Version = new CatalogVersion(commit, date, date) };

    private static ItemCatalog Current() => Catalog("old", Old, "Ring A", "Ring B");

    private static (CatalogUpdateChecker Checker, FakeHandler Handler) Publishing(ItemCatalog? published, int formatVersion = 1)
    {
        var handler = new FakeHandler();
        if (published is not null)
        {
            handler.Serve(Source.VersionUrl, (PublishedCatalogVersion.For(published) with { FormatVersion = formatVersion }).ToJson());
            handler.Serve(Source.CatalogUrl, CatalogSerializer.Serialize(published));
        }
        return (new CatalogUpdateChecker(new HttpClient(handler), Source), handler);
    }

    [Fact]
    public async Task SameCommit_IsUpToDate_WithoutDownloadingTheCatalog()
    {
        var (checker, handler) = Publishing(Current());

        var result = await checker.CheckAsync(Current(), []);

        Assert.IsType<CatalogCheckResult.UpToDate>(result);
        Assert.Equal([Source.VersionUrl], handler.Requested);
    }

    [Fact]
    public async Task OlderPublishedCatalog_IsUpToDate()
    {
        var (checker, _) = Publishing(Catalog("older", Old.AddDays(-1), "Ring A", "Ring B"));
        Assert.IsType<CatalogCheckResult.UpToDate>(await checker.CheckAsync(Current(), []));
    }

    [Fact]
    public async Task NewerCatalog_IsAvailable_WithTheDifferences()
    {
        var (checker, handler) = Publishing(Catalog("new", New, "Ring A", "Ring B", "Ring C"));

        var result = await checker.CheckAsync(Current(), ["Ring A|1|Ring"]);

        var available = Assert.IsType<CatalogCheckResult.Available>(result);
        Assert.Equal("new", available.Catalog.Version.UpstreamCommit);
        Assert.Equal(["Ring C|1|Ring"], available.Diff.AddedKeys);
        Assert.Empty(available.Diff.RemovedKeys);
        Assert.Equal(0, available.Diff.OrphanedCopyCount);
        Assert.Equal([Source.VersionUrl, Source.CatalogUrl], handler.Requested);
    }

    [Fact]
    public async Task NewerCatalog_CountsCopiesItNoLongerHas()
    {
        var (checker, _) = Publishing(Catalog("new", New, "Ring A", "Ring B", "Ring C"));
        var current = Catalog("old", Old, "Ring A", "Ring B", "Ring Z");

        var result = await checker.CheckAsync(current, ["Ring Z|1|Ring", "Ring Z|1|Ring", "Ring A|1|Ring"]);

        var available = Assert.IsType<CatalogCheckResult.Available>(result);
        Assert.Equal(["Ring Z|1|Ring"], available.Diff.RemovedKeys);
        Assert.Equal(2, available.Diff.OrphanedCopyCount);
    }

    [Fact]
    public async Task NewerFormat_IsUnsupported_WithoutDownloadingTheCatalog()
    {
        var (checker, handler) = Publishing(Catalog("new", New, "Ring A"), formatVersion: PublishedCatalogVersion.CurrentFormatVersion + 1);

        Assert.IsType<CatalogCheckResult.Unsupported>(await checker.CheckAsync(Current(), []));
        Assert.Equal([Source.VersionUrl], handler.Requested);
    }

    [Fact]
    public async Task CatalogThatLostMostItems_IsRefused()
    {
        var current = TestCatalogs.WithItemCount(100) with { Version = new CatalogVersion("old", Old, Old) };
        var (checker, _) = Publishing(TestCatalogs.WithItemCount(10) with { Version = new CatalogVersion("new", New, New) });

        var refused = Assert.IsType<CatalogCheckResult.Refused>(await checker.CheckAsync(current, []));
        Assert.Contains(refused.Errors, e => e.Contains("dropped from 100 to 10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CatalogStillAtTheOldCommit_WhileTheVersionFileIsAhead_IsUpToDate()
    {
        var (checker, handler) = Publishing(Catalog("new", New, "Ring A"));
        handler.Serve(Source.CatalogUrl, CatalogSerializer.Serialize(Current()));

        Assert.IsType<CatalogCheckResult.UpToDate>(await checker.CheckAsync(Current(), []));
    }

    [Fact]
    public async Task NothingPublished_Fails()
    {
        var (checker, _) = Publishing(null);

        var failed = Assert.IsType<CatalogCheckResult.Failed>(await checker.CheckAsync(Current(), []));
        Assert.Equal(CatalogUpdateChecker.UnreachableMessage, failed.Message);
    }

    [Fact]
    public async Task NetworkError_Fails()
    {
        var (checker, handler) = Publishing(null);
        handler.Responses[Source.VersionUrl] = () => throw new HttpRequestException("offline");

        Assert.IsType<CatalogCheckResult.Failed>(await checker.CheckAsync(Current(), []));
    }

    [Fact]
    public async Task DamagedCatalog_Fails()
    {
        var (checker, handler) = Publishing(Catalog("new", New, "Ring A"));
        handler.Serve(Source.CatalogUrl, "{ not a catalog");

        var failed = Assert.IsType<CatalogCheckResult.Failed>(await checker.CheckAsync(Current(), []));
        Assert.Equal(CatalogUpdateChecker.UnreadableMessage, failed.Message);
    }

    [Fact]
    public async Task Cancelled_Throws()
    {
        var (checker, _) = Publishing(Current());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(Current(), [], cts.Token));
    }
}
