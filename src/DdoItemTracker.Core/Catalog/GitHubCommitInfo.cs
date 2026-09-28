using System.Text.Json;

namespace DdoItemTracker.Core.Catalog;

public sealed record GitHubCommitInfo(string Sha, DateTimeOffset CommitDateUtc)
{
    /// <summary>Reads a GitHub commit object, or the first commit of a commit list.</summary>
    public static GitHubCommitInfo Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0) throw new InvalidDataException("GitHub returned no commits.");
                root = root[0];
            }
            var sha = root.GetProperty("sha").GetString();
            var date = root.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTimeOffset();
            if (string.IsNullOrWhiteSpace(sha)) throw new InvalidDataException("GitHub returned a commit with no SHA.");
            return new GitHubCommitInfo(sha, date.ToUniversalTime());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("GitHub returned an unexpected response.", ex);
        }
    }
}
