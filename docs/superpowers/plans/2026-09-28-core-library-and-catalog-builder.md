# DDO Item Tracker: Core Library and Catalog Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and test the UI-free heart of DDO Item Tracker (catalog conversion and validation, ownership rules, filtering, persistence, backups, Life Tracker import) plus the CatalogBuilder tool that produces the built-in `catalog.json`.

**Architecture:** A plain `net10.0` class library (`DdoItemTracker.Core`) with no MAUI or NuGet dependencies, tested by xUnit. A console tool (`tools/CatalogBuilder`) calls the same Core converter and validator the app will use. The MAUI app (plan 2) and the catalog updater (plan 3) build on these types.

**Tech Stack:** .NET 10, C#, System.Text.Json, xUnit 2.

**Spec:** `docs/superpowers/specs/2026-09-25-ddo-item-tracker-design.md`

**This is plan 1 of 3.**
- Plan 1 (this): Core library, tests, CatalogBuilder, built-in catalog.
- Plan 2: the .NET MAUI app (spec sections 3.1, 5, 7 UI).
- Plan 3: the in-app catalog update (spec 6.4 to 6.6, or 6.4a). Written after the owner chooses between 6.4 and 6.4a.

## Global Constraints

- SDK pinned by `global.json`: `"version": "10.0.300"`, `"rollForward": "latestPatch"` (copied from DDO Life Tracker).
- `DdoItemTracker.Core` targets `net10.0`, references no MAUI assemblies and no NuGet packages.
- `Nullable` and `ImplicitUsings` enabled in every project.
- Servers are exactly `Cormyr`, `Moonsea`, `Shadowdale`, `Thrane`. No Lamannia.
- Item identity is `Key = Name|MinLevel|Slot`.
- Update validation thresholds: new item count at least 95% of current; invalid records at most 1% of input.
- Upstream is `illusionistpm/ddo-gear-planner`, branch `master`, files `data/items.json` and `data/sets.json`. Downloads are always pinned to a commit SHA.
- The built-in catalog is written to `src/DdoItemTracker/Resources/Raw/catalog.json`.
- User-facing messages are plain English. No em dash characters anywhere in code, messages or docs (use `-`).
- Commits are authored as `grimorde <grimorde@gmail.com>` (already set in the repo's local git config). End commit messages with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Upstream effects with no bonus type** (54 real records, for example `Required Class: Paladin (UMD` on Adherent's Pendant): converted with `BonusType = null`, never dropped, never a crash. Test in Task 2.
2. **A zero-byte or whitespace-only `tracker.json`** (a crash mid-write on a phone, a full disk): treated as unreadable, the app recovers from `tracker.json.bak`, and the bad file is kept as a copy. Test in Task 8.
3. **Server names in a Life Tracker file with odd case or spaces** (`"thrane "`): matched to the canonical `Thrane`, not skipped. Test in Task 10.
4. **Character names that differ only by case or surrounding spaces on one server** (`"grimorde "` vs `"Grimorde"`): treated as the same name and rejected as a duplicate. Test in Task 6.
5. **Search text with surrounding spaces or different case** (`"  absorption "`): still matches `Absorption Gauntlet`. Test in Task 7.

---

## File Structure

```
DdoItemTracker/
├── global.json
├── nuget.config
├── DdoItemTracker.slnx
├── README.md
├── src/
│   ├── DdoItemTracker.Core/
│   │   ├── DdoItemTracker.Core.csproj
│   │   ├── Catalog/
│   │   │   ├── ItemKey.cs              key format
│   │   │   ├── CatalogModels.cs        Effect, SetTier, CatalogSet, CatalogItem, CatalogVersion, ItemCatalog
│   │   │   ├── CatalogConverter.cs     upstream JSON -> ItemCatalog + ConversionReport
│   │   │   ├── CatalogValidator.cs     hard/soft rules
│   │   │   ├── CatalogDiff.cs          added/removed keys, orphaned copies
│   │   │   ├── CatalogSerializer.cs    catalog.json read/write
│   │   │   ├── CatalogIndex.cs         lookups, set members, filter option lists
│   │   │   ├── UpstreamSource.cs       GitHub URLs
│   │   │   └── GitHubCommitInfo.cs     parse GitHub commit JSON
│   │   ├── Ownership/
│   │   │   ├── Servers.cs
│   │   │   ├── OwnershipModels.cs      StorageType, Character, Folder, OwnedCopy, TrackerData
│   │   │   ├── OwnershipRules.cs       copy validation
│   │   │   ├── TrackerRuleException.cs
│   │   │   └── TrackerOperations.cs    add/rename/delete characters, folders, copies
│   │   ├── Filtering/
│   │   │   ├── ItemQuery.cs            ItemFilter, OwnershipFilter, catalog filtering, owned counts
│   │   │   └── CopyQuery.cs            CopyFilter, OwnedCopyRow, My Items query, summary
│   │   ├── Persistence/
│   │   │   ├── TrackerJson.cs          shared serializer options
│   │   │   └── TrackerStore.cs         atomic save, .bak recovery
│   │   └── Import/
│   │       ├── TrackerBackupService.cs own backup export/import
│   │       ├── LifeTrackerBackupReader.cs
│   │       └── LifeTrackerImporter.cs
├── tests/
│   └── DdoItemTracker.Core.Tests/
│       ├── DdoItemTracker.Core.Tests.csproj
│       ├── Fixtures/items.sample.json, Fixtures/sets.sample.json
│       ├── Support/Fixtures.cs, Support/TestCatalogs.cs
│       ├── Catalog/*Tests.cs, Ownership/*Tests.cs, Filtering/*Tests.cs, Persistence/*Tests.cs, Import/*Tests.cs
└── tools/
    └── CatalogBuilder/
        ├── CatalogBuilder.csproj
        └── Program.cs
```

---

### Task 1: Solution scaffold and item key

**Files:**
- Create: `global.json`, `nuget.config`, `DdoItemTracker.slnx`, `README.md`
- Create: `src/DdoItemTracker.Core/DdoItemTracker.Core.csproj`, `src/DdoItemTracker.Core/Catalog/ItemKey.cs`
- Create: `tests/DdoItemTracker.Core.Tests/DdoItemTracker.Core.Tests.csproj`
- Test: `tests/DdoItemTracker.Core.Tests/Catalog/ItemKeyTests.cs`

**Interfaces:**
- Produces: `DdoItemTracker.Core.Catalog.ItemKey.For(string name, int minLevel, string slot) : string`

- [ ] **Step 1: Create build files**

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

`nuget.config`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

`DdoItemTracker.slnx`:
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/DdoItemTracker.Core/DdoItemTracker.Core.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/DdoItemTracker.Core.Tests/DdoItemTracker.Core.Tests.csproj" />
  </Folder>
</Solution>
```

`src/DdoItemTracker.Core/DdoItemTracker.Core.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>DdoItemTracker.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

`tests/DdoItemTracker.Core.Tests/DdoItemTracker.Core.Tests.csproj` (if restore cannot find a listed package version, use the latest stable version from nuget.org):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <RootNamespace>DdoItemTracker.Core.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DdoItemTracker.Core\DdoItemTracker.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

`README.md`:
```markdown
# DDO Item Tracker

Track the named items you own in Dungeons & Dragons Online, and where each copy is held, across characters and servers.

Item and set data comes from [illusionistpm/ddo-gear-planner](https://github.com/illusionistpm/ddo-gear-planner), which is scraped from [ddowiki](https://ddowiki.com).

## Build and test

    dotnet test

## Refresh the built-in catalog

    dotnet run --project tools/CatalogBuilder -- --out src/DdoItemTracker/Resources/Raw/catalog.json

See `docs/superpowers/specs/` for the design.
```

- [ ] **Step 2: Write the failing test**

`tests/DdoItemTracker.Core.Tests/Catalog/ItemKeyTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class ItemKeyTests
{
    [Fact]
    public void For_JoinsNameLevelAndSlot()
    {
        Assert.Equal(
            "Cloak of Winter's End (level 8)|8|Cloak",
            ItemKey.For("Cloak of Winter's End (level 8)", 8, "Cloak"));
    }

    [Fact]
    public void For_SameNameDifferentSlot_GivesDifferentKeys()
    {
        Assert.NotEqual(ItemKey.For("Chains", 8, "Belt"), ItemKey.For("Chains", 8, "Necklace"));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test`
Expected: build FAILS with `The name 'ItemKey' does not exist in the current context`.

- [ ] **Step 4: Write minimal implementation**

`src/DdoItemTracker.Core/Catalog/ItemKey.cs`:
```csharp
using System.Globalization;

namespace DdoItemTracker.Core.Catalog;

/// <summary>An item's identity across catalog versions: name, minimum level and slot.</summary>
public static class ItemKey
{
    public static string For(string name, int minLevel, string slot) =>
        $"{name}|{minLevel.ToString(CultureInfo.InvariantCulture)}|{slot}";
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test`
Expected: PASS, 2 tests.

- [ ] **Step 6: Commit**

```bash
git add global.json nuget.config DdoItemTracker.slnx README.md src tests
git commit -m "Scaffold solution with Core library and item key" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Catalog models and converter

**Files:**
- Create: `src/DdoItemTracker.Core/Catalog/CatalogModels.cs`, `src/DdoItemTracker.Core/Catalog/CatalogConverter.cs`
- Create: `tests/DdoItemTracker.Core.Tests/Fixtures/items.sample.json`, `tests/DdoItemTracker.Core.Tests/Fixtures/sets.sample.json`, `tests/DdoItemTracker.Core.Tests/Support/Fixtures.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Catalog/CatalogConverterTests.cs`

**Interfaces:**
- Consumes: `ItemKey.For`
- Produces:
  - `record Effect(string Name, string? BonusType, string? Value, bool IsToggle)`
  - `record SetTier(int PiecesRequired, IReadOnlyList<Effect> Effects)`
  - `record CatalogSet(string Name, IReadOnlyList<SetTier> Tiers)`
  - `record CatalogItem { Key, Name, MinLevel, Slot, Type?, Pack?, Quests, Effects, CraftingSlots, SetNames, IsRare, IsArtifact, WikiUrl? }`
  - `record CatalogVersion(string UpstreamCommit, DateTimeOffset UpstreamCommitDateUtc, DateTimeOffset BuiltUtc)`
  - `record ItemCatalog(CatalogVersion Version, IReadOnlyList<CatalogItem> Items, IReadOnlyList<CatalogSet> Sets)`
  - `record ConversionReport(int InputItemCount, int DroppedInvalid, int DroppedDuplicate, IReadOnlyList<string> DuplicateKeys, IReadOnlyList<string> UnresolvedSetNames)`
  - `record ConversionResult(ItemCatalog Catalog, ConversionReport Report)`
  - `CatalogConverter.Convert(string itemsJson, string setsJson, CatalogVersion version) : ConversionResult` (throws `InvalidDataException` for unparseable input)

The catalog type is named `ItemCatalog`, not `Catalog`, because `Catalog` is also a namespace segment and the clash breaks type lookups.

- [ ] **Step 1: Create the fixtures**

These are real gear-planner records (commit `1bc6224`, 2026-09-25) with two deliberate additions, noted in the test: `Broken Record` (no slot) and the Forbidden Knowledge tiers stored out of order.

`tests/DdoItemTracker.Core.Tests/Fixtures/items.sample.json`:
```json
[
  {"affixes":[{"name":"Elemental Resistance","type":"Competence","value":"5"},{"name":"Magical Sheltering","type":"Insight","value":"10"},{"name":"Spellcraft","type":"Quality","value":"4"},{"name":"Fire Absorption","type":"Enhancement","value":"26"}],"crafting":["Yellow Augment Slot"],"ml":18,"name":"Absorption Gauntlet","pack":"Vecna Unleashed","quests":["Taken in Hand"],"sets":["Forbidden Knowledge"],"slot":"Gloves","type":"Hand items","url":"/page/Item:Absorption_Gauntlet"},
  {"affixes":[{"name":"Enhancement Bonus (Weapon)","type":"Enhancement","value":"+15"},{"name":"Ranged Alacrity","type":"Enhancement","value":"20"},{"name":"Elasticity","type":"Bool","value":1},{"name":"Dripping with Magma","type":"Bool","value":1}],"crafting":["Sealed in Fire","Orange Augment Slot","Purple Augment Slot"],"ml":33,"name":"Aeon, the Blazing Reign","pack":"Magic of Myth Drannor","quests":["Threats Old and New"],"slot":"Weapon","type":"Long Bows","url":"/page/Item:Aeon,_the_Blazing_Reign"},
  {"affixes":[{"name":"Enhancement Bonus (Weapon)","type":"Enhancement","value":"+15"},{"name":"Ranged Alacrity","type":"Enhancement","value":"20"},{"name":"Elasticity","type":"Bool","value":1},{"name":"Dripping with Magma","type":"Bool","value":1}],"crafting":["Sealed in Fire","Orange Augment Slot","Purple Augment Slot"],"ml":33,"name":"Aeon, the Blazing Reign","pack":"Magic of Myth Drannor","quests":["Threats Old and New"],"slot":"Weapon","type":"Long Bows","url":"/page/Item:Aeon,_the_Blazing_Reign"},
  {"affixes":[],"crafting":["Slaver's Bonus Slot","Slaver's Extra Slot","Slaver's Prefix Slot","Slaver's Set Bonus","Slaver's Suffix Slot","Green Augment Slot"],"ml":8,"name":"Chains","pack":"Against the Slave Lords","quests":["Slave Pits of the Undercity"],"slot":"Belt","type":"Waist items","url":"/page/Item:Chains"},
  {"affixes":[],"crafting":["Slaver's Bonus Slot","Slaver's Extra Slot","Slaver's Prefix Slot","Slaver's Set Bonus","Slaver's Suffix Slot","Green Augment Slot"],"ml":8,"name":"Chains","pack":"Against the Slave Lords","quests":["Slave Pits of the Undercity"],"slot":"Necklace","type":"Neck items","url":"/page/Item:Chains"},
  {"affixes":[{"name":"Force Absorption","type":"Enhancement","value":"11"},{"name":"False Life","type":"Vitality","value":"11"},{"name":"Immunity to Fear","type":"Bool","value":1}],"crafting":["Sun Augment Slot","Blue Augment Slot"],"ml":4,"name":"Cloak of Winter's End (level 4)","quests":["Timeline Fragment Exchange"],"slot":"Cloak","type":"Back items","url":"/page/Item:Cloak_of_Winter%27s_End_(level_4)"},
  {"affixes":[{"name":"Force Absorption","type":"Enhancement","value":"12"},{"name":"False Life","type":"Vitality","value":"16"},{"name":"Immunity to Fear","type":"Bool","value":1}],"crafting":["Sun Augment Slot","Blue Augment Slot"],"ml":8,"name":"Cloak of Winter's End (level 8)","quests":["Timeline Fragment Exchange"],"slot":"Cloak","type":"Back items","url":"/page/Item:Cloak_of_Winter%27s_End_(level_8)"},
  {"affixes":[{"name":"Required Class: Paladin (UMD","value":"0"},{"name":"Healing Lore","type":"Equipment","value":"12"},{"name":"Positive Spell Power","type":"Equipment","value":"80"},{"name":"Wizardry","type":"Enhancement","value":"121"},{"name":"Action Boost Charges","type":"Enhancement","value":"3"}],"crafting":["Yellow Augment Slot"],"ml":11,"name":"Adherent's Pendant","pack":"Demon Sands","quests":["The Chamber of Raiyum"],"sets":["Oasis of Morality"],"slot":"Necklace","type":"Neck items","url":"/page/Item:Adherent%27s_Pendant"},
  {"name":"Broken Record","ml":5}
]
```

`tests/DdoItemTracker.Core.Tests/Fixtures/sets.sample.json`:
```json
{
  "Forbidden Knowledge": [
    {"affixes":[{"name":"Melee Power","type":"Profane","value":"5"},{"name":"Ranged Power","type":"Profane","value":"5"},{"name":"Universal Spell Power","type":"Profane","value":"10"},{"name":"Spell Focus Mastery","type":"Profane","value":"1"}],"threshold":5},
    {"affixes":[{"name":"Physical Sheltering","type":"Profane","value":"10"}],"threshold":3},
    {"affixes":[{"name":"Well Rounded","type":"Profane","value":"1"},{"name":"Accuracy","type":"Profane","value":"1"},{"name":"Deadly","type":"Profane","value":"1"}],"threshold":4}
  ]
}
```

`tests/DdoItemTracker.Core.Tests/Support/Fixtures.cs`:
```csharp
using System.Globalization;
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Support;

