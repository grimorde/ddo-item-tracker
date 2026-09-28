using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Presentation.Tests.Support;

internal static class SampleCatalog
{
    public static class Keys
    {
        public const string Gauntlet = "Absorption Gauntlet|18|Gloves";
        public const string Buckler = "Azure Buckler|18|Offhand";
        public const string ChainsBelt = "Chains|8|Belt";
        public const string ChainsNecklace = "Chains|8|Necklace";
        public const string Pendant = "Adherent's Pendant|11|Necklace";
        public const string Relic = "Relic|30|Trinket";
    }

    public static CatalogItem Item(string name, int minLevel, string slot, string? type = null, string? pack = null,
        string[]? quests = null, string[]? sets = null, bool artifact = false, Effect[]? effects = null, string[]? crafting = null) => new()
    {
        Key = ItemKey.For(name, minLevel, slot),
        Name = name,
        MinLevel = minLevel,
        Slot = slot,
        Type = type,
        Pack = pack,
        Quests = quests ?? [],
        SetNames = sets ?? [],
        IsArtifact = artifact,
        Effects = effects ?? [],
        CraftingSlots = crafting ?? [],
        WikiUrl = "https://ddowiki.com/page/Item:" + name.Replace(' ', '_'),
    };

    public static CatalogIndex Create() => new(new ItemCatalog(
        new CatalogVersion("83bc99b2a764cebe13e3c72c41f0580167575498", new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero), DateTimeOffset.UnixEpoch),
        [
            Item("Absorption Gauntlet", 18, "Gloves", "Hand items", "Vecna Unleashed", ["Taken in Hand"], ["Forbidden Knowledge"],
                effects: [new Effect("Magical Sheltering", "Insight", "10", false), new Effect("Immunity to Fear", null, null, true)],
                crafting: ["Yellow Augment Slot"]),
            Item("Adherent's Pendant", 11, "Necklace", "Neck items", "Demon Sands", ["The Chamber of Raiyum"], ["Oasis of Morality"]),
            Item("Azure Buckler", 18, "Offhand", "Shields", "Vecna Unleashed", ["Taken in Hand"], ["Forbidden Knowledge"]),
            Item("Chains", 8, "Belt", "Waist items", "Against the Slave Lords", ["Slave Pits of the Undercity"]),
            Item("Chains", 8, "Necklace", "Neck items", "Against the Slave Lords", ["Slave Pits of the Undercity"]),
            Item("Relic", 30, "Trinket", artifact: true),
        ],
        [
            new CatalogSet("Forbidden Knowledge",
            [
                new SetTier(3, [new Effect("Physical Sheltering", "Profane", "10", false)]),
                new SetTier(5, [new Effect("Melee Power", "Profane", "5", false), new Effect("Ranged Power", "Profane", "5", false)]),
            ]),
        ]));
}
