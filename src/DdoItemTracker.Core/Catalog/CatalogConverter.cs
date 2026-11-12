using System.Text.Json;

namespace DdoItemTracker.Core.Catalog;

public sealed record ConversionReport(
    int InputItemCount,
    int DroppedInvalid,
    int DroppedDuplicate,
    IReadOnlyList<string> DuplicateKeys,
    IReadOnlyList<string> UnresolvedSetNames);

public sealed record ConversionResult(ItemCatalog Catalog, ConversionReport Report);

/// <summary>Turns gear-planner's items.json, sets.json and quests.json into an <see cref="ItemCatalog"/>.</summary>
public static class CatalogConverter
{
    private const string WikiBase = "https://ddowiki.com";

    /// <summary>
    /// Upstream maps these quest chains to packs of their own, but they are story arcs inside a pack.
    /// Remove an entry once gear-planner's quests.json maps the chain to its parent pack.
    /// </summary>
    private static readonly Dictionary<string, string> PackCorrections = new(StringComparer.Ordinal)
    {
        ["Shadow Over Wheloon"] = "Shadowfell Conspiracy",
        ["The Secret of the Storm Horns"] = "Shadowfell Conspiracy",
    };

    /// <param name="questsJson">
    /// quests.json, whose "packs" map gives each source its pack. Items stopped carrying "pack" in November 2026;
    /// an item that still has one keeps it.
    /// </param>
    public static ConversionResult Convert(string itemsJson, string setsJson, string questsJson, CatalogVersion version)
    {
        using var itemsDoc = ParseDocument(itemsJson, "items");
        using var setsDoc = ParseDocument(setsJson, "sets");
        using var questsDoc = ParseDocument(questsJson, "quests");
        if (itemsDoc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The items file is not a JSON array.");
        if (setsDoc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The sets file is not a JSON object.");
        if (questsDoc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The quests file is not a JSON object.");
        var sourcePacks = GetSourcePacks(questsDoc.RootElement);

        var byKey = new Dictionary<string, CatalogItem>(StringComparer.Ordinal);
        var duplicateKeys = new SortedSet<string>(StringComparer.Ordinal);
        int input = 0, invalid = 0, duplicate = 0;

        foreach (var element in itemsDoc.RootElement.EnumerateArray())
        {
            input++;
            var item = MapItem(element, sourcePacks);
            if (item is null)
            {
                invalid++;
                continue;
            }
            if (byKey.TryGetValue(item.Key, out var existing))
            {
                if (SameContent(existing, item)) duplicate++;
                else duplicateKeys.Add(item.Key);
                continue;
            }
            byKey.Add(item.Key, item);
        }

        var sets = setsDoc.RootElement.EnumerateObject()
            .Select(p => MapSet(p.Name, p.Value))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
        var setNames = sets.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        var items = byKey.Values
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.MinLevel)
            .ThenBy(i => i.Slot, StringComparer.Ordinal)
            .ToList();

        var unresolved = items
            .SelectMany(i => i.SetNames)
            .Where(n => !setNames.Contains(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var report = new ConversionReport(input, invalid, duplicate, duplicateKeys.ToList(), unresolved);
        return new ConversionResult(new ItemCatalog(version, items, sets), report);
    }

    private static JsonDocument ParseDocument(string json, string label)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The {label} file is not valid JSON.", ex);
        }
    }

    private static Dictionary<string, string> GetSourcePacks(JsonElement quests)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!quests.TryGetProperty("packs", out var packs) || packs.ValueKind != JsonValueKind.Object) return map;
        foreach (var p in packs.EnumerateObject())
        {
            if (NullIfBlank(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null) is { } pack)
                map[p.Name.Trim()] = pack;
        }
        return map;
    }

    /// <summary>The item's own "pack" if it has one, otherwise the pack of its first source that has one.</summary>
    private static string? ResolvePack(JsonElement e, IReadOnlyList<string> quests, Dictionary<string, string> sourcePacks)
    {
        var pack = NullIfBlank(GetString(e, "pack"))
            ?? quests.Select(q => sourcePacks.GetValueOrDefault(q)).FirstOrDefault(p => p is not null);
        return pack is not null && PackCorrections.TryGetValue(pack, out var corrected) ? corrected : pack;
    }

    private static CatalogItem? MapItem(JsonElement e, Dictionary<string, string> sourcePacks)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var name = GetString(e, "name")?.Trim();
        var slot = GetString(e, "slot")?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(slot)) return null;
        if (!e.TryGetProperty("ml", out var ml) || ml.ValueKind != JsonValueKind.Number || !ml.TryGetInt32(out var minLevel))
            return null;

        var quests = GetStrings(e, "quests");
        return new CatalogItem
        {
            Key = ItemKey.For(name, minLevel, slot),
            Name = name,
            MinLevel = minLevel,
            Slot = slot,
            Type = NullIfBlank(GetString(e, "type")),
            Pack = ResolvePack(e, quests, sourcePacks),
            Quests = quests,
            Effects = GetEffects(e),
            CraftingSlots = GetStrings(e, "crafting"),
            SetNames = GetStrings(e, "sets"),
            IsRare = GetBool(e, "rare"),
            IsArtifact = GetBool(e, "artifact"),
            WikiUrl = ToWikiUrl(GetString(e, "url")),
        };
    }

    private static CatalogSet MapSet(string name, JsonElement value)
    {
        var tiers = new List<SetTier>();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in value.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.Object) continue;
                if (!t.TryGetProperty("threshold", out var th) || th.ValueKind != JsonValueKind.Number || !th.TryGetInt32(out var pieces))
                    continue;
                tiers.Add(new SetTier(pieces, GetEffects(t)));
            }
        }
        return new CatalogSet(name, tiers.OrderBy(t => t.PiecesRequired).ToList());
    }

    private static IReadOnlyList<Effect> GetEffects(JsonElement owner)
    {
        if (!owner.TryGetProperty("affixes", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        var list = new List<Effect>();
        foreach (var a in arr.EnumerateArray())
        {
            if (a.ValueKind != JsonValueKind.Object) continue;
            var name = GetString(a, "name")?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var type = NullIfBlank(GetString(a, "type"));
            list.Add(type == "Bool"
                ? new Effect(name, null, null, true)
                : new Effect(name, type, GetValue(a), false));
        }
        return list;
    }

    private static string? GetValue(JsonElement a)
    {
        if (!a.TryGetProperty("value", out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => NullIfBlank(v.GetString()),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    private static string? GetString(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static IReadOnlyList<string> GetStrings(JsonElement e, string property)
    {
        if (!e.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static bool GetBool(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? ToWikiUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return url;
        return url.StartsWith('/') ? WikiBase + url : null;
    }

    private static bool SameContent(CatalogItem a, CatalogItem b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
}