internal static class Fixtures
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static CatalogVersion Version { get; } = new(
        "fixture",
        DateTimeOffset.Parse("2026-09-25T10:20:33Z", CultureInfo.InvariantCulture),
        DateTimeOffset.UnixEpoch);

    public static ConversionResult ConvertSample() =>
        CatalogConverter.Convert(Read("items.sample.json"), Read("sets.sample.json"), Version);
}
```

- [ ] **Step 2: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Catalog/CatalogConverterTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogConverterTests
{
    private static CatalogItem Get(ConversionResult r, string key) =>
        Assert.Single(r.Catalog.Items, i => i.Key == key);

    [Fact]
    public void Counts_InputInvalidAndDuplicateRecords()
    {
        var r = Fixtures.ConvertSample();

        Assert.Equal(9, r.Report.InputItemCount);
        Assert.Equal(1, r.Report.DroppedInvalid);     // Broken Record has no slot
        Assert.Equal(1, r.Report.DroppedDuplicate);   // Aeon appears twice, identical
        Assert.Empty(r.Report.DuplicateKeys);
        Assert.Equal(7, r.Catalog.Items.Count);
    }

    [Fact]
    public void SameNameOnDifferentSlots_IsKeptAsTwoItems()
    {
        var r = Fixtures.ConvertSample();
        Get(r, "Chains|8|Belt");
        Get(r, "Chains|8|Necklace");
    }

    [Fact]
    public void LevelScaledItems_GetDistinctKeys()
    {
        var r = Fixtures.ConvertSample();
        Get(r, "Cloak of Winter's End (level 4)|4|Cloak");
        Get(r, "Cloak of Winter's End (level 8)|8|Cloak");
    }

    [Fact]
    public void MapsAllItemFields()
    {
        var item = Get(Fixtures.ConvertSample(), "Absorption Gauntlet|18|Gloves");

        Assert.Equal("Absorption Gauntlet", item.Name);
        Assert.Equal(18, item.MinLevel);
        Assert.Equal("Gloves", item.Slot);
        Assert.Equal("Hand items", item.Type);
        Assert.Equal("Vecna Unleashed", item.Pack);
        Assert.Equal(["Taken in Hand"], item.Quests);
        Assert.Equal(["Yellow Augment Slot"], item.CraftingSlots);
        Assert.Equal(["Forbidden Knowledge"], item.SetNames);
        Assert.Equal("https://ddowiki.com/page/Item:Absorption_Gauntlet", item.WikiUrl);
        Assert.Equal(new Effect("Magical Sheltering", "Insight", "10", false), item.Effects[1]);
        Assert.False(item.IsRare);
        Assert.False(item.IsArtifact);
    }

    [Fact]
    public void BoolAffix_BecomesToggle()
    {
        var item = Get(Fixtures.ConvertSample(), "Cloak of Winter's End (level 4)|4|Cloak");
        Assert.Equal(new Effect("Immunity to Fear", null, null, true), item.Effects[2]);
    }

    [Fact]
    public void AffixWithNoBonusType_IsKeptWithNullBonusType()
    {
        var item = Get(Fixtures.ConvertSample(), "Adherent's Pendant|11|Necklace");
        Assert.Equal(new Effect("Required Class: Paladin (UMD", null, "0", false), item.Effects[0]);
        Assert.Equal(5, item.Effects.Count);
    }

    [Fact]
    public void NumericAffixValue_IsStoredAsText()
    {
        const string items = """[{"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":3}]}]""";
        var r = CatalogConverter.Convert(items, "{}", Fixtures.Version);
        Assert.Equal("3", r.Catalog.Items[0].Effects[0].Value);
    }

    [Fact]
    public void SetTiers_AreOrderedByPiecesRequired()
    {
        var set = Assert.Single(Fixtures.ConvertSample().Catalog.Sets);
        Assert.Equal("Forbidden Knowledge", set.Name);
        Assert.Equal([3, 4, 5], set.Tiers.Select(t => t.PiecesRequired));
        Assert.Equal(new Effect("Physical Sheltering", "Profane", "10", false), set.Tiers[0].Effects[0]);
    }

    [Fact]
    public void SetNamesMissingFromSetsFile_AreReported()
    {
        Assert.Equal(["Oasis of Morality"], Fixtures.ConvertSample().Report.UnresolvedSetNames);
    }

    [Fact]
    public void ConflictingRecordsWithSameKey_AreReportedAndFirstIsKept()
    {
        const string items = """
            [{"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":"3"}]},
             {"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":"4"}]}]
            """;
        var r = CatalogConverter.Convert(items, "{}", Fixtures.Version);

        Assert.Equal(["X|1|Ring"], r.Report.DuplicateKeys);
        Assert.Equal(0, r.Report.DroppedDuplicate);
        Assert.Equal("3", Assert.Single(r.Catalog.Items).Effects[0].Value);
    }

    [Fact]
    public void Items_AreSortedByNameThenLevelThenSlot()
    {
        var names = Fixtures.ConvertSample().Catalog.Items.Select(i => i.Key).ToList();
        Assert.Equal(
            [
                "Absorption Gauntlet|18|Gloves",
                "Adherent's Pendant|11|Necklace",
                "Aeon, the Blazing Reign|33|Weapon",
                "Chains|8|Belt",
                "Chains|8|Necklace",
                "Cloak of Winter's End (level 4)|4|Cloak",
                "Cloak of Winter's End (level 8)|8|Cloak",
            ],
            names);
    }

    [Theory]
    [InlineData("not json", "{}")]
    [InlineData("{}", "{}")]      // items must be an array
    [InlineData("[]", "[]")]      // sets must be an object
    public void MalformedInput_ThrowsInvalidDataException(string items, string sets)
    {
        Assert.Throws<InvalidDataException>(() => CatalogConverter.Convert(items, sets, Fixtures.Version));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The type or namespace name 'ConversionResult' could not be found`.

- [ ] **Step 4: Write the models**

`src/DdoItemTracker.Core/Catalog/CatalogModels.cs`:
```csharp
namespace DdoItemTracker.Core.Catalog;

/// <summary>One enchantment line. Toggles (upstream type "Bool") have no bonus type or value.</summary>
public sealed record Effect(string Name, string? BonusType, string? Value, bool IsToggle);

public sealed record SetTier(int PiecesRequired, IReadOnlyList<Effect> Effects);

public sealed record CatalogSet(string Name, IReadOnlyList<SetTier> Tiers);

public sealed record CatalogItem
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int MinLevel { get; init; }
    public required string Slot { get; init; }
    public string? Type { get; init; }
    public string? Pack { get; init; }
    public IReadOnlyList<string> Quests { get; init; } = [];
    public IReadOnlyList<Effect> Effects { get; init; } = [];
    public IReadOnlyList<string> CraftingSlots { get; init; } = [];
    public IReadOnlyList<string> SetNames { get; init; } = [];
    public bool IsRare { get; init; }
    public bool IsArtifact { get; init; }
    public string? WikiUrl { get; init; }
}

public sealed record CatalogVersion(string UpstreamCommit, DateTimeOffset UpstreamCommitDateUtc, DateTimeOffset BuiltUtc);

public sealed record ItemCatalog(CatalogVersion Version, IReadOnlyList<CatalogItem> Items, IReadOnlyList<CatalogSet> Sets);
```

- [ ] **Step 5: Write the converter**

`src/DdoItemTracker.Core/Catalog/CatalogConverter.cs`:
```csharp
using System.Text.Json;

namespace DdoItemTracker.Core.Catalog;

public sealed record ConversionReport(
    int InputItemCount,
    int DroppedInvalid,
    int DroppedDuplicate,
    IReadOnlyList<string> DuplicateKeys,
    IReadOnlyList<string> UnresolvedSetNames);

public sealed record ConversionResult(ItemCatalog Catalog, ConversionReport Report);

/// <summary>Turns gear-planner's items.json and sets.json into an <see cref="ItemCatalog"/>.</summary>
public static class CatalogConverter
{
    private const string WikiBase = "https://ddowiki.com";

    public static ConversionResult Convert(string itemsJson, string setsJson, CatalogVersion version)
    {
        using var itemsDoc = ParseDocument(itemsJson, "items");
        using var setsDoc = ParseDocument(setsJson, "sets");
        if (itemsDoc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The items file is not a JSON array.");
        if (setsDoc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The sets file is not a JSON object.");

        var byKey = new Dictionary<string, CatalogItem>(StringComparer.Ordinal);
        var duplicateKeys = new SortedSet<string>(StringComparer.Ordinal);
        int input = 0, invalid = 0, duplicate = 0;

        foreach (var element in itemsDoc.RootElement.EnumerateArray())
        {
            input++;
            var item = MapItem(element);
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

    private static CatalogItem? MapItem(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var name = GetString(e, "name")?.Trim();
        var slot = GetString(e, "slot")?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(slot)) return null;
        if (!e.TryGetProperty("ml", out var ml) || ml.ValueKind != JsonValueKind.Number || !ml.TryGetInt32(out var minLevel))
            return null;

        return new CatalogItem
        {
            Key = ItemKey.For(name, minLevel, slot),
            Name = name,
            MinLevel = minLevel,
            Slot = slot,
            Type = NullIfBlank(GetString(e, "type")),
            Pack = NullIfBlank(GetString(e, "pack")),
            Quests = GetStrings(e, "quests"),
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
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "Add catalog models and gear-planner converter" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Catalog validator and diff

**Files:**
- Create: `src/DdoItemTracker.Core/Catalog/CatalogValidator.cs`, `src/DdoItemTracker.Core/Catalog/CatalogDiff.cs`
- Create: `tests/DdoItemTracker.Core.Tests/Support/TestCatalogs.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Catalog/CatalogValidatorTests.cs`, `tests/DdoItemTracker.Core.Tests/Catalog/CatalogDiffTests.cs`

**Interfaces:**
- Consumes: `ItemCatalog`, `ConversionResult`, `ConversionReport`, `CatalogItem`, `ItemKey`
- Produces:
  - `record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) { bool IsValid }`
  - `CatalogValidator.Validate(ItemCatalog candidate, ItemCatalog? current) : ValidationResult`
  - `CatalogValidator.Validate(ConversionResult candidate, ItemCatalog? current) : ValidationResult`
  - `CatalogValidator.MinimumRetainedPercent = 95`, `CatalogValidator.MaximumInvalidPercent = 1`
  - `record CatalogDiff(IReadOnlyList<string> AddedKeys, IReadOnlyList<string> RemovedKeys, int OrphanedCopyCount)` with `static CatalogDiff Compute(ItemCatalog? current, ItemCatalog candidate, IEnumerable<string> ownedItemKeys)`
  - Test helper `TestCatalogs.Item(...)`, `TestCatalogs.Catalog(...)`, `TestCatalogs.WithItemCount(int)`

- [ ] **Step 1: Write the test helper**

`tests/DdoItemTracker.Core.Tests/Support/TestCatalogs.cs`:
```csharp
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Support;

