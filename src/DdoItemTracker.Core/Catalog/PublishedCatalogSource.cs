namespace DdoItemTracker.Core.Catalog;

/// <summary>Where the ready-made catalog is published: a rolling GitHub Release in the app's own repo.</summary>
public sealed record PublishedCatalogSource(string Repo, string Tag)
{
    public const string CatalogFileName = "catalog.json";
    public const string VersionFileName = "catalog-version.json";

    public static PublishedCatalogSource Default { get; } = new("grimorde/ddo-item-tracker", "catalog-latest");

    public string CatalogUrl => AssetUrl(CatalogFileName);
    public string VersionUrl => AssetUrl(VersionFileName);

    private string AssetUrl(string fileName) => $"https://github.com/{Repo}/releases/download/{Tag}/{fileName}";
}
