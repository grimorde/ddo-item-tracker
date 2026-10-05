using System.Text.Json;

namespace DdoItemTracker.Core.Catalog;

/// <summary>
/// The small file published next to catalog.json, so the app can tell whether there is anything new
/// before downloading the full catalog. <see cref="FormatVersion"/> changes only when catalog.json changes shape.
/// </summary>
public sealed record PublishedCatalogVersion(
    int FormatVersion,
    string UpstreamCommit,
    DateTimeOffset UpstreamCommitDateUtc,
    DateTimeOffset BuiltUtc,
    int ItemCount,
    int SetCount)
{
    public const int CurrentFormatVersion = 1;

    public static PublishedCatalogVersion For(ItemCatalog catalog) => new(
        CurrentFormatVersion,
        catalog.Version.UpstreamCommit,
        catalog.Version.UpstreamCommitDateUtc,
        catalog.Version.BuiltUtc,
        catalog.Items.Count,
        catalog.Sets.Count);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static PublishedCatalogVersion Parse(string json)
    {
        PublishedCatalogVersion? version;
        try
        {
            version = JsonSerializer.Deserialize<PublishedCatalogVersion>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The published catalog version couldn't be read.", ex);
        }
        if (version is null || version.FormatVersion < 1 || string.IsNullOrWhiteSpace(version.UpstreamCommit))
            throw new InvalidDataException("The published catalog version couldn't be read.");
        return version;
    }
}