internal static class TestCatalogs
{
    public static CatalogItem Item(
        string name,
        int minLevel = 1,
        string slot = "Ring",
        string[]? sets = null,
        string? type = null,
        string? pack = null,
        string[]? quests = null,
        bool artifact = false) => new()
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
    };

    public static ItemCatalog Catalog(IEnumerable<CatalogItem> items, IEnumerable<CatalogSet>? sets = null) => new(
        new CatalogVersion("test", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
        items.ToList(),
        sets?.ToList() ?? new List<CatalogSet> { new("Test Set", []) });

    public static ItemCatalog WithItemCount(int count) =>
        Catalog(Enumerable.Range(1, count).Select(n => Item($"Item {n}")));

    public static ConversionReport CleanReport(int input) => new(input, 0, 0, [], []);
}
```

- [ ] **Step 2: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Catalog/CatalogValidatorTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    [Fact]
    public void ValidCatalog_Passes()
    {
        var result = CatalogValidator.Validate(TestCatalogs.WithItemCount(100), TestCatalogs.WithItemCount(100));
        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void NoItems_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([]), null);
        Assert.Contains("The catalog has no items.", result.Errors);
    }

    [Fact]
    public void NoSets_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([TestCatalogs.Item("A")], []), null);
        Assert.Contains("The catalog has no sets.", result.Errors);
    }

    [Fact]
    public void DuplicateKeyInCatalog_Fails()
    {
        var a = TestCatalogs.Item("A");
        var result = CatalogValidator.Validate(TestCatalogs.Catalog([a, a]), null);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("A|1|Ring"));
    }

    [Fact]
    public void ItemCountBelow95Percent_Fails()
    {
        var result = CatalogValidator.Validate(TestCatalogs.WithItemCount(94), TestCatalogs.WithItemCount(100));
        Assert.Contains("Item count dropped from 100 to 94.", result.Errors);
    }

    [Fact]
    public void ItemCountAtExactly95Percent_Passes()
    {
        Assert.True(CatalogValidator.Validate(TestCatalogs.WithItemCount(95), TestCatalogs.WithItemCount(100)).IsValid);
    }

    [Fact]
    public void NoCurrentCatalog_SkipsCountRule()
    {
        Assert.True(CatalogValidator.Validate(TestCatalogs.WithItemCount(1), null).IsValid);
    }

    [Fact]
    public void MoreThanOnePercentInvalid_Fails()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(98), new ConversionReport(100, 2, 0, [], []));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.Contains("2 of 100 item records are missing a name, level or slot.", result.Errors);
    }

    [Fact]
    public void ExactlyOnePercentInvalid_Passes()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(99), new ConversionReport(100, 1, 0, [], []));
        Assert.True(CatalogValidator.Validate(conversion, null).IsValid);
    }

    [Fact]
    public void ConflictingKeysInReport_Fails()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(10), new ConversionReport(11, 0, 0, ["X|1|Ring"], []));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.Contains(result.Errors, e => e.Contains("X|1|Ring"));
    }

    [Fact]
    public void UnresolvedSetNames_WarnButPass()
    {
        var conversion = new ConversionResult(TestCatalogs.WithItemCount(10), new ConversionReport(10, 0, 0, [], ["Oasis of Morality"]));
        var result = CatalogValidator.Validate(conversion, null);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Oasis of Morality"));
    }

    [Fact]
    public void RealFixture_FailsInvalidRecordRule()
    {
        // The fixture deliberately contains Broken Record: 1 of 9 records invalid is over the 1% limit.
        Assert.False(CatalogValidator.Validate(Fixtures.ConvertSample(), null).IsValid);
    }
}
```

`tests/DdoItemTracker.Core.Tests/Catalog/CatalogDiffTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogDiffTests
{
    [Fact]
    public void Compute_ReportsAddedRemovedAndOrphanedCopies()
    {
        var current = TestCatalogs.Catalog([TestCatalogs.Item("A"), TestCatalogs.Item("B")]);
        var candidate = TestCatalogs.Catalog([TestCatalogs.Item("B"), TestCatalogs.Item("C")]);
        string[] owned = ["A|1|Ring", "A|1|Ring", "B|1|Ring"];

        var diff = CatalogDiff.Compute(current, candidate, owned);

        Assert.Equal(["C|1|Ring"], diff.AddedKeys);
        Assert.Equal(["A|1|Ring"], diff.RemovedKeys);
        Assert.Equal(2, diff.OrphanedCopyCount);
    }

    [Fact]
    public void Compute_WithNoCurrentCatalog_TreatsEverythingAsAdded()
    {
        var diff = CatalogDiff.Compute(null, TestCatalogs.Catalog([TestCatalogs.Item("A")]), []);
        Assert.Equal(["A|1|Ring"], diff.AddedKeys);
        Assert.Empty(diff.RemovedKeys);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The name 'CatalogValidator' does not exist in the current context`.

- [ ] **Step 4: Write the implementation**

`src/DdoItemTracker.Core/Catalog/CatalogValidator.cs`:
```csharp
namespace DdoItemTracker.Core.Catalog;

public sealed record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Decides whether a candidate catalog is safe to replace the current one (spec 6.3).</summary>
public static class CatalogValidator
{
    public const int MinimumRetainedPercent = 95;
    public const int MaximumInvalidPercent = 1;

    public static ValidationResult Validate(ItemCatalog candidate, ItemCatalog? current)
    {
        var errors = new List<string>();
        if (candidate.Items.Count == 0) errors.Add("The catalog has no items.");
        if (candidate.Sets.Count == 0) errors.Add("The catalog has no sets.");

        var duplicates = candidate.Items
            .GroupBy(i => i.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            errors.Add(FormattableString.Invariant($"{duplicates.Count} item key(s) are used by more than one item, for example \"{duplicates[0]}\"."));

        if (current is not null && current.Items.Count > 0
            && (long)candidate.Items.Count * 100 < (long)current.Items.Count * MinimumRetainedPercent)
            errors.Add(FormattableString.Invariant($"Item count dropped from {current.Items.Count:N0} to {candidate.Items.Count:N0}."));

        return new ValidationResult(errors, []);
    }

    public static ValidationResult Validate(ConversionResult candidate, ItemCatalog? current)
    {
        var baseline = Validate(candidate.Catalog, current);
        var errors = baseline.Errors.ToList();
        var warnings = baseline.Warnings.ToList();
        var report = candidate.Report;

        if (report.DuplicateKeys.Count > 0)
            errors.Add(FormattableString.Invariant($"{report.DuplicateKeys.Count} item key(s) belong to records with different content, for example \"{report.DuplicateKeys[0]}\"."));

        if ((long)report.DroppedInvalid * 100 > (long)report.InputItemCount * MaximumInvalidPercent)
            errors.Add(FormattableString.Invariant($"{report.DroppedInvalid:N0} of {report.InputItemCount:N0} item records are missing a name, level or slot."));

        if (report.UnresolvedSetNames.Count > 0)
            warnings.Add(FormattableString.Invariant($"{report.UnresolvedSetNames.Count} set name(s) have no bonus details, for example \"{report.UnresolvedSetNames[0]}\"."));

        return new ValidationResult(errors, warnings);
    }
}
```

`src/DdoItemTracker.Core/Catalog/CatalogDiff.cs`:
```csharp
namespace DdoItemTracker.Core.Catalog;

public sealed record CatalogDiff(IReadOnlyList<string> AddedKeys, IReadOnlyList<string> RemovedKeys, int OrphanedCopyCount)
{
    /// <param name="ownedItemKeys">One entry per owned copy, so two copies of a removed item count twice.</param>
    public static CatalogDiff Compute(ItemCatalog? current, ItemCatalog candidate, IEnumerable<string> ownedItemKeys)
    {
        var oldKeys = current?.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var newKeys = candidate.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);

        var added = newKeys.Where(k => !oldKeys.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var removed = oldKeys.Where(k => !newKeys.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var orphaned = ownedItemKeys.Count(k => !newKeys.Contains(k));
        return new CatalogDiff(added, removed, orphaned);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add catalog validator and diff" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Catalog serializer and index

**Files:**
- Create: `src/DdoItemTracker.Core/Catalog/CatalogSerializer.cs`, `src/DdoItemTracker.Core/Catalog/CatalogIndex.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Catalog/CatalogSerializerTests.cs`, `tests/DdoItemTracker.Core.Tests/Catalog/CatalogIndexTests.cs`

**Interfaces:**
- Consumes: `ItemCatalog`, `CatalogItem`, `CatalogSet`
- Produces:
  - `CatalogSerializer.Serialize(ItemCatalog) : string`, `CatalogSerializer.Deserialize(string) : ItemCatalog` (throws `InvalidDataException("The catalog file is damaged.")`)
  - `class CatalogIndex(ItemCatalog catalog)` with `Catalog`, `Find(string key) : CatalogItem?`, `FindSet(string name) : CatalogSet?`, `SetMembers(string setName) : IReadOnlyList<CatalogItem>`, and sorted option lists `Slots`, `Types`, `Packs`, `Quests` (`IReadOnlyList<string>`)

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Catalog/CatalogSerializerTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesContent()
    {
        var catalog = Fixtures.ConvertSample().Catalog;
        var json = CatalogSerializer.Serialize(catalog);
        var back = CatalogSerializer.Deserialize(json);

        Assert.Equal(json, CatalogSerializer.Serialize(back));
        Assert.Equal(catalog.Items.Count, back.Items.Count);
        Assert.Equal(catalog.Version, back.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public void DamagedFile_ThrowsInvalidDataException(string json)
    {
        var ex = Assert.Throws<InvalidDataException>(() => CatalogSerializer.Deserialize(json));
        Assert.Equal("The catalog file is damaged.", ex.Message);
    }
}
```

`tests/DdoItemTracker.Core.Tests/Catalog/CatalogIndexTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogIndexTests
{
    private static CatalogIndex Sample() => new(Fixtures.ConvertSample().Catalog);

    [Fact]
    public void Find_ReturnsItemByKey_OrNull()
    {
        var index = Sample();
        Assert.Equal("Absorption Gauntlet", index.Find("Absorption Gauntlet|18|Gloves")?.Name);
        Assert.Null(index.Find("Nothing|1|Ring"));
    }

    [Fact]
    public void FindSet_ReturnsSet_OrNullForUnresolved()
    {
        var index = Sample();
        Assert.NotNull(index.FindSet("Forbidden Knowledge"));
        Assert.Null(index.FindSet("Oasis of Morality"));
    }

    [Fact]
    public void SetMembers_ListsItemsInTheSet()
    {
        var catalog = TestCatalogs.Catalog([
            TestCatalogs.Item("Zeta Ring", sets: ["S"]),
            TestCatalogs.Item("Alpha Ring", sets: ["S"]),
            TestCatalogs.Item("Other"),
        ]);
        var members = new CatalogIndex(catalog).SetMembers("S");
        Assert.Equal(["Alpha Ring", "Zeta Ring"], members.Select(m => m.Name));
        Assert.Empty(new CatalogIndex(catalog).SetMembers("Missing"));
    }

    [Fact]
    public void OptionLists_AreDistinctAndSorted()
    {
        var index = Sample();
        Assert.Equal(["Belt", "Cloak", "Gloves", "Necklace", "Weapon"], index.Slots);
        Assert.Contains("Vecna Unleashed", index.Packs);
        Assert.Contains("Timeline Fragment Exchange", index.Quests);
        Assert.Equal(index.Types.Distinct().Count(), index.Types.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The name 'CatalogSerializer' does not exist in the current context`.

- [ ] **Step 3: Write the implementation**

`src/DdoItemTracker.Core/Catalog/CatalogSerializer.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdoItemTracker.Core.Catalog;

public static class CatalogSerializer
{
    private const string DamagedMessage = "The catalog file is damaged.";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(ItemCatalog catalog) => JsonSerializer.Serialize(catalog, Options);

    public static ItemCatalog Deserialize(string json)
    {
        ItemCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<ItemCatalog>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(DamagedMessage, ex);
        }
        if (catalog?.Version is null || catalog.Items is null || catalog.Sets is null)
            throw new InvalidDataException(DamagedMessage);
        return catalog;
    }
}
```

