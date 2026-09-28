using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Services;

public static class BuiltInCatalog
{
    /// <summary>Reads the catalog shipped in Resources/Raw. Runs once, when the session is first created.</summary>
    public static CatalogIndex Load()
    {
        using var stream = FileSystem.OpenAppPackageFileAsync("catalog.json").GetAwaiter().GetResult();
        using var reader = new StreamReader(stream);
        return new CatalogIndex(CatalogSerializer.Deserialize(reader.ReadToEnd()));
    }
}
