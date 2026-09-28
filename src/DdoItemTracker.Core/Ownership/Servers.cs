namespace DdoItemTracker.Core.Ownership;

public static class Servers
{
    public static IReadOnlyList<string> All { get; } = ["Cormyr", "Moonsea", "Shadowdale", "Thrane"];

    /// <summary>The canonical server name for user or imported text, or null if it isn't a supported server.</summary>
    public static string? Canonical(string? server)
    {
        if (string.IsNullOrWhiteSpace(server)) return null;
        var trimmed = server.Trim();
        return All.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