`src/DdoItemTracker.Core/Catalog/CatalogIndex.cs`:
```csharp
namespace DdoItemTracker.Core.Catalog;

/// <summary>Fast lookups over a loaded catalog.</summary>
public sealed class CatalogIndex
{
    private readonly Dictionary<string, CatalogItem> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogSet> _sets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<CatalogItem>> _members;

    public CatalogIndex(ItemCatalog catalog)
    {
        Catalog = catalog;
        foreach (var item in catalog.Items) _byKey.TryAdd(item.Key, item);
        foreach (var set in catalog.Sets) _sets.TryAdd(set.Name, set);

        _members = catalog.Items
            .SelectMany(i => i.SetNames.Select(n => (Set: n, Item: i)))
            .GroupBy(p => p.Set, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CatalogItem>)g.Select(p => p.Item)
                    .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.MinLevel)
                    .ThenBy(i => i.Slot, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        Slots = DistinctSorted(catalog.Items.Select(i => i.Slot));
        Types = DistinctSorted(catalog.Items.Select(i => i.Type));
        Packs = DistinctSorted(catalog.Items.Select(i => i.Pack));
        Quests = DistinctSorted(catalog.Items.SelectMany(i => i.Quests));
    }

    public ItemCatalog Catalog { get; }
    public IReadOnlyList<string> Slots { get; }
    public IReadOnlyList<string> Types { get; }
    public IReadOnlyList<string> Packs { get; }
    public IReadOnlyList<string> Quests { get; }

    public CatalogItem? Find(string key) => _byKey.GetValueOrDefault(key);

    public CatalogSet? FindSet(string name) => _sets.GetValueOrDefault(name);

    public IReadOnlyList<CatalogItem> SetMembers(string setName) =>
        _members.TryGetValue(setName, out var members) ? members : [];

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add catalog serializer and index" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Upstream helpers, CatalogBuilder tool and built-in catalog

**Files:**
- Create: `src/DdoItemTracker.Core/Catalog/UpstreamSource.cs`, `src/DdoItemTracker.Core/Catalog/GitHubCommitInfo.cs`
- Create: `tools/CatalogBuilder/CatalogBuilder.csproj`, `tools/CatalogBuilder/Program.cs`
- Modify: `DdoItemTracker.slnx` (add the tool)
- Create (generated): `src/DdoItemTracker/Resources/Raw/catalog.json`
- Test: `tests/DdoItemTracker.Core.Tests/Catalog/UpstreamSourceTests.cs`, `tests/DdoItemTracker.Core.Tests/Catalog/GitHubCommitInfoTests.cs`

**Interfaces:**
- Consumes: `CatalogConverter`, `CatalogValidator`, `CatalogSerializer`, `CatalogVersion`
- Produces:
  - `record UpstreamSource(string Repo, string Branch, string ItemsPath, string SetsPath)` with `static Default`, `RawUrl(string commit, string path)`, `LatestCommitApiUrl(string path)`, `CommitApiUrl(string commit)`
  - `record GitHubCommitInfo(string Sha, DateTimeOffset CommitDateUtc)` with `static Parse(string json)` (accepts a single commit object or an array; throws `InvalidDataException`)
  - CLI: `CatalogBuilder --out <path> [--commit <sha>] [--items <file> --sets <file> --commit <sha> --date <iso>] [--previous <catalog.json>]`. Exit codes: 0 written, 1 validation failed, 2 usage error.

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Catalog/UpstreamSourceTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class UpstreamSourceTests
{
    [Fact]
    public void Default_PointsAtGearPlannerDataFolder()
    {
        var s = UpstreamSource.Default;
        Assert.Equal("illusionistpm/ddo-gear-planner", s.Repo);
        Assert.Equal("master", s.Branch);
        Assert.Equal("data/items.json", s.ItemsPath);
        Assert.Equal("data/sets.json", s.SetsPath);
    }

    [Fact]
    public void RawUrl_IsPinnedToCommit()
    {
        Assert.Equal(
            "https://raw.githubusercontent.com/illusionistpm/ddo-gear-planner/abc123/data/items.json",
            UpstreamSource.Default.RawUrl("abc123", "data/items.json"));
    }

    [Fact]
    public void LatestCommitApiUrl_FiltersByBranchAndPath()
    {
        Assert.Equal(
            "https://api.github.com/repos/illusionistpm/ddo-gear-planner/commits?sha=master&path=data%2Fitems.json&per_page=1",
            UpstreamSource.Default.LatestCommitApiUrl("data/items.json"));
    }

    [Fact]
    public void CommitApiUrl_AddressesOneCommit()
    {
        Assert.Equal(
            "https://api.github.com/repos/illusionistpm/ddo-gear-planner/commits/abc123",
            UpstreamSource.Default.CommitApiUrl("abc123"));
    }
}
```

`tests/DdoItemTracker.Core.Tests/Catalog/GitHubCommitInfoTests.cs`:
```csharp
using System.Globalization;
using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class GitHubCommitInfoTests
{
    private const string Commit = """
        {"sha":"1bc622480c85b5e2740b15562b9cbc5b70ce9e0c","commit":{"committer":{"date":"2026-09-25T10:20:33Z"}}}
        """;

    [Fact]
    public void Parse_SingleCommit()
    {
        var info = GitHubCommitInfo.Parse(Commit);
        Assert.Equal("1bc622480c85b5e2740b15562b9cbc5b70ce9e0c", info.Sha);
        Assert.Equal(DateTimeOffset.Parse("2026-09-25T10:20:33Z", CultureInfo.InvariantCulture), info.CommitDateUtc);
    }

    [Fact]
    public void Parse_ArrayTakesFirst()
    {
        Assert.Equal("1bc622480c85b5e2740b15562b9cbc5b70ce9e0c", GitHubCommitInfo.Parse($"[{Commit}]").Sha);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"message":"API rate limit exceeded"}""")]
    public void Parse_Unusable_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => GitHubCommitInfo.Parse(json));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The name 'UpstreamSource' does not exist in the current context`.

- [ ] **Step 3: Write the Core helpers**

`src/DdoItemTracker.Core/Catalog/UpstreamSource.cs`:
```csharp
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
```

`src/DdoItemTracker.Core/Catalog/GitHubCommitInfo.cs`:
```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Create the tool**

`tools/CatalogBuilder/CatalogBuilder.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DdoItemTracker.Core\DdoItemTracker.Core.csproj" />
  </ItemGroup>
</Project>
```

`tools/CatalogBuilder/Program.cs`:
```csharp
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
```

Add the tool to `DdoItemTracker.slnx`, inside `<Solution>` after the tests folder:
```xml
  <Folder Name="/tools/">
    <Project Path="tools/CatalogBuilder/CatalogBuilder.csproj" />
  </Folder>
```

- [ ] **Step 6: Prove the tool refuses a bad catalog**

Run: `dotnet run --project tools/CatalogBuilder -- --out %TEMP%/fixture-catalog.json --items tests/DdoItemTracker.Core.Tests/Fixtures/items.sample.json --sets tests/DdoItemTracker.Core.Tests/Fixtures/sets.sample.json --commit fixture --date 2026-09-25T10:20:33Z`
(In Git Bash use `$TEMP` instead of `%TEMP%`.)
Expected: exit code 1, output includes `Error: 1 of 9 item records are missing a name, level or slot.` and `Catalog NOT written.`, and no file is created.

- [ ] **Step 7: Build the real built-in catalog**

Run: `dotnet run --project tools/CatalogBuilder -- --out src/DdoItemTracker/Resources/Raw/catalog.json`
Expected: exit code 0. At the 2026-09-25 upstream commit the output was `Input records: 8207, dropped invalid: 0, dropped duplicate: 142, conflicting keys: 0` and `Items: 8065, sets: 282, unresolved set names: 0`. A later commit may differ slightly; any non-zero `conflicting keys` or an error means stop and investigate.

- [ ] **Step 8: Prove the item-count guard works against the real catalog**

Run the Step 6 command again but with `--previous src/DdoItemTracker/Resources/Raw/catalog.json` and a fixture that passes the invalid-record rule: first delete the `Broken Record` line from a scratch copy of `items.sample.json` in `%TEMP%`, then point `--items` at that copy.
Expected: exit code 1 with `Error: Item count dropped from 8,065 to 7.` (numbers match your Step 7 run). Delete the scratch copy afterwards.

- [ ] **Step 9: Run all tests and commit**

Run: `dotnet test`
Expected: PASS.

```bash
git add DdoItemTracker.slnx src tests tools
git commit -m "Add CatalogBuilder tool and built-in catalog" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Ownership model, rules and operations

**Files:**
- Create: `src/DdoItemTracker.Core/Ownership/Servers.cs`, `OwnershipModels.cs`, `OwnershipRules.cs`, `TrackerRuleException.cs`, `TrackerOperations.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Ownership/OwnershipRulesTests.cs`, `tests/DdoItemTracker.Core.Tests/Ownership/TrackerOperationsTests.cs`

**Interfaces:**
- Produces:
  - `Servers.All : IReadOnlyList<string>`, `Servers.Canonical(string?) : string?`
  - `enum StorageType { Equipped, Inventory, Bank, SharedBank }`
  - `class Character { Id, Server, Name, FolderId?, LifeTrackerId? }`
  - `class Folder { Id, Name }`
  - `class OwnedCopy { Id, ItemKey, ItemName, Server, Storage, CharacterId?, Note?, AddedUtc }`
  - `class TrackerData { SchemaVersion, Characters, Folders, OwnedCopies }`, `TrackerData.CurrentSchemaVersion = 1`
  - `OwnershipRules.ValidateCopy(TrackerData, OwnedCopy) : string?` (null when valid)
  - `TrackerRuleException(string message)`
  - `enum CharacterCopyDisposal { DeleteCopies, MoveToSharedBank }`
  - `TrackerOperations`: `AddCharacter(data, server, name, folderId = null) : Character`, `RenameCharacter(data, characterId, newName)`, `MoveCharacterToFolder(data, characterId, folderId?)`, `DeleteCharacter(data, characterId, disposal)`, `AddFolder(data, name) : Folder`, `DeleteFolder(data, folderId)`, `AddCopy(data, copy) : OwnedCopy`, `UpdateCopy(data, copy)`, `RemoveCopy(data, copyId)`, `IsSameName(string a, string b) : bool`. All throw `TrackerRuleException` with a user-facing message on a broken rule.

`UpdateCopy` must be given a **new** `OwnedCopy` instance carrying the existing `Id`, so a rejected edit leaves the stored copy unchanged.

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Ownership/OwnershipRulesTests.cs`:
```csharp
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Ownership;

public class OwnershipRulesTests
{
    private static (TrackerData Data, Character Grim) Setup()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        return (data, grim);
    }

    private static OwnedCopy Copy(StorageType storage, string server = "Cormyr", string? characterId = null) =>
        new() { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = server, Storage = storage, CharacterId = characterId };

    [Fact]
    public void SharedBankWithoutCharacter_IsValid()
    {
        var (data, _) = Setup();
        Assert.Null(OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank)));
    }

    [Fact]
    public void SharedBankWithCharacter_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Shared Bank items don't belong to a character.", OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank, characterId: grim.Id)));
    }

    [Theory]
    [InlineData(StorageType.Equipped)]
    [InlineData(StorageType.Inventory)]
    [InlineData(StorageType.Bank)]
    public void CharacterStorageWithoutCharacter_IsRejected(StorageType storage)
    {
        var (data, _) = Setup();
        Assert.Equal("Choose a character.", OwnershipRules.ValidateCopy(data, Copy(storage)));
    }

    [Fact]
    public void CharacterOnAnotherServer_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Grimorde is on Cormyr, not Thrane.", OwnershipRules.ValidateCopy(data, Copy(StorageType.Bank, "Thrane", grim.Id)));
    }

    [Fact]
    public void UnknownCharacter_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("That character no longer exists.", OwnershipRules.ValidateCopy(data, Copy(StorageType.Bank, characterId: "missing")));
    }

    [Fact]
    public void UnknownServer_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("Choose a server.", OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank, "Lamannia")));
    }

    [Fact]
    public void MissingItem_IsRejected()
    {
        var (data, _) = Setup();
        var copy = Copy(StorageType.SharedBank);
        copy.ItemKey = " ";
        Assert.Equal("Choose an item.", OwnershipRules.ValidateCopy(data, copy));
    }

    [Fact]
    public void UndefinedStorageValue_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("Choose where it's stored.", OwnershipRules.ValidateCopy(data, Copy((StorageType)99)));
    }
}
```

`tests/DdoItemTracker.Core.Tests/Ownership/TrackerOperationsTests.cs`:
```csharp
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Ownership;

public class TrackerOperationsTests
{
    [Fact]
    public void Servers_AreTheFourLiveWorlds()
    {
        Assert.Equal(["Cormyr", "Moonsea", "Shadowdale", "Thrane"], Servers.All);
        Assert.Null(Servers.Canonical("Lamannia"));
        Assert.Equal("Thrane", Servers.Canonical("  thrane "));
    }

    [Fact]
    public void AddCharacter_TrimsNameAndCanonicalisesServer()
    {
        var data = new TrackerData();
        var c = TrackerOperations.AddCharacter(data, "cormyr", "  Grimorde ");
        Assert.Equal("Cormyr", c.Server);
        Assert.Equal("Grimorde", c.Name);
        Assert.Single(data.Characters);
    }

