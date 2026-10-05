using CommunityToolkit.Maui;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;
using DdoItemTracker.Presentation.ViewModels;
using DdoItemTracker.Services;
using DdoItemTracker.Views;
using Microsoft.Extensions.Logging;

namespace DdoItemTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Font-Awesome-6-Free-Solid-900.ttf", "IconSet");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_ => new TrackerStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton(_ => new CatalogStore(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton(sp => CatalogChoice.Choose(BuiltInCatalog.Read(), sp.GetRequiredService<CatalogStore>().Load()));
        builder.Services.AddSingleton(sp =>
        {
            var choice = sp.GetRequiredService<CatalogChoice>();
            var session = new TrackerSession(sp.GetRequiredService<TrackerStore>(), new CatalogIndex(choice.Catalog), choice.IsDownloaded);
            session.Load();
            return session;
        });
        builder.Services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.TryParseAdd($"DdoItemTracker/{AppInfo.Current.VersionString}");
            return http;
        });
        builder.Services.AddSingleton<ICatalogUpdateChecker>(sp =>
            new CatalogUpdateChecker(sp.GetRequiredService<HttpClient>(), PublishedCatalogSource.Default));
        builder.Services.AddSingleton(sp => new CatalogUpdateCoordinator(
            sp.GetRequiredService<TrackerSession>(),
            sp.GetRequiredService<CatalogStore>(),
            sp.GetRequiredService<ICatalogUpdateChecker>(),
            BuiltInCatalog.Read,
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<CatalogChoice>().StoredWasUnreadable));

        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<INavigator, ShellNavigator>();
        builder.Services.AddSingleton<IFileService, MauiFileService>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();

        builder.Services.AddTransient<CatalogViewModel>();
        builder.Services.AddTransient<ItemDetailViewModel>();
        builder.Services.AddTransient<CopyEditorViewModel>();

        builder.Services.AddTransient<CatalogPage>();
        builder.Services.AddTransient<ItemDetailPage>();
        builder.Services.AddTransient<CopyEditorPage>();

        builder.Services.AddTransient<MyItemsViewModel>();
        builder.Services.AddTransient<CharactersViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<MyItemsPage>();
        builder.Services.AddTransient<CharactersPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
