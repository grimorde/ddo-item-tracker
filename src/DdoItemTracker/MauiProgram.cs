using CommunityToolkit.Maui;
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
        builder.Services.AddSingleton(sp =>
        {
            var session = new TrackerSession(sp.GetRequiredService<TrackerStore>(), BuiltInCatalog.Load());
            session.Load();
            return session;
        });

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