    [Fact]
    public void AddCharacter_SameNameDifferingOnlyByCaseOrSpaces_IsRejected()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var ex = Assert.Throws<TrackerRuleException>(() => TrackerOperations.AddCharacter(data, "Cormyr", "grimorde "));
        Assert.Equal("There is already a character called grimorde on Cormyr.", ex.Message);
    }

    [Fact]
    public void AddCharacter_SameNameOnAnotherServer_IsAllowed()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        TrackerOperations.AddCharacter(data, "Thrane", "Grimorde");
        Assert.Equal(2, data.Characters.Count);
    }

    [Theory]
    [InlineData("Lamannia", "Grimorde", "Choose a server.")]
    [InlineData("Cormyr", "   ", "Enter a character name.")]
    public void AddCharacter_InvalidInput_IsRejected(string server, string name, string message)
    {
        var ex = Assert.Throws<TrackerRuleException>(() => TrackerOperations.AddCharacter(new TrackerData(), server, name));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void RenameCharacter_ToAnotherCharactersName_IsRejected()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var alt = TrackerOperations.AddCharacter(data, "Cormyr", "Alt");
        Assert.Throws<TrackerRuleException>(() => TrackerOperations.RenameCharacter(data, alt.Id, "GRIMORDE"));
        TrackerOperations.RenameCharacter(data, alt.Id, "Alt Two");
        Assert.Equal("Alt Two", alt.Name);
    }

    [Fact]
    public void DeleteCharacter_DeleteCopies_RemovesOnlyThatCharactersCopies()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "B|1|Ring", ItemName = "B", Server = "Cormyr", Storage = StorageType.SharedBank });

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.DeleteCopies);

        Assert.Empty(data.Characters);
        Assert.Equal("B|1|Ring", Assert.Single(data.OwnedCopies).ItemKey);
    }

    [Fact]
    public void DeleteCharacter_MoveToSharedBank_KeepsCopiesOnSameServer()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Thrane", "Grimorde");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Thrane", Storage = StorageType.Equipped, CharacterId = grim.Id });

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.MoveToSharedBank);

        var copy = Assert.Single(data.OwnedCopies);
        Assert.Null(copy.CharacterId);
        Assert.Equal(StorageType.SharedBank, copy.Storage);
        Assert.Equal("Thrane", copy.Server);
        Assert.Null(OwnershipRules.ValidateCopy(data, copy));
    }

    [Fact]
    public void DeleteFolder_UnfilesItsCharacters()
    {
        var data = new TrackerData();
        var folder = TrackerOperations.AddFolder(data, "Mains");
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde", folder.Id);

        TrackerOperations.DeleteFolder(data, folder.Id);

        Assert.Empty(data.Folders);
        Assert.Null(grim.FolderId);
    }

    [Fact]
    public void MoveCharacterToFolder_UnknownFolder_IsRejected()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        Assert.Throws<TrackerRuleException>(() => TrackerOperations.MoveCharacterToFolder(data, grim.Id, "nope"));
        var folder = TrackerOperations.AddFolder(data, "Mains");
        TrackerOperations.MoveCharacterToFolder(data, grim.Id, folder.Id);
        Assert.Equal(folder.Id, grim.FolderId);
    }

    [Fact]
    public void AddCopy_SeveralCopiesOfOneItem_AreAllKept()
    {
        var data = new TrackerData();
        for (var i = 0; i < 2; i++)
            TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });
        Assert.Equal(2, data.OwnedCopies.Count);
    }

    [Fact]
    public void AddCopy_NormalisesServerAndNoteAndStampsTime()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "cormyr", Storage = StorageType.SharedBank, Note = "   " });
        Assert.Equal("Cormyr", copy.Server);
        Assert.Null(copy.Note);
        Assert.NotEqual(default, copy.AddedUtc);
    }

    [Fact]
    public void AddCopy_Invalid_IsRejectedAndNotStored()
    {
        var data = new TrackerData();
        var ex = Assert.Throws<TrackerRuleException>(() =>
            TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank }));
        Assert.Equal("Choose a character.", ex.Message);
        Assert.Empty(data.OwnedCopies);
    }

    [Fact]
    public void UpdateCopy_ReplacesById_AndRejectedEditLeavesOriginal()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var original = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });

        Assert.Throws<TrackerRuleException>(() => TrackerOperations.UpdateCopy(data, new OwnedCopy { Id = original.Id, ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank }));
        Assert.Same(original, Assert.Single(data.OwnedCopies));

        TrackerOperations.UpdateCopy(data, new OwnedCopy { Id = original.Id, ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id, AddedUtc = original.AddedUtc });
        Assert.Equal(StorageType.Bank, Assert.Single(data.OwnedCopies).Storage);
    }

    [Fact]
    public void RemoveCopy_RemovesById()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });
        TrackerOperations.RemoveCopy(data, copy.Id);
        Assert.Empty(data.OwnedCopies);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The type or namespace name 'Ownership' does not exist in the namespace 'DdoItemTracker.Core'`.

- [ ] **Step 3: Write the implementation**

`src/DdoItemTracker.Core/Ownership/Servers.cs`:
```csharp
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
```

`src/DdoItemTracker.Core/Ownership/OwnershipModels.cs`:
```csharp
namespace DdoItemTracker.Core.Ownership;

public enum StorageType
{
    Equipped,
    Inventory,
    Bank,
    SharedBank,
}

public sealed class Character
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Server { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FolderId { get; set; }
    public string? LifeTrackerId { get; set; }
}

public sealed class Folder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}

public sealed class OwnedCopy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ItemKey { get; set; } = string.Empty;
    /// <summary>Name at the time the copy was recorded, shown if the item leaves the catalog.</summary>
    public string ItemName { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public StorageType Storage { get; set; }
    public string? CharacterId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset AddedUtc { get; set; }
}

public sealed class TrackerData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Character> Characters { get; set; } = [];
    public List<Folder> Folders { get; set; } = [];
    public List<OwnedCopy> OwnedCopies { get; set; } = [];
}
```

`src/DdoItemTracker.Core/Ownership/TrackerRuleException.cs`:
```csharp
namespace DdoItemTracker.Core.Ownership;

/// <summary>A broken ownership rule. The message is written for the player.</summary>
public sealed class TrackerRuleException(string message) : Exception(message);
```

`src/DdoItemTracker.Core/Ownership/OwnershipRules.cs`:
```csharp
namespace DdoItemTracker.Core.Ownership;

public static class OwnershipRules
{
    /// <summary>Returns null when the copy is valid, otherwise a message for the player.</summary>
    public static string? ValidateCopy(TrackerData data, OwnedCopy copy)
    {
        if (string.IsNullOrWhiteSpace(copy.ItemKey)) return "Choose an item.";
        var server = Servers.Canonical(copy.Server);
        if (server is null) return "Choose a server.";
        if (!Enum.IsDefined(copy.Storage)) return "Choose where it's stored.";

        if (copy.Storage == StorageType.SharedBank)
            return copy.CharacterId is null ? null : "Shared Bank items don't belong to a character.";

        if (copy.CharacterId is null) return "Choose a character.";
        var character = data.Characters.FirstOrDefault(c => c.Id == copy.CharacterId);
        if (character is null) return "That character no longer exists.";
        if (character.Server != server) return $"{character.Name} is on {character.Server}, not {server}.";
        return null;
    }
}
```

`src/DdoItemTracker.Core/Ownership/TrackerOperations.cs`:
```csharp
namespace DdoItemTracker.Core.Ownership;

public enum CharacterCopyDisposal
{
    DeleteCopies,
    MoveToSharedBank,
}

/// <summary>Every change to <see cref="TrackerData"/> goes through here so the rules hold.</summary>
public static class TrackerOperations
{
    public static bool IsSameName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Character AddCharacter(TrackerData data, string server, string name, string? folderId = null)
    {
        var canonical = Servers.Canonical(server) ?? throw new TrackerRuleException("Choose a server.");
        var trimmed = RequireName(name, "Enter a character name.");
        EnsureNameFree(data, canonical, trimmed, exceptId: null);
        if (folderId is not null) FindFolder(data, folderId);
        var character = new Character { Server = canonical, Name = trimmed, FolderId = folderId };
        data.Characters.Add(character);
        return character;
    }

    public static void RenameCharacter(TrackerData data, string characterId, string newName)
    {
        var character = FindCharacter(data, characterId);
        var trimmed = RequireName(newName, "Enter a character name.");
        EnsureNameFree(data, character.Server, trimmed, character.Id);
        character.Name = trimmed;
    }

    public static void MoveCharacterToFolder(TrackerData data, string characterId, string? folderId)
    {
        var character = FindCharacter(data, characterId);
        if (folderId is not null) FindFolder(data, folderId);
        character.FolderId = folderId;
    }

    public static void DeleteCharacter(TrackerData data, string characterId, CharacterCopyDisposal disposal)
    {
        var character = FindCharacter(data, characterId);
        if (disposal == CharacterCopyDisposal.DeleteCopies)
        {
            data.OwnedCopies.RemoveAll(c => c.CharacterId == characterId);
        }
        else
        {
            foreach (var copy in data.OwnedCopies.Where(c => c.CharacterId == characterId))
            {
                copy.CharacterId = null;
                copy.Storage = StorageType.SharedBank;
                copy.Server = character.Server;
            }
        }
        data.Characters.Remove(character);
    }

    public static Folder AddFolder(TrackerData data, string name)
    {
        var trimmed = RequireName(name, "Enter a folder name.");
        if (data.Folders.Any(f => IsSameName(f.Name, trimmed)))
            throw new TrackerRuleException($"There is already a folder called {trimmed}.");
        var folder = new Folder { Name = trimmed };
        data.Folders.Add(folder);
        return folder;
    }

    public static void DeleteFolder(TrackerData data, string folderId)
    {
        foreach (var c in data.Characters.Where(c => c.FolderId == folderId)) c.FolderId = null;
        data.Folders.RemoveAll(f => f.Id == folderId);
    }

    public static OwnedCopy AddCopy(TrackerData data, OwnedCopy copy)
    {
        Normalise(copy);
        if (OwnershipRules.ValidateCopy(data, copy) is { } error) throw new TrackerRuleException(error);
        if (data.OwnedCopies.Any(c => c.Id == copy.Id)) throw new TrackerRuleException("This copy is already recorded.");
        if (copy.AddedUtc == default) copy.AddedUtc = DateTimeOffset.UtcNow;
        data.OwnedCopies.Add(copy);
        return copy;
    }

    /// <summary>Replaces the stored copy with the same Id. Pass a new instance so a rejected edit changes nothing.</summary>
    public static void UpdateCopy(TrackerData data, OwnedCopy copy)
    {
        var index = data.OwnedCopies.FindIndex(c => c.Id == copy.Id);
        if (index < 0) throw new TrackerRuleException("That copy no longer exists.");
        Normalise(copy);
        if (OwnershipRules.ValidateCopy(data, copy) is { } error) throw new TrackerRuleException(error);
        if (copy.AddedUtc == default) copy.AddedUtc = data.OwnedCopies[index].AddedUtc;
        data.OwnedCopies[index] = copy;
    }

    public static void RemoveCopy(TrackerData data, string copyId) =>
        data.OwnedCopies.RemoveAll(c => c.Id == copyId);

    private static void Normalise(OwnedCopy copy)
    {
        copy.Server = Servers.Canonical(copy.Server) ?? copy.Server;
        copy.Note = string.IsNullOrWhiteSpace(copy.Note) ? null : copy.Note.Trim();
    }

    private static string RequireName(string? name, string message)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length == 0 ? throw new TrackerRuleException(message) : trimmed;
    }

    private static void EnsureNameFree(TrackerData data, string server, string name, string? exceptId)
    {
        if (data.Characters.Any(c => c.Id != exceptId && c.Server == server && IsSameName(c.Name, name)))
            throw new TrackerRuleException($"There is already a character called {name} on {server}.");
    }

    private static Character FindCharacter(TrackerData data, string id) =>
        data.Characters.FirstOrDefault(c => c.Id == id) ?? throw new TrackerRuleException("That character no longer exists.");

    private static Folder FindFolder(TrackerData data, string id) =>
        data.Folders.FirstOrDefault(f => f.Id == id) ?? throw new TrackerRuleException("That folder no longer exists.");
}
```

Note: the duplicate-name message uses the name as the player typed it (trimmed), so `"grimorde "` produces `There is already a character called grimorde on Cormyr.`

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add ownership model, rules and operations" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Filtering

**Files:**
- Create: `src/DdoItemTracker.Core/Filtering/ItemQuery.cs`, `src/DdoItemTracker.Core/Filtering/CopyQuery.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Filtering/ItemQueryTests.cs`, `tests/DdoItemTracker.Core.Tests/Filtering/CopyQueryTests.cs`

**Interfaces:**
- Consumes: `CatalogItem`, `CatalogIndex`, `TrackerData`, `OwnedCopy`, `StorageType`, `Servers`
- Produces:
  - `enum OwnershipFilter { All, Owned, NotOwned }`
  - `record ItemFilter { Search?, MinLevelFrom?, MinLevelTo?, Slot?, Type?, Pack?, Quest?, InSetOnly, ArtifactOnly, Ownership = All, OwnershipServer? }` with `bool HasItemFieldFilters`
  - `ItemQuery.Apply(IEnumerable<CatalogItem>, ItemFilter, TrackerData) : IReadOnlyList<CatalogItem>`
  - `ItemQuery.MatchesItem(CatalogItem, ItemFilter) : bool` (ignores ownership)
  - `ItemQuery.OwnedCounts(TrackerData, string? server = null) : IReadOnlyDictionary<string, int>`
  - `record CopyFilter { ItemFilter Item, Server?, CharacterId?, StorageType? Storage, NotInCatalogOnly }`
  - `record OwnedCopyRow(OwnedCopy Copy, CatalogItem? Item, string ItemName, string HolderName)` with `IsInCatalog`
  - `CopyQuery.SharedBankHolder = "Shared Bank"`, `CopyQuery.Apply(TrackerData, CatalogIndex, CopyFilter) : IReadOnlyList<OwnedCopyRow>` sorted Server, Shared Bank first, holder, storage, item name
  - `CopyQuery.Summary(TrackerData, CatalogIndex) : (int Owned, int Total)`

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Filtering/ItemQueryTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Filtering;

public class ItemQueryTests
{
    private static readonly IReadOnlyList<CatalogItem> Items = Fixtures.ConvertSample().Catalog.Items;

    private static IReadOnlyList<string> Keys(ItemFilter f, TrackerData? data = null) =>
        ItemQuery.Apply(Items, f, data ?? new TrackerData()).Select(i => i.Key).ToList();

    private static TrackerData OwnChainsBeltOn(string server)
    {
        var data = new TrackerData();
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = server, Storage = StorageType.SharedBank });
        return data;
    }

    [Fact]
    public void EmptyFilter_ReturnsEverything() => Assert.Equal(Items.Count, Keys(new ItemFilter()).Count);

    [Fact]
    public void Search_IgnoresCaseAndSurroundingSpaces() =>
        Assert.Equal(["Absorption Gauntlet|18|Gloves"], Keys(new ItemFilter { Search = "  absorption " }));

    [Fact]
    public void LevelRange_IsInclusive() =>
        Assert.Equal(
            ["Adherent's Pendant|11|Necklace", "Chains|8|Belt", "Chains|8|Necklace", "Cloak of Winter's End (level 8)|8|Cloak"],
            Keys(new ItemFilter { MinLevelFrom = 8, MinLevelTo = 11 }));

    [Fact]
    public void Slot_Type_Pack_Quest_Filter()
    {
        Assert.Equal(2, Keys(new ItemFilter { Slot = "necklace" }).Count);
        Assert.Equal(["Aeon, the Blazing Reign|33|Weapon"], Keys(new ItemFilter { Type = "Long Bows" }));
        Assert.Equal(2, Keys(new ItemFilter { Pack = "Against the Slave Lords" }).Count);
        Assert.Equal(2, Keys(new ItemFilter { Quest = "Timeline Fragment Exchange" }).Count);
    }

    [Fact]
    public void InSetOnly_KeepsSetItems() =>
        Assert.Equal(["Absorption Gauntlet|18|Gloves", "Adherent's Pendant|11|Necklace"], Keys(new ItemFilter { InSetOnly = true }));

    [Fact]
    public void ArtifactOnly_KeepsArtifacts()
    {
        var items = new[] { TestCatalogs.Item("Art", artifact: true), TestCatalogs.Item("Plain") };
        Assert.Equal(["Art"], ItemQuery.Apply(items, new ItemFilter { ArtifactOnly = true }, new TrackerData()).Select(i => i.Name));
    }

    [Fact]
    public void Ownership_OwnedAndNotOwned()
    {
        var data = OwnChainsBeltOn("Cormyr");
        Assert.Equal(["Chains|8|Belt"], Keys(new ItemFilter { Ownership = OwnershipFilter.Owned }, data));
        Assert.DoesNotContain("Chains|8|Belt", Keys(new ItemFilter { Ownership = OwnershipFilter.NotOwned }, data));
        Assert.Equal(Items.Count - 1, Keys(new ItemFilter { Ownership = OwnershipFilter.NotOwned }, data).Count);
    }

    [Fact]
    public void Ownership_ScopedToServer()
    {
        var data = OwnChainsBeltOn("Cormyr");
        Assert.Empty(Keys(new ItemFilter { Ownership = OwnershipFilter.Owned, OwnershipServer = "Thrane" }, data));
        Assert.Single(Keys(new ItemFilter { Ownership = OwnershipFilter.Owned, OwnershipServer = "cormyr" }, data));
    }

    [Fact]
    public void Filters_Combine() =>
        Assert.Equal(["Chains|8|Necklace"], Keys(new ItemFilter { Search = "chains", Slot = "Necklace" }));

    [Fact]
    public void OwnedCounts_CountsCopiesPerKey()
    {
        var data = OwnChainsBeltOn("Cormyr");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Thrane", Storage = StorageType.SharedBank });
        Assert.Equal(2, ItemQuery.OwnedCounts(data)["Chains|8|Belt"]);
        Assert.Equal(1, ItemQuery.OwnedCounts(data, "Thrane")["Chains|8|Belt"]);
    }
}
```

