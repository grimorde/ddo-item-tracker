namespace DdoItemTracker.Core.Catalog;

public abstract record CatalogCheckResult
{
    public sealed record UpToDate : CatalogCheckResult;

    /// <summary>The published catalog is in a newer format than this version of the app can read.</summary>
    public sealed record Unsupported : CatalogCheckResult;

    /// <summary>GitHub couldn't be reached, or sent something unusable.</summary>
    public sealed record Failed(string Message) : CatalogCheckResult;

    /// <summary>A newer catalog exists but breaks a validation rule (spec 6.3).</summary>
    public sealed record Refused(IReadOnlyList<string> Errors) : CatalogCheckResult;

    public sealed record Available(ItemCatalog Catalog, CatalogDiff Diff, IReadOnlyList<string> Warnings) : CatalogCheckResult;
}

public interface ICatalogUpdateChecker
{
    /// <param name="ownedItemKeys">One entry per owned copy, for counting copies the new catalog no longer has.</param>
    Task<CatalogCheckResult> CheckAsync(ItemCatalog current, IEnumerable<string> ownedItemKeys, CancellationToken cancellationToken = default);
}

/// <summary>Looks for a newer published catalog, downloading the full file only when there is one.</summary>
public sealed class CatalogUpdateChecker(HttpClient http, PublishedCatalogSource source) : ICatalogUpdateChecker
{
    public const string UnreachableMessage = "Couldn't reach GitHub, try again later.";
    public const string UnreadableMessage = "The published catalog couldn't be read, try again later.";

    public async Task<CatalogCheckResult> CheckAsync(ItemCatalog current, IEnumerable<string> ownedItemKeys, CancellationToken cancellationToken = default)
    {
        try
        {
            var published = PublishedCatalogVersion.Parse(await http.GetStringAsync(source.VersionUrl, cancellationToken).ConfigureAwait(false));
            if (published.FormatVersion > PublishedCatalogVersion.CurrentFormatVersion) return new CatalogCheckResult.Unsupported();
            if (!IsNewer(published.UpstreamCommit, published.UpstreamCommitDateUtc, current)) return new CatalogCheckResult.UpToDate();

            var candidate = CatalogSerializer.Deserialize(await http.GetStringAsync(source.CatalogUrl, cancellationToken).ConfigureAwait(false));
            // The version file can briefly run ahead of catalog.json while a new release is uploading.
            if (!IsNewer(candidate.Version.UpstreamCommit, candidate.Version.UpstreamCommitDateUtc, current)) return new CatalogCheckResult.UpToDate();

            var validation = CatalogValidator.Validate(candidate, current);
            if (!validation.IsValid) return new CatalogCheckResult.Refused(validation.Errors);
            return new CatalogCheckResult.Available(candidate, CatalogDiff.Compute(current, candidate, ownedItemKeys), validation.Warnings);
        }
        catch (HttpRequestException)
        {
            return new CatalogCheckResult.Failed(UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CatalogCheckResult.Failed(UnreachableMessage);
        }
        catch (InvalidDataException)
        {
            return new CatalogCheckResult.Failed(UnreadableMessage);
        }
    }

    private static bool IsNewer(string commit, DateTimeOffset commitDateUtc, ItemCatalog current) =>
        !string.Equals(commit, current.Version.UpstreamCommit, StringComparison.Ordinal)
        && commitDateUtc > current.Version.UpstreamCommitDateUtc;
}
