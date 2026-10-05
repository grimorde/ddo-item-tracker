using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.State;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class CatalogUpdateCoordinatorTests
{
    private static readonly DateTimeOffset NewDate = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);

    /// <summary>The sample catalog a week later: Relic removed, a new trinket in a new pack added.</summary>
    private static ItemCatalog Newer()
    {
        var current = Create().Catalog;
        return current with
        {
            Version = new CatalogVersion("fedcba9876543210", NewDate, NewDate),
            Items = [.. current.Items.Where(i => i.Key != Keys.Relic), Item("Shiny Trinket", 32, "Trinket", pack: "New Pack")],
        };
    }

    private static CatalogCheckResult.Available Available(SessionFixture f, ItemCatalog candidate) =>
        new(candidate, CatalogDiff.Compute(f.Session.Catalog.Catalog, candidate, f.Session.Data.OwnedCopies.Select(c => c.ItemKey)), []);

    private static void AddSharedCopy(SessionFixture f, string key) =>
        f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = key, ItemName = "x", Server = "Cormyr", Storage = StorageType.SharedBank }));

    [Fact]
    public async Task Available_Confirmed_SavesAndSwapsInTheNewCatalog()
    {
        using var f = new SessionFixture();
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;
        f.CatalogChecker.Result = Available(f, Newer());
        f.Dialogs.ConfirmAnswers.Enqueue(true);

        await f.CatalogUpdates().CheckAsync(userInitiated: true);

        Assert.Equal("fedcba9876543210", f.Session.Catalog.Catalog.Version.UpstreamCommit);
        Assert.True(f.Session.CatalogIsDownloaded);
        Assert.Equal(1, raised);
        Assert.Equal("fedcba9876543210", f.CatalogStore.Load().Catalog!.Version.UpstreamCommit);
        Assert.Equal(SessionFixture.Now, f.Settings.LastCatalogCheckUtc);
    }

    [Fact]
    public async Task Available_Declined_ChangesNothing()
    {
        using var f = new SessionFixture();
        f.CatalogChecker.Result = Available(f, Newer());
        f.Dialogs.ConfirmAnswers.Enqueue(false);

        await f.CatalogUpdates().CheckAsync(userInitiated: true);

        Assert.Equal("83bc99b2a764cebe13e3c72c41f0580167575498", f.Session.Catalog.Catalog.Version.UpstreamCommit);
        Assert.False(f.Session.CatalogIsDownloaded);
        Assert.Null(f.CatalogStore.Load().Catalog);
    }

    [Fact]
    public async Task Summary_ShowsDatesChangesAndCopiesNoLongerListed()
    {
        using var f = new SessionFixture();
        AddSharedCopy(f, Keys.Relic);
        f.CatalogChecker.Result = Available(f, Newer());
        f.Dialogs.ConfirmAnswers.Enqueue(false);

        await f.CatalogUpdates().CheckAsync(userInitiated: false);

        var asked = Assert.Single(f.Dialogs.Asked);
        Assert.Contains("27 Sep 2026 → 4 Oct 2026", asked);
        Assert.Contains("1 item added, 1 removed.", asked);
        Assert.Contains("1 of your copies is not in the new catalog", asked);
        Assert.Equal([Keys.Relic], f.CatalogChecker.LastOwnedKeys);
    }

    [Fact]
    public async Task UpToDate_UserInitiated_SaysSo()
    {
        using var f = new SessionFixture();
        await f.CatalogUpdates().CheckAsync(userInitiated: true);
        Assert.Equal((CatalogUpdateCoordinator.Title, "The catalog is up to date."), Assert.Single(f.Dialogs.Alerts));
    }

    [Theory]
    [MemberData(nameof(QuietResults))]
    public async Task NothingToOffer_AtStartup_StaysQuiet(CatalogCheckResult result)
    {
        using var f = new SessionFixture();
        f.CatalogChecker.Result = result;

        await f.CatalogUpdates().CheckOnStartupAsync();

        Assert.Empty(f.Dialogs.Alerts);
        Assert.Empty(f.Dialogs.Asked);
    }

    public static TheoryData<CatalogCheckResult> QuietResults() =>
    [
        new CatalogCheckResult.UpToDate(),
        new CatalogCheckResult.Unsupported(),
        new CatalogCheckResult.Failed("offline"),
        new CatalogCheckResult.Refused(["bad"]),
    ];

    [Fact]
    public async Task Failed_UserInitiated_ShowsTheMessage_AndDoesNotCountAsAChecked()
    {
        using var f = new SessionFixture();
        f.CatalogChecker.Result = new CatalogCheckResult.Failed(CatalogUpdateChecker.UnreachableMessage);

        await f.CatalogUpdates().CheckAsync(userInitiated: true);

        Assert.Equal(CatalogUpdateChecker.UnreachableMessage, Assert.Single(f.Dialogs.Alerts).Message);
        Assert.Null(f.Settings.LastCatalogCheckUtc);
    }

    [Fact]
    public async Task Startup_CheckedWithinADay_DoesNotCheckAgain()
    {
        using var f = new SessionFixture();
        f.Settings.LastCatalogCheckUtc = SessionFixture.Now.AddHours(-23);

        await f.CatalogUpdates().CheckOnStartupAsync();

        Assert.Equal(0, f.CatalogChecker.Calls);
    }

    [Fact]
    public async Task Startup_CheckedOverADayAgo_ChecksOnce()
    {
        using var f = new SessionFixture();
        f.Settings.LastCatalogCheckUtc = SessionFixture.Now.AddHours(-25);
        var updates = f.CatalogUpdates();

        await updates.CheckOnStartupAsync();
        await updates.CheckOnStartupAsync();

        Assert.Equal(1, f.CatalogChecker.Calls);
        Assert.Equal(SessionFixture.Now, f.Settings.LastCatalogCheckUtc);
    }

    [Fact]
    public async Task Startup_UnreadableDownload_IsReportedOnce()
    {
        using var f = new SessionFixture();
        var updates = f.CatalogUpdates(storedWasUnreadable: true);

        await updates.CheckOnStartupAsync();
        await updates.CheckOnStartupAsync();

        Assert.Contains("couldn't be read", Assert.Single(f.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Reset_Confirmed_DeletesTheDownload_AndGoesBackToBuiltIn()
    {
        using var f = new SessionFixture();
        f.CatalogChecker.Result = Available(f, Newer());
        f.Dialogs.ConfirmAnswers.Enqueue(true);
        var updates = f.CatalogUpdates();
        await updates.CheckAsync(userInitiated: true);

        f.Dialogs.ConfirmAnswers.Enqueue(true);
        await updates.ResetToBuiltInAsync();

        Assert.False(f.Session.CatalogIsDownloaded);
        Assert.Equal("83bc99b2a764cebe13e3c72c41f0580167575498", f.Session.Catalog.Catalog.Version.UpstreamCommit);
        Assert.False(File.Exists(f.CatalogStore.FilePath));
    }

    [Fact]
    public async Task Reset_WhenAlreadyBuiltIn_JustSaysSo()
    {
        using var f = new SessionFixture();
        await f.CatalogUpdates().ResetToBuiltInAsync();
        Assert.Contains("already using", Assert.Single(f.Dialogs.Alerts).Message);
        Assert.Empty(f.Dialogs.Asked);
    }

    [Fact]
    public async Task CatalogScreen_AfterAnUpdate_ListsTheNewItems_AndFilterOptions()
    {
        using var f = new SessionFixture();
        var vm = new CatalogViewModel(f.Session, f.Navigator, f.Dialogs, f.CatalogUpdates());
        vm.Activate();
        vm.Filters.SelectedPack = "Vecna Unleashed";
        f.CatalogChecker.Result = Available(f, Newer());
        f.Dialogs.ConfirmAnswers.Enqueue(true);

        await f.CatalogUpdates().CheckAsync(userInitiated: true);

        Assert.Contains("New Pack", vm.Filters.Packs);
        Assert.Equal("Vecna Unleashed", vm.Filters.SelectedPack);
        vm.Filters.SelectedPack = ItemFilterPanel.Any;
        Assert.Contains(vm.Results, r => r.Name == "Shiny Trinket");
        Assert.DoesNotContain(vm.Results, r => r.Key == Keys.Relic);
    }

    [Fact]
    public async Task CatalogScreen_OnAppearing_StartsTheStartupCheck()
    {
        using var f = new SessionFixture();
        var vm = new CatalogViewModel(f.Session, f.Navigator, f.Dialogs, f.CatalogUpdates());

        await vm.OnAppearingAsync();
        await vm.StartupCatalogCheck;

        Assert.Equal(1, f.CatalogChecker.Calls);
    }
}