`tests/DdoItemTracker.Core.Tests/Filtering/CopyQueryTests.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Filtering;

public class CopyQueryTests
{
    private static readonly CatalogIndex Index = new(Fixtures.ConvertSample().Catalog);

    private static (TrackerData Data, Character Grim, Character Alt) Setup()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var alt = TrackerOperations.AddCharacter(data, "Thrane", "Alt");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Absorption Gauntlet|18|Gloves", ItemName = "Absorption Gauntlet", Server = "Cormyr", Storage = StorageType.SharedBank });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Thrane", Storage = StorageType.Equipped, CharacterId = alt.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Retired Ring|5|Ring", ItemName = "Retired Ring", Server = "Thrane", Storage = StorageType.SharedBank });
        return (data, grim, alt);
    }

    [Fact]
    public void Apply_SortsByServerThenSharedBankThenHolder()
    {
        var (data, _, _) = Setup();
        var rows = CopyQuery.Apply(data, Index, new CopyFilter());
        Assert.Equal(
            ["Cormyr/Shared Bank/Absorption Gauntlet", "Cormyr/Grimorde/Chains", "Thrane/Shared Bank/Retired Ring", "Thrane/Alt/Chains"],
            rows.Select(r => $"{r.Copy.Server}/{r.HolderName}/{r.ItemName}"));
    }

    [Fact]
    public void Apply_FiltersByServerCharacterAndStorage()
    {
        var (data, grim, _) = Setup();
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Server = "Thrane" }).Count);
        Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { CharacterId = grim.Id }));
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Storage = StorageType.SharedBank }).Count);
    }

    [Fact]
    public void CopyWhoseItemLeftTheCatalog_IsShownByItsSavedName()
    {
        var (data, _, _) = Setup();
        var row = Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { NotInCatalogOnly = true }));
        Assert.False(row.IsInCatalog);
        Assert.Equal("Retired Ring", row.ItemName);
    }

    [Fact]
    public void CopyNotInCatalog_MatchesSearchButNotItemFieldFilters()
    {
        var (data, _, _) = Setup();
        Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Search = "retired" } }));
        Assert.DoesNotContain(
            CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Slot = "Ring" } }),
            r => !r.IsInCatalog);
    }

    [Fact]
    public void ItemFilters_ApplyToCatalogCopies()
    {
        var (data, _, _) = Setup();
        var rows = CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Slot = "Belt" } });
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("Chains", r.ItemName));
    }

    [Fact]
    public void Summary_CountsDistinctCatalogItemsOwned()
    {
        var (data, _, _) = Setup();
        Assert.Equal((2, Index.Catalog.Items.Count), CopyQuery.Summary(data, Index));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The type or namespace name 'Filtering' does not exist in the namespace 'DdoItemTracker.Core'`.

- [ ] **Step 3: Write the implementation**

`src/DdoItemTracker.Core/Filtering/ItemQuery.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Filtering;

public enum OwnershipFilter
{
    All,
    Owned,
    NotOwned,
}

public sealed record ItemFilter
{
    public string? Search { get; init; }
    public int? MinLevelFrom { get; init; }
    public int? MinLevelTo { get; init; }
    public string? Slot { get; init; }
    public string? Type { get; init; }
    public string? Pack { get; init; }
    public string? Quest { get; init; }
    public bool InSetOnly { get; init; }
    public bool ArtifactOnly { get; init; }
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;
    /// <summary>When set, Owned / Not owned look only at copies on this server.</summary>
    public string? OwnershipServer { get; init; }

    /// <summary>True when a filter other than the search box needs catalog fields.</summary>
    public bool HasItemFieldFilters =>
        MinLevelFrom is not null || MinLevelTo is not null || Slot is not null || Type is not null
        || Pack is not null || Quest is not null || InSetOnly || ArtifactOnly;
}

public static class ItemQuery
{
    public static IReadOnlyList<CatalogItem> Apply(IEnumerable<CatalogItem> items, ItemFilter filter, TrackerData data)
    {
        var owned = filter.Ownership == OwnershipFilter.All
            ? null
            : OwnedCounts(data, filter.OwnershipServer);
        return items.Where(i => MatchesItem(i, filter) && MatchesOwnership(i, filter.Ownership, owned)).ToList();
    }

    public static bool MatchesItem(CatalogItem item, ItemFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search)
            && !item.Name.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (filter.MinLevelFrom is int lo && item.MinLevel < lo) return false;
        if (filter.MinLevelTo is int hi && item.MinLevel > hi) return false;
        if (filter.Slot is not null && !Same(item.Slot, filter.Slot)) return false;
        if (filter.Type is not null && !Same(item.Type, filter.Type)) return false;
        if (filter.Pack is not null && !Same(item.Pack, filter.Pack)) return false;
        if (filter.Quest is not null && !item.Quests.Any(q => Same(q, filter.Quest))) return false;
        if (filter.InSetOnly && item.SetNames.Count == 0) return false;
        if (filter.ArtifactOnly && !item.IsArtifact) return false;
        return true;
    }

    public static IReadOnlyDictionary<string, int> OwnedCounts(TrackerData data, string? server = null)
    {
        var canonical = server is null ? null : Servers.Canonical(server) ?? server;
        return data.OwnedCopies
            .Where(c => canonical is null || c.Server == canonical)
            .GroupBy(c => c.ItemKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    private static bool MatchesOwnership(CatalogItem item, OwnershipFilter ownership, IReadOnlyDictionary<string, int>? owned) =>
        ownership switch
        {
            OwnershipFilter.Owned => owned!.ContainsKey(item.Key),
            OwnershipFilter.NotOwned => !owned!.ContainsKey(item.Key),
            _ => true,
        };

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
```

`src/DdoItemTracker.Core/Filtering/CopyQuery.cs`:
```csharp
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Filtering;

public sealed record CopyFilter
{
    public ItemFilter Item { get; init; } = new();
    public string? Server { get; init; }
    public string? CharacterId { get; init; }
    public StorageType? Storage { get; init; }
    public bool NotInCatalogOnly { get; init; }
}

public sealed record OwnedCopyRow(OwnedCopy Copy, CatalogItem? Item, string ItemName, string HolderName)
{
    public bool IsInCatalog => Item is not null;
}

/// <summary>The My Items view: owned copies with their catalog item and holder.</summary>
public static class CopyQuery
{
    public const string SharedBankHolder = "Shared Bank";

    public static IReadOnlyList<OwnedCopyRow> Apply(TrackerData data, CatalogIndex index, CopyFilter filter)
    {
        var names = data.Characters.ToDictionary(c => c.Id, c => c.Name);
        var server = filter.Server is null ? null : Servers.Canonical(filter.Server) ?? filter.Server;
        var rows = new List<OwnedCopyRow>();

        foreach (var copy in data.OwnedCopies)
        {
            if (server is not null && copy.Server != server) continue;
            if (filter.CharacterId is not null && copy.CharacterId != filter.CharacterId) continue;
            if (filter.Storage is { } storage && copy.Storage != storage) continue;

            var item = index.Find(copy.ItemKey);
            if (filter.NotInCatalogOnly && item is not null) continue;
            if (item is not null ? !ItemQuery.MatchesItem(item, filter.Item) : !MatchesMissingItem(copy, filter.Item)) continue;

            var holder = copy.CharacterId is null
                ? SharedBankHolder
                : names.GetValueOrDefault(copy.CharacterId, "(unknown character)");
            rows.Add(new OwnedCopyRow(copy, item, item?.Name ?? copy.ItemName, holder));
        }

        return rows
            .OrderBy(r => r.Copy.Server, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Copy.CharacterId is null ? 0 : 1)
            .ThenBy(r => r.HolderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Copy.Storage)
            .ThenBy(r => r.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static (int Owned, int Total) Summary(TrackerData data, CatalogIndex index)
    {
        var owned = data.OwnedCopies
            .Select(c => c.ItemKey)
            .Where(k => index.Find(k) is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return (owned, index.Catalog.Items.Count);
    }

    // Only the search box can apply to an item the catalog no longer has; any other item filter excludes it.
    private static bool MatchesMissingItem(OwnedCopy copy, ItemFilter filter) =>
        !filter.HasItemFieldFilters
        && (string.IsNullOrWhiteSpace(filter.Search)
            || copy.ItemName.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add catalog and owned-copy filtering" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Tracker persistence

**Files:**
- Create: `src/DdoItemTracker.Core/Persistence/TrackerJson.cs`, `src/DdoItemTracker.Core/Persistence/TrackerStore.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Persistence/TrackerStoreTests.cs`

**Interfaces:**
- Consumes: `TrackerData`, `TrackerOperations`
- Produces:
  - `TrackerJson.Options : JsonSerializerOptions` (indented, omit nulls, case-insensitive, enums as strings)
  - `enum LoadOutcome { NewFile, Loaded, RecoveredFromBackup, BothUnreadable }`
  - `record LoadResult(TrackerData Data, LoadOutcome Outcome, string? PreservedCopyPath)`
  - `class TrackerStore(string directoryPath, TimeProvider? clock = null)` with `FilePath`, `BackupPath`, `Load() : LoadResult`, `Save(TrackerData)`

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Persistence/TrackerStoreTests.cs`:
```csharp
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Tests.Persistence;

public sealed class TrackerStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ddo-item-tracker-tests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static TrackerData Sample(string name = "Grimorde")
    {
        var data = new TrackerData();
        var c = TrackerOperations.AddCharacter(data, "Cormyr", name);
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = c.Id });
        return data;
    }

    [Fact]
    public void Load_WithNoFiles_ReturnsEmptyNewFile()
    {
        var result = new TrackerStore(_dir).Load();
        Assert.Equal(LoadOutcome.NewFile, result.Outcome);
        Assert.Empty(result.Data.Characters);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample());

        var result = store.Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("Grimorde", Assert.Single(result.Data.Characters).Name);
        Assert.Equal(StorageType.Bank, Assert.Single(result.Data.OwnedCopies).Storage);
        Assert.Contains("\"Bank\"", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Save_KeepsPreviousVersionAsBak()
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample("First"));
        store.Save(Sample("Second"));

        Assert.Contains("First", File.ReadAllText(store.BackupPath));
        Assert.Contains("Second", File.ReadAllText(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void Load_UnreadableMainFile_RecoversFromBakAndPreservesBadFile(string badContent)
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample("First"));
        store.Save(Sample("Second"));
        File.WriteAllText(store.FilePath, badContent);

        var result = store.Load();

        Assert.Equal(LoadOutcome.RecoveredFromBackup, result.Outcome);
        Assert.Equal("First", Assert.Single(result.Data.Characters).Name);
        Assert.NotNull(result.PreservedCopyPath);
        Assert.Equal(badContent, File.ReadAllText(result.PreservedCopyPath!));
    }

    [Fact]
    public void Load_BothUnreadable_StartsEmptyAndPreservesBoth()
    {
        var store = new TrackerStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "bad main");
        File.WriteAllText(store.BackupPath, "bad backup");

        var result = store.Load();

        Assert.Equal(LoadOutcome.BothUnreadable, result.Outcome);
        Assert.Empty(result.Data.Characters);
        var preserved = Directory.GetFiles(_dir, "*.unreadable-*").Select(File.ReadAllText).Order().ToList();
        Assert.Equal(["bad backup", "bad main"], preserved);
    }

    [Fact]
    public void Load_NullListsInFile_BecomeEmptyLists()
    {
        var store = new TrackerStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, """{"SchemaVersion":1,"Characters":null}""");

        var result = store.Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Empty(result.Data.Characters);
        Assert.Empty(result.Data.Folders);
        Assert.Empty(result.Data.OwnedCopies);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The type or namespace name 'Persistence' does not exist in the namespace 'DdoItemTracker.Core'`.

- [ ] **Step 3: Write the implementation**

`src/DdoItemTracker.Core/Persistence/TrackerJson.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdoItemTracker.Core.Persistence;

public static class TrackerJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
```

