using System.Globalization;
using System.Net.Http.Headers;
using DdoItemTracker.Core.Catalog;

const string Usage = """
    Usage:
      CatalogBuilder --out <catalog.json> [--commit <sha>] [--previous <catalog.json>]
      CatalogBuilder --out <catalog.json> --items <items.json> --sets <sets.json> --commit <sha> --date <iso-date> [--previous <catalog.json>]

    Without --items/--sets the upstream files are downloaded pinned to --commit, or to the latest commit that changed them.
    --previous defaults to the existing --out file, so the item-count rule compares against the catalog being replaced.
    """;

var opts = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 0; i + 1 < args.Length; i += 2) opts[args[i]] = args[i + 1];
if (!opts.TryGetValue("--out", out var outPath))
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var source = UpstreamSource.Default;
var built = DateTimeOffset.UtcNow;
string itemsJson, setsJson;
CatalogVersion version;

if (opts.TryGetValue("--items", out var itemsPath) | opts.TryGetValue("--sets", out var setsPath))
{
    if (itemsPath is null || setsPath is null || !opts.TryGetValue("--commit", out var sha) || !opts.TryGetValue("--date", out var date))
    {
        Console.Error.WriteLine("--items, --sets, --commit and --date must be given together.");
        return 2;
    }
    itemsJson = await File.ReadAllTextAsync(itemsPath);
    setsJson = await File.ReadAllTextAsync(setsPath);
    version = new CatalogVersion(sha, DateTimeOffset.Parse(date, CultureInfo.InvariantCulture).ToUniversalTime(), built);
}
else
{
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DdoItemTracker-CatalogBuilder", "1.0"));

    GitHubCommitInfo commit;
    if (opts.TryGetValue("--commit", out var sha))
    {
        commit = GitHubCommitInfo.Parse(await http.GetStringAsync(source.CommitApiUrl(sha)));
    }
    else
    {
        var items = GitHubCommitInfo.Parse(await http.GetStringAsync(source.LatestCommitApiUrl(source.ItemsPath)));
        var sets = GitHubCommitInfo.Parse(await http.GetStringAsync(source.LatestCommitApiUrl(source.SetsPath)));
        commit = items.CommitDateUtc >= sets.CommitDateUtc ? items : sets;
    }
    Console.WriteLine($"Upstream commit {commit.Sha} ({commit.CommitDateUtc:yyyy-MM-dd})");
    itemsJson = await http.GetStringAsync(source.RawUrl(commit.Sha, source.ItemsPath));
    setsJson = await http.GetStringAsync(source.RawUrl(commit.Sha, source.SetsPath));
    version = new CatalogVersion(commit.Sha, commit.CommitDateUtc, built);
}

var result = CatalogConverter.Convert(itemsJson, setsJson, version);
var previousPath = opts.GetValueOrDefault("--previous") ?? outPath;
ItemCatalog? previous = File.Exists(previousPath)
    ? CatalogSerializer.Deserialize(await File.ReadAllTextAsync(previousPath))
    : null;
var validation = CatalogValidator.Validate(result, previous);

var r = result.Report;
Console.WriteLine($"Input records: {r.InputItemCount}, dropped invalid: {r.DroppedInvalid}, dropped duplicate: {r.DroppedDuplicate}, conflicting keys: {r.DuplicateKeys.Count}");
Console.WriteLine($"Items: {result.Catalog.Items.Count}, sets: {result.Catalog.Sets.Count}, unresolved set names: {r.UnresolvedSetNames.Count}");
foreach (var w in validation.Warnings) Console.WriteLine($"Warning: {w}");
foreach (var e in validation.Errors) Console.Error.WriteLine($"Error: {e}");
if (!validation.IsValid)
{
    Console.Error.WriteLine("Catalog NOT written.");
    return 1;
}

var fullOut = Path.GetFullPath(outPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
var temp = fullOut + ".tmp";
await File.WriteAllTextAsync(temp, CatalogSerializer.Serialize(result.Catalog));
File.Move(temp, fullOut, overwrite: true);
Console.WriteLine($"Wrote {fullOut}");
return 0;
