using System.Globalization;
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Support;

internal static class Fixtures
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static CatalogVersion Version { get; } = new(
        "fixture",
        DateTimeOffset.Parse("2026-09-25T10:20:33Z", CultureInfo.InvariantCulture),
        DateTimeOffset.UnixEpoch);

    public static ConversionResult ConvertSample() =>
        CatalogConverter.Convert(Read("items.sample.json"), Read("sets.sample.json"), Version);
}