`src/DdoItemTracker.Core/Persistence/TrackerStore.cs`:
```csharp
using System.Globalization;
using System.Text.Json;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Persistence;

public enum LoadOutcome
{
    NewFile,
    Loaded,
    RecoveredFromBackup,
    BothUnreadable,
}

public sealed record LoadResult(TrackerData Data, LoadOutcome Outcome, string? PreservedCopyPath);

/// <summary>Saves the player's data atomically and keeps the previous version as a .bak.</summary>
public sealed class TrackerStore(string directoryPath, TimeProvider? clock = null)
{
    public const string FileName = "tracker.json";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string DirectoryPath { get; } = directoryPath;
    public string FilePath => Path.Combine(DirectoryPath, FileName);
    public string BackupPath => FilePath + ".bak";
    private string TempPath => FilePath + ".tmp";

    public LoadResult Load()
    {
        var mainExists = File.Exists(FilePath);
        var backupExists = File.Exists(BackupPath);
        if (!mainExists && !backupExists) return new LoadResult(new TrackerData(), LoadOutcome.NewFile, null);

        if (mainExists && TryRead(FilePath) is { } data) return new LoadResult(data, LoadOutcome.Loaded, null);

        var preserved = mainExists ? Preserve(FilePath) : null;
        if (backupExists && TryRead(BackupPath) is { } recovered)
            return new LoadResult(recovered, LoadOutcome.RecoveredFromBackup, preserved);

        if (backupExists) Preserve(BackupPath);
        return new LoadResult(new TrackerData(), LoadOutcome.BothUnreadable, preserved);
    }

    public void Save(TrackerData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(TempPath, JsonSerializer.Serialize(data, TrackerJson.Options));
        if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, BackupPath);
        else File.Move(TempPath, FilePath);
    }

    private static TrackerData? TryRead(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonSerializer.Deserialize<TrackerData>(json, TrackerJson.Options);
            if (data is null) return null;
            data.Characters ??= [];
            data.Folders ??= [];
            data.OwnedCopies ??= [];
            return data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string Preserve(string path)
    {
        var stamp = _clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var destination = $"{path}.unreadable-{stamp}";
        File.Copy(path, destination, overwrite: true);
        return destination;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add tracker persistence with backup recovery" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Own backup export and import

**Files:**
- Create: `src/DdoItemTracker.Core/Import/TrackerBackupService.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Import/TrackerBackupServiceTests.cs`

**Interfaces:**
- Consumes: `TrackerData`, `Character`, `Folder`, `OwnedCopy`, `Servers`, `OwnershipRules`, `TrackerOperations.IsSameName`, `TrackerJson.Options`
- Produces:
  - `class TrackerBackup { SchemaVersion, ExportedUtc, Characters, Folders, OwnedCopies? }`, `TrackerBackup.CurrentSchemaVersion = 1`
  - `enum ImportMode { Merge, Replace }`
  - `record BackupImportResult(int Added, int Updated, int Skipped)`
  - `TrackerBackupService.Export(TrackerData, DateTimeOffset now) : string`
  - `TrackerBackupService.Parse(string json) : TrackerBackup` (throws `InvalidDataException`)
  - `TrackerBackupService.Import(TrackerData, TrackerBackup, ImportMode) : BackupImportResult`

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Import/TrackerBackupServiceTests.cs`:
```csharp
using System.Text.Json;
using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Tests.Import;

public class TrackerBackupServiceTests
{
    private static TrackerData Sample()
    {
        var data = new TrackerData();
        var folder = TrackerOperations.AddFolder(data, "Mains");
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde", folder.Id);
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Thrane", Storage = StorageType.SharedBank });
        return data;
    }

    private static string Snapshot(TrackerData data) => JsonSerializer.Serialize(data, TrackerJson.Options);

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExportThenReplaceIntoEmpty_ReproducesData()
    {
        var source = Sample();
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(source, Now));
        var target = new TrackerData();

        var result = TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.Equal(Snapshot(source), Snapshot(target));
        Assert.Equal(new BackupImportResult(3, 0, 0), result); // 1 character + 2 copies; folders aren't counted
        Assert.Equal(Now, backup.ExportedUtc);
    }

    [Fact]
    public void MergeSameBackupTwice_ChangesNothingTheSecondTime()
    {
        var json = TrackerBackupService.Export(Sample(), Now);
        var target = new TrackerData();
        TrackerBackupService.Import(target, TrackerBackupService.Parse(json), ImportMode.Merge);
        var afterFirst = Snapshot(target);

        TrackerBackupService.Import(target, TrackerBackupService.Parse(json), ImportMode.Merge);

        Assert.Equal(afterFirst, Snapshot(target));
    }

    [Fact]
    public void Merge_KeepsExistingRecordsNotInBackup_AndIncomingWins()
    {
        var target = Sample();
        var extra = TrackerOperations.AddCharacter(target, "Moonsea", "Keeper");
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(target, Now));
        backup.Characters.Single(c => c.Name == "Grimorde").Name = "Grimorde Renamed";
        backup.Characters.RemoveAll(c => c.Id == extra.Id);

        TrackerBackupService.Import(target, backup, ImportMode.Merge);

        Assert.Contains(target.Characters, c => c.Name == "Keeper");
        Assert.Contains(target.Characters, c => c.Name == "Grimorde Renamed");
    }

    [Fact]
    public void Replace_RemovesRecordsNotInBackup()
    {
        var target = Sample();
        TrackerOperations.AddCharacter(target, "Moonsea", "Keeper");
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));

        TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.DoesNotContain(target.Characters, c => c.Name == "Keeper");
    }

    [Fact]
    public void InvalidRecordsInBackup_AreSkippedAndCounted()
    {
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));
        backup.Characters.Add(new Character { Server = "Lamannia", Name = "Tester" });
        backup.OwnedCopies!.Add(new OwnedCopy { ItemKey = "B|1|Ring", ItemName = "B", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = "missing" });
        var target = new TrackerData();

        var result = TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.Equal(2, result.Skipped);
        Assert.DoesNotContain(target.Characters, c => c.Name == "Tester");
        Assert.DoesNotContain(target.OwnedCopies, c => c.ItemKey == "B|1|Ring");
    }

    [Fact]
    public void CharacterPointingAtMissingFolder_IsUnfiled()
    {
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));
        backup.Folders.Clear();
        var target = new TrackerData();

        TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.Null(Assert.Single(target.Characters).FolderId);
    }

    [Fact]
    public void Parse_LifeTrackerBackup_PointsToTheRightImport()
    {
        const string lifeTracker = """{"SchemaVersion":2,"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Grimorde"}],"Folders":[]}""";
        var ex = Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse(lifeTracker));
        Assert.Equal("This file isn't a DDO Item Tracker backup. To bring in characters from DDO Life Tracker, use Import from DDO Life Tracker on the Characters page.", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Parse_Garbage_Throws(string json)
    {
        Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse(json));
    }

    [Fact]
    public void Parse_NewerSchema_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse("""{"SchemaVersion":99,"OwnedCopies":[]}"""));
        Assert.Equal("This backup was made by a newer version of DDO Item Tracker. Update the app, then try again.", ex.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The type or namespace name 'Import' does not exist in the namespace 'DdoItemTracker.Core'`.

- [ ] **Step 3: Write the implementation**

`src/DdoItemTracker.Core/Import/TrackerBackupService.cs`:
```csharp
using System.Text.Json;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Import;

public sealed class TrackerBackup
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ExportedUtc { get; set; }
    public List<Character> Characters { get; set; } = [];
    public List<Folder> Folders { get; set; } = [];
    /// <summary>Null when the file isn't ours; a DDO Life Tracker backup has no OwnedCopies.</summary>
    public List<OwnedCopy>? OwnedCopies { get; set; }
}

public enum ImportMode
{
    Merge,
    Replace,
}

public sealed record BackupImportResult(int Added, int Updated, int Skipped);

public static class TrackerBackupService
{
    private const string NotOurs =
        "This file isn't a DDO Item Tracker backup. To bring in characters from DDO Life Tracker, use Import from DDO Life Tracker on the Characters page.";

    public static string Export(TrackerData data, DateTimeOffset now) =>
        JsonSerializer.Serialize(new TrackerBackup
        {
            ExportedUtc = now,
            Characters = data.Characters,
            Folders = data.Folders,
            OwnedCopies = data.OwnedCopies,
        }, TrackerJson.Options);

    public static TrackerBackup Parse(string json)
    {
        TrackerBackup? backup;
        try
        {
            backup = JsonSerializer.Deserialize<TrackerBackup>(json, TrackerJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(NotOurs, ex);
        }
        if (backup?.OwnedCopies is null) throw new InvalidDataException(NotOurs);
        if (backup.SchemaVersion > TrackerBackup.CurrentSchemaVersion)
            throw new InvalidDataException("This backup was made by a newer version of DDO Item Tracker. Update the app, then try again.");
        backup.Characters ??= [];
        backup.Folders ??= [];
        return backup;
    }

    public static BackupImportResult Import(TrackerData data, TrackerBackup backup, ImportMode mode)
    {
        if (mode == ImportMode.Replace)
        {
            data.Characters.Clear();
            data.Folders.Clear();
            data.OwnedCopies.Clear();
        }
        int added = 0, updated = 0, skipped = 0;

        foreach (var folder in backup.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Name)) { skipped++; continue; }
            Upsert(data.Folders, folder, f => f.Id);
        }

        foreach (var character in backup.Characters)
        {
            var server = Servers.Canonical(character.Server);
            if (server is null || string.IsNullOrWhiteSpace(character.Name)
                || data.Characters.Any(c => c.Id != character.Id && c.Server == server && TrackerOperations.IsSameName(c.Name, character.Name)))
            {
                skipped++;
                continue;
            }
            character.Server = server;
            character.Name = character.Name.Trim();
            if (character.FolderId is not null && data.Folders.All(f => f.Id != character.FolderId)) character.FolderId = null;
            if (Upsert(data.Characters, character, c => c.Id)) updated++; else added++;
        }

        foreach (var copy in backup.OwnedCopies ?? [])
        {
            copy.Server = Servers.Canonical(copy.Server) ?? copy.Server;
            if (OwnershipRules.ValidateCopy(data, copy) is not null) { skipped++; continue; }
            if (Upsert(data.OwnedCopies, copy, c => c.Id)) updated++; else added++;
        }

        return new BackupImportResult(added, updated, skipped);
    }

    /// <returns>True when an existing record was replaced, false when the record was added.</returns>
    private static bool Upsert<T>(List<T> list, T item, Func<T, string> id)
    {
        var index = list.FindIndex(x => id(x) == id(item));
        if (index < 0)
        {
            list.Add(item);
            return false;
        }
        list[index] = item;
        return true;
    }
}
```

Folders are not counted in `Added`/`Updated` (the counts describe characters and copies, which is what the player sees); an unnamed folder counts as skipped.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add backup export and import" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: DDO Life Tracker character import

**Files:**
- Create: `src/DdoItemTracker.Core/Import/LifeTrackerBackupReader.cs`, `src/DdoItemTracker.Core/Import/LifeTrackerImporter.cs`
- Test: `tests/DdoItemTracker.Core.Tests/Import/LifeTrackerImportTests.cs`

