using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Services;

public static class BuiltInCatalog
{
    /// <summary>Reads the catalog shipped in Resources/Raw: at startup, and again if the player resets to it.</summary>
    public static ItemCatalog Read()
    {
        using var stream = FileSystem.OpenAppPackageFileAsync("catalog.json").GetAwaiter().GetResult();
        using var reader = new StreamReader(stream);
        return CatalogSerializer.Deserialize(reader.ReadToEnd());
    }
}
