namespace DdoItemTracker.Core.Catalog;

/// <summary>Where the gear-planner data lives on GitHub.</summary>
public sealed record UpstreamSource(string Repo, string Branch, string ItemsPath, string SetsPath)
{
    public static UpstreamSource Default { get; } =
        new("illusionistpm/ddo-gear-planner", "master", "data/items.json", "data/sets.json");

    public string RawUrl(string commit, string path) =>
        $"https://raw.githubusercontent.com/{Repo}/{commit}/{path}";

    public string LatestCommitApiUrl(string path) =>
        $"https://api.github.com/repos/{Repo}/commits?sha={Uri.EscapeDataString(Branch)}&path={Uri.EscapeDataString(path)}&per_page=1";

    public string CommitApiUrl(string commit) =>
        $"https://api.github.com/repos/{Repo}/commits/{commit}";
}