**Interfaces:**
- Consumes: `TrackerData`, `Character`, `Folder`, `Servers`, `TrackerOperations.IsSameName`
- Produces:
  - `record LifeTrackerCharacter(string Id, string Server, string Name, string? FolderId)`
  - `record LifeTrackerFolder(string Id, string Name)`
  - `record LifeTrackerBackup(IReadOnlyList<LifeTrackerCharacter> Characters, IReadOnlyList<LifeTrackerFolder> Folders)`
  - `LifeTrackerBackupReader.Parse(string json) : LifeTrackerBackup` (throws `InvalidDataException`; accepts the `BackupPayload` object or Life Tracker's legacy bare character array)
  - `enum LifeTrackerImportAction { Add, Update, SkipUnknownServer, SkipInvalid }`
  - `record LifeTrackerImportEntry(LifeTrackerCharacter Source, LifeTrackerImportAction Action, string? ExistingCharacterId, string? Reason)`
  - `record LifeTrackerImportPlan(IReadOnlyList<LifeTrackerImportEntry> Entries)` with `AddCount`, `UpdateCount`, `SkipCount`
  - `LifeTrackerImporter.Plan(TrackerData, LifeTrackerBackup) : LifeTrackerImportPlan` (no changes)
  - `LifeTrackerImporter.Apply(TrackerData, LifeTrackerBackup) : LifeTrackerImportPlan` (re-plans against current data, then applies)

Matching rules (spec 7.2): same server and `LifeTrackerId == source Id` first, then same server and same name (case and spaces ignored). An update sets the name and `LifeTrackerId`, and sets the folder only when the Life Tracker character has one. Folders are matched by name and created only if a character being added or updated uses them.

- [ ] **Step 1: Write the failing tests**

`tests/DdoItemTracker.Core.Tests/Import/LifeTrackerImportTests.cs`:
```csharp
using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Import;

public class LifeTrackerImportTests
{
    // Shape written by DDO Life Tracker 1.4.x (BackupPayload, schema 2, PascalCase, past lives included).
    private const string Backup = """
        {
          "SchemaVersion": 2,
          "ExportedUtc": "2026-09-01T10:00:00Z",
          "Characters": [
            { "Id": "a1", "Server": "Cormyr", "Name": "Grimorde", "FolderId": "f1", "HeroicPastLives": { "Fighter": 3 } },
            { "Id": "a2", "Server": "thrane ", "Name": "Alt Two" },
            { "Id": "a3", "Server": "Lamannia", "Name": "Tester" }
          ],
          "Folders": [ { "Id": "f1", "Name": "Mains" }, { "Id": "f2", "Name": "Unused" } ]
        }
        """;

    [Fact]
    public void Parse_ReadsCharactersAndFolders_IgnoringPastLives()
    {
        var backup = LifeTrackerBackupReader.Parse(Backup);
        Assert.Equal(3, backup.Characters.Count);
        Assert.Equal(new LifeTrackerCharacter("a1", "Cormyr", "Grimorde", "f1"), backup.Characters[0]);
        Assert.Equal(2, backup.Folders.Count);
    }

    [Fact]
    public void Parse_AcceptsLegacyBareCharacterList()
    {
        var backup = LifeTrackerBackupReader.Parse("""[{ "Id": "a1", "Server": "Cormyr", "Name": "Grimorde" }]""");
        Assert.Single(backup.Characters);
        Assert.Empty(backup.Folders);
    }

    [Fact]
    public void Parse_OurOwnBackup_PointsToRestore()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            LifeTrackerBackupReader.Parse("""{"SchemaVersion":1,"Characters":[],"Folders":[],"OwnedCopies":[]}"""));
        Assert.Equal("This is a DDO Item Tracker backup. Use Restore backup in Settings instead.", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"Characters":[{"Foo":1}]}""")]
    [InlineData("""{"items":[]}""")]
    public void Parse_NotALifeTrackerFile_Throws(string json)
    {
        var ex = Assert.Throws<InvalidDataException>(() => LifeTrackerBackupReader.Parse(json));
        Assert.Equal(LifeTrackerBackupReader.NotALifeTrackerBackup, ex.Message);
    }

    [Fact]
    public void Plan_IntoEmptyData_AddsSupportedServers_AndCanonicalisesServerCase()
    {
        var plan = LifeTrackerImporter.Plan(new TrackerData(), LifeTrackerBackupReader.Parse(Backup));

        Assert.Equal(2, plan.AddCount);
        Assert.Equal(0, plan.UpdateCount);
        Assert.Equal(1, plan.SkipCount);
        Assert.Equal(LifeTrackerImportAction.SkipUnknownServer, plan.Entries.Single(e => e.Source.Id == "a3").Action);
        Assert.Equal(LifeTrackerImportAction.Add, plan.Entries.Single(e => e.Source.Id == "a2").Action);
    }

    [Fact]
    public void Plan_DoesNotChangeData()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Plan(data, LifeTrackerBackupReader.Parse(Backup));
        Assert.Empty(data.Characters);
    }

    [Fact]
    public void Apply_AddsCharactersWithLifeTrackerIdAndUsedFoldersOnly()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        var grim = data.Characters.Single(c => c.Name == "Grimorde");
        Assert.Equal("a1", grim.LifeTrackerId);
        Assert.Equal("Mains", data.Folders.Single(f => f.Id == grim.FolderId).Name);
        Assert.Equal("Thrane", data.Characters.Single(c => c.Name == "Alt Two").Server);
        Assert.DoesNotContain(data.Folders, f => f.Name == "Unused");
    }

    [Fact]
    public void Apply_Twice_UpdatesInsteadOfDuplicating()
    {
        var data = new TrackerData();
        var backup = LifeTrackerBackupReader.Parse(Backup);
        LifeTrackerImporter.Apply(data, backup);

        var second = LifeTrackerImporter.Apply(data, backup);

        Assert.Equal(2, data.Characters.Count);
        Assert.Single(data.Folders);
        Assert.Equal(2, second.UpdateCount);
    }

    [Fact]
    public void ExistingCharacterWithSameName_IsMatchedAndKeepsItsId()
    {
        var data = new TrackerData();
        var existing = TrackerOperations.AddCharacter(data, "Cormyr", "grimorde");

        var plan = LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        Assert.Equal(existing.Id, plan.Entries.Single(e => e.Source.Id == "a1").ExistingCharacterId);
        Assert.Equal("Grimorde", existing.Name);
        Assert.Equal("a1", existing.LifeTrackerId);
    }

    [Fact]
    public void RenameInLifeTracker_IsFollowedByLifeTrackerId()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Grimorde Reborn"}],"Folders":[]}"""));

        Assert.Contains(data.Characters, c => c.Name == "Grimorde Reborn" && c.LifeTrackerId == "a1");
        Assert.DoesNotContain(data.Characters, c => c.Name == "Grimorde");
    }

    [Fact]
    public void RenameOntoAnotherCharactersName_IsSkipped()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));
        TrackerOperations.AddCharacter(data, "Cormyr", "Taken");

        var plan = LifeTrackerImporter.Plan(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Taken"}]}"""));

        Assert.Equal(LifeTrackerImportAction.SkipInvalid, Assert.Single(plan.Entries).Action);
    }

    [Fact]
    public void DuplicateInFile_AndBlankName_AreSkipped()
    {
        const string json = """
            {"Characters":[
              {"Id":"x1","Server":"Moonsea","Name":"Twin"},
              {"Id":"x2","Server":"Moonsea","Name":"twin "},
              {"Id":"x3","Server":"Moonsea","Name":"  "}]}
            """;
        var plan = LifeTrackerImporter.Plan(new TrackerData(), LifeTrackerBackupReader.Parse(json));
        Assert.Equal(1, plan.AddCount);
        Assert.Equal(2, plan.SkipCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test`
Expected: build FAILS with `The name 'LifeTrackerBackupReader' does not exist in the current context`.

- [ ] **Step 3: Write the reader**

`src/DdoItemTracker.Core/Import/LifeTrackerBackupReader.cs`:
```csharp
using System.Text.Json;

namespace DdoItemTracker.Core.Import;

public sealed record LifeTrackerCharacter(string Id, string Server, string Name, string? FolderId);

public sealed record LifeTrackerFolder(string Id, string Name);

public sealed record LifeTrackerBackup(IReadOnlyList<LifeTrackerCharacter> Characters, IReadOnlyList<LifeTrackerFolder> Folders);

/// <summary>Reads the characters and folders from a DDO Life Tracker backup. Past lives and tomes are ignored.</summary>
public static class LifeTrackerBackupReader
{
    public const string NotALifeTrackerBackup = "This file isn't a DDO Life Tracker backup.";

    public static LifeTrackerBackup Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(NotALifeTrackerBackup, ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            JsonElement characters;
            JsonElement? folders = null;

            if (root.ValueKind == JsonValueKind.Array)
            {
                characters = root; // Life Tracker's legacy format: a bare character list
            }
            else if (root.ValueKind == JsonValueKind.Object && TryGet(root, "Characters", out characters) && characters.ValueKind == JsonValueKind.Array)
            {
                if (TryGet(root, "OwnedCopies", out _))
                    throw new InvalidDataException("This is a DDO Item Tracker backup. Use Restore backup in Settings instead.");
                if (TryGet(root, "Folders", out var f) && f.ValueKind == JsonValueKind.Array) folders = f;
            }
            else
            {
                throw new InvalidDataException(NotALifeTrackerBackup);
            }

            var characterList = new List<LifeTrackerCharacter>();
            foreach (var c in characters.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object || !TryGet(c, "Server", out _) || !TryGet(c, "Name", out _))
                    throw new InvalidDataException(NotALifeTrackerBackup);
                characterList.Add(new LifeTrackerCharacter(
                    Str(c, "Id") ?? string.Empty,
                    Str(c, "Server") ?? string.Empty,
                    Str(c, "Name") ?? string.Empty,
                    NullIfBlank(Str(c, "FolderId"))));
            }

            var folderList = new List<LifeTrackerFolder>();
            if (folders is { } fs)
            {
                foreach (var f in fs.EnumerateArray())
                {
                    if (f.ValueKind != JsonValueKind.Object) continue;
                    var id = Str(f, "Id");
                    var name = Str(f, "Name");
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                        folderList.Add(new LifeTrackerFolder(id, name.Trim()));
                }
            }

            return new LifeTrackerBackup(characterList, folderList);
        }
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? Str(JsonElement obj, string name) =>
        TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
```

- [ ] **Step 4: Write the importer**

`src/DdoItemTracker.Core/Import/LifeTrackerImporter.cs`:
```csharp
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Import;

public enum LifeTrackerImportAction
{
    Add,
    Update,
    SkipUnknownServer,
    SkipInvalid,
}

public sealed record LifeTrackerImportEntry(
    LifeTrackerCharacter Source,
    LifeTrackerImportAction Action,
    string? ExistingCharacterId,
    string? Reason);

public sealed record LifeTrackerImportPlan(IReadOnlyList<LifeTrackerImportEntry> Entries)
{
    public int AddCount => Entries.Count(e => e.Action == LifeTrackerImportAction.Add);
    public int UpdateCount => Entries.Count(e => e.Action == LifeTrackerImportAction.Update);
    public int SkipCount => Entries.Count(e => e.Action is LifeTrackerImportAction.SkipUnknownServer or LifeTrackerImportAction.SkipInvalid);
}

public static class LifeTrackerImporter
{
    private const string DuplicateInFile = "Appears more than once in the file.";

    /// <summary>Works out what an import would do, without changing anything.</summary>
    public static LifeTrackerImportPlan Plan(TrackerData data, LifeTrackerBackup backup)
    {
        var entries = new List<LifeTrackerImportEntry>();
        var matchedIds = new HashSet<string>(StringComparer.Ordinal);
        var plannedNew = new List<(string Server, string Name)>();

        foreach (var source in backup.Characters)
        {
            var server = Servers.Canonical(source.Server);
            if (server is null)
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipUnknownServer, null, $"Server \"{source.Server.Trim()}\" isn't supported."));
                continue;
            }
            var name = source.Name.Trim();
            if (name.Length == 0)
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, "No character name."));
                continue;
            }

            var existing =
                data.Characters.FirstOrDefault(c => source.Id.Length > 0 && c.LifeTrackerId == source.Id && c.Server == server)
                ?? data.Characters.FirstOrDefault(c => c.Server == server && TrackerOperations.IsSameName(c.Name, name));

            if (existing is not null)
            {
                if (!matchedIds.Add(existing.Id))
                {
                    entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, DuplicateInFile));
                    continue;
                }
                if (data.Characters.Any(c => c.Id != existing.Id && c.Server == server && TrackerOperations.IsSameName(c.Name, name)))
                {
                    entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, existing.Id, $"Another {server} character is already called {name}."));
                    continue;
                }
                entries.Add(new(source, LifeTrackerImportAction.Update, existing.Id, null));
                continue;
            }

            if (plannedNew.Any(p => p.Server == server && TrackerOperations.IsSameName(p.Name, name)))
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, DuplicateInFile));
                continue;
            }
            plannedNew.Add((server, name));
            entries.Add(new(source, LifeTrackerImportAction.Add, null, null));
        }

        return new LifeTrackerImportPlan(entries);
    }

    /// <summary>Plans against the current data and applies the plan. Returns the plan that was applied.</summary>
    public static LifeTrackerImportPlan Apply(TrackerData data, LifeTrackerBackup backup)
    {
        var plan = Plan(data, backup);
        var folderNames = backup.Folders.ToDictionary(f => f.Id, f => f.Name, StringComparer.Ordinal);

        foreach (var entry in plan.Entries)
        {
            if (entry.Action is not (LifeTrackerImportAction.Add or LifeTrackerImportAction.Update)) continue;
            var source = entry.Source;
            var folderId = source.FolderId is { } f && folderNames.TryGetValue(f, out var folderName)
                ? FindOrCreateFolder(data, folderName)
                : null;
            var lifeTrackerId = source.Id.Length > 0 ? source.Id : null;

            if (entry.Action == LifeTrackerImportAction.Add)
            {
                data.Characters.Add(new Character
                {
                    Server = Servers.Canonical(source.Server)!,
                    Name = source.Name.Trim(),
                    FolderId = folderId,
                    LifeTrackerId = lifeTrackerId,
                });
            }
            else
            {
                var character = data.Characters.First(c => c.Id == entry.ExistingCharacterId);
                character.Name = source.Name.Trim();
                character.LifeTrackerId = lifeTrackerId ?? character.LifeTrackerId;
                if (folderId is not null) character.FolderId = folderId;
            }
        }

        return plan;
    }

    private static string FindOrCreateFolder(TrackerData data, string name)
    {
        var folder = data.Folders.FirstOrDefault(f => TrackerOperations.IsSameName(f.Name, name));
        if (folder is null)
        {
            folder = new Folder { Name = name };
            data.Folders.Add(folder);
        }
        return folder.Id;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS, all tests.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add DDO Life Tracker character import" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Prove the new tests cover the code, then push

**Files:** none changed unless a gap is found.

- [ ] **Step 1: Mutation spot checks**

For each line below, make the change, run `dotnet test`, confirm at least one test FAILS, then revert with `git checkout -- <file>`:

| File | Change | Test that must fail |
|---|---|---|
| `CatalogConverter.cs` | in `GetEffects`, drop records whose `type` is null (`if (type is null) continue;`) | `AffixWithNoBonusType_IsKeptWithNullBonusType` |
| `CatalogValidator.cs` | change `MinimumRetainedPercent` to `90` | `ItemCountBelow95Percent_Fails` |
| `TrackerStore.cs` | in `TryRead`, remove `if (data is null) return null;` | `Load_UnreadableMainFile_RecoversFromBakAndPreservesBadFile` (the `"null"` case) |
| `Servers.cs` | use `StringComparison.Ordinal` and drop `Trim()` | `Plan_IntoEmptyData_AddsSupportedServers_AndCanonicalisesServerCase` |
| `TrackerOperations.cs` | make `IsSameName` compare with `StringComparison.Ordinal` without trimming | `AddCharacter_SameNameDifferingOnlyByCaseOrSpaces_IsRejected` |
| `ItemQuery.cs` | remove `.Trim()` from the search check | `Search_IgnoresCaseAndSurroundingSpaces` |

- [ ] **Step 2: Final full run**

Run: `dotnet test`
Expected: PASS, 0 failed. `git status` shows a clean tree.

- [ ] **Step 3: Push (ask the owner first)**

```bash
git push origin main
```
