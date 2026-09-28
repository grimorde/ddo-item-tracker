# DDO Item Tracker - Design

**Date:** 2026-09-25
**Status:** Approved. Revised 2026-09-28: the app is item-driven and every part of a copy's location is optional (sections 1, 4.2, 5, 7, 8).
**Author:** grimorde (with Claude)

## 1. Purpose

A .NET MAUI app for Dungeons & Dragons Online players to record which **named items** they own. The app is **item-driven**: the player browses the catalog, opens an item and records that they have it. Saying **where** a copy is held (server, Shared Bank, character, storage) is optional detail. A player may track locations fully, track only the server, or ignore location altogether. Characters exist only as a pick-list for that optional detail. It is a sibling to DDO Life Tracker (same author) and follows its conventions.

### Goals

- Browse the full catalog of DDO named items and filter it by level, slot, type, pack, quest, set membership and ownership.
- Record owned copies by hand, in one tap when no location is wanted. A player can own any number of copies of an item.
- Optionally record where each copy is: server, then Shared Bank or a character, then (for a character) Inventory or Bank. Inventory includes equipped gear. Every level can be left out and added later.
- Show an item's details, including its effects and, if it belongs to a set, the set's bonus tiers and the other set pieces with their ownership state.
- Optionally import characters from a DDO Life Tracker backup file, as a quick way to fill the character pick-list. Creating characters directly in the app must work without it.
- Keep the item catalog current without an app release.
- Publish to the Windows, Google Play and Apple stores.

### Non-goals (version 1)

- Trove inventory import.
- Farming plans or "what is left to farm" views.
- Any stat, stacking or optimization maths.
- Cloud sync or accounts. All data stays on the device.
- A raid filter. The source data does not record raid drops.
- The Lamannia preview server.

### Precondition for publishing

The catalog comes from [illusionistpm/ddo-gear-planner](https://github.com/illusionistpm/ddo-gear-planner), which carries **no license**. The owner will obtain permission from illusionistpm before any store release. Development may proceed before that.

## 2. Data source

| Property | Value |
|---|---|
| Repo | `illusionistpm/ddo-gear-planner`, default branch `master` |
| Items file | `data/items.json` (a JSON array, 8,207 records at 2026-09-25) |
| Sets file | `data/sets.json` (a JSON object keyed by set name, 282 sets) |
| Refresh cadence | Upstream re-scrapes ddowiki almost daily |
| Latest-commit lookup | `GET https://api.github.com/repos/illusionistpm/ddo-gear-planner/commits?path=data/items.json&per_page=1` (and the same for `data/sets.json`) |
| Pinned download | `https://raw.githubusercontent.com/illusionistpm/ddo-gear-planner/<sha>/data/items.json` |

The files previously lived under `site/src/assets/`. That move is why the paths are configurable (section 6.4).

### 2.1 Upstream record shapes

Item record (fields optional unless noted):

```jsonc
{
  "name": "Absorption Gauntlet",          // required
  "ml": 18,                               // required, minimum level
  "slot": "Gloves",                       // required
  "type": "Hand Wraps",
  "pack": "Vecna Unleashed",
  "quests": ["Taken in Hand"],
  "sets": ["Forbidden Knowledge"],
  "affixes": [{ "name": "Magical Sheltering", "type": "Insight", "value": "10" }],
  "crafting": ["Yellow Augment Slot"],
  "rare": true,
  "artifact": true,
  "url": "/page/Item:Absorption_Gauntlet"
}
```

Slots seen: Weapon, Armor, Offhand, Ring, Helm, Cloak, Trinket, Necklace, Bracers, Belt, Gloves, Boots, Goggles, Quiver.

An affix `value` may be a string (`"+1"`, `"10"`) or a number. An affix with `type: "Bool"` is an on/off effect (for example `Acid`) and is shown by name only.

Set record: `"<Set Name>": [ { "threshold": 5, "affixes": [ ...same affix shape... ] }, ... ]`.

### 2.2 Data quirks the converter must handle

- **Level-scaled items** have one record per level, named `X (level N)`. About 1,400 records. Each level is a distinct catalog item.
- **Duplicate names.** 148 names appear twice. 142 are byte-identical duplicates (mostly longbows) and are dropped. 6 are distinct items sharing a name on different slots (Belt/Necklace, Ring/Trinket, Boots/Bracers pairs) and are kept.

## 3. Architecture

```
DdoItemTracker/
├── DdoItemTracker.slnx
├── global.json, nuget.config          (as in DDO Life Tracker)
├── src/
│   ├── DdoItemTracker.Core/           net10.0 class library, no MAUI references
│   │   ├── Catalog/                   models, CatalogConverter, CatalogValidator, CatalogStore contract
│   │   ├── Ownership/                 Server list, Character, OwnedCopy, StorageType, ownership rules
│   │   ├── Import/                    LifeTrackerBackupReader, TrackerBackup (own format)
│   │   └── Filtering/                 ItemFilter and its evaluation
│   └── DdoItemTracker/                .NET MAUI app
│       ├── Views/, ViewModels/, Services/, Converters/, Helpers/
│       ├── Resources/Raw/catalog.json built-in catalog
│       └── Platforms/                 Windows, Android, iOS
├── tests/
│   └── DdoItemTracker.Core.Tests/     xUnit
└── tools/
    └── CatalogBuilder/                console app: gear-planner files -> catalog.json
```

### 3.1 Conventions carried over from DDO Life Tracker

- .NET 10, C# 13, `Nullable` and `ImplicitUsings` enabled, `MauiXamlInflator=SourceGen`.
- CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) and CommunityToolkit.Maui.
- Shell navigation with registered routes and `IQueryAttributable` for page parameters.
- JSON persistence in `FileSystem.AppDataDirectory` via a singleton service with an in-memory cache.
- Separate desktop and phone page variants where layouts differ.
- The dark fantasy theme (`Colors.xaml`, `Styles.xaml`), copied so the two apps look related.
- Target frameworks: `net10.0-windows10.0.19041.0`, `net10.0-android`, `net10.0-ios`. Unpackaged Windows build.

### 3.2 Why this differs from DDO Life Tracker

- **Core library plus tests.** Catalog conversion, validation, key matching, filtering and imports contain no UI and decide whether user data stays correct across catalog updates. They live in a plain library so xUnit can test them without a device.
- **One conversion path.** `CatalogBuilder` (build time) and the in-app updater (run time) both call `Core.Catalog.CatalogConverter`. The shipped catalog and a downloaded update are produced by the same code.

## 4. Data model

### 4.1 Catalog (read-only, replaced on update)

```
Catalog
  Version        { UpstreamCommit, UpstreamCommitDateUtc, BuiltUtc }
  Items          CatalogItem[]
  Sets           CatalogSet[]

CatalogItem
  Key            string  "<Name>|<MinLevel>|<Slot>"
  Name           string
  MinLevel       int
  Slot           string
  Type           string?
  Pack           string?
  Quests         string[]
  Effects        Effect[]
  CraftingSlots  string[]
  SetNames       string[]
  IsRare         bool
  IsArtifact     bool
  WikiUrl        string?  absolute, "https://ddowiki.com" + upstream url

CatalogSet
  Name           string
  Tiers          SetTier[]   ordered by PiecesRequired
  (derived at load) MemberKeys  string[]  items whose SetNames contain Name

SetTier   { PiecesRequired int, Effects Effect[] }
Effect    { Name string, BonusType string?, Value string?, IsToggle bool }
```

- **Item identity** is `Key = Name|MinLevel|Slot`. Two distinct items producing the same key is a conversion error (section 6.3).
- `Effect.IsToggle` is true when the upstream type is `Bool`. `BonusType` and `Value` are then null.
- Numeric affix values are normalized to strings as written upstream (`"+1"` stays `"+1"`).

### 4.2 User data (`tracker.json`)

```
TrackerData
  SchemaVersion  int (starts at 1)
  Characters     Character[]
  OwnedCopies    OwnedCopy[]

Character  { Id GUID, Server, Name, LifeTrackerId? }
OwnedCopy  { Id GUID, ItemKey, ItemName, Server?, CharacterId?, Storage?, Note?, AddedUtc }

StorageType = SharedBank | Inventory | Bank     (null = not recorded)
Servers     = Cormyr, Moonsea, Shadowdale, Thrane
```

A copy's location is one of these levels. Each is valid; the player can stop at any of them.

| Location | `Server` | `CharacterId` | `Storage` |
|---|---|---|---|
| Not recorded | null | null | null |
| Server only | set | null | null |
| Shared Bank | set | null | `SharedBank` |
| Character | set (the character's server) | set | null, `Inventory` or `Bank` |

Rules, enforced in Core:

- `Server`, when set, is one of the four servers.
- No server means no character and no storage.
- `SharedBank` has no character. `Inventory` and `Bank` need a character. Inventory covers equipped items too.
- A character must exist and be on the copy's server. Choosing a character in the app fills in the server.
- `ItemName` is a snapshot taken when the copy is created so a copy whose key is no longer in the catalog can still be displayed.
- Several copies of the same item are several `OwnedCopy` records.
- A character's `(Server, Name)` pair is unique, compared case-insensitively.
- **Deleting a character** that holds copies requires a choice: keep the copies recorded on that server only (`CharacterId = null`, `Storage = null`, `Server` kept), or delete them.
- There are no character folders.

## 5. Screens

Navigation: two tabs (bottom on phones, sidebar on Windows): **Catalog** and **My Items**; plus **Settings**, which holds **Characters**.

### 5.1 Catalog

- Name search, case-insensitive substring, updated as the user types.
- Filters: MinLevel range, Slot, Type, Pack, Quest, In a set, Artifact, Ownership (All / Owned / Not owned) with an optional Server scope. A copy with no server does not count as owned on any particular server.
- Row: name, MinLevel, slot, pack, and an owned-count badge (for example `×2`) when the user owns copies.

### 5.2 Item detail

- Header: name, MinLevel, slot, type, pack, quests, link to the ddowiki page.
- Your copies, near the top: two buttons, **I have one** (adds a copy with no location, in one tap) and **Add with location...** (opens the form). Each copy shows its location, or "Location not recorded", and its note, with edit and delete.
- Effects: `Name +Value (BonusType)`, toggles by name only. Crafting slots listed.
- Set section, one block per set in `SetNames`: set name, each tier as "N pieces: effects", and every member item with its owned state. Tapping a member opens its detail. A set name with no matching `CatalogSet` shows "Set bonus details not available".

### 5.3 Add / edit copy

- **Server:** Not recorded, or one of the four servers.
- **Held in** (shown when a server is chosen): Not recorded, Shared Bank, or one of that server's characters.
- **Storage** (shown when a character is chosen): Not recorded, Inventory, Bank.
- **Note:** optional.
- A new copy starts at the last location used on the form. Every combination the form can produce is valid (4.2), so Save is always available.

### 5.4 My Items

- The Catalog filters (without Ownership) plus Server (Any, Not recorded, each server), Character, Storage (Any, Not recorded, each type), and **Not in current catalog**.
- Results grouped by location: "Location not recorded", "Cormyr", "Cormyr · Shared Bank", "Cormyr · Grimorde", "Cormyr · Grimorde · Bank".
- Summary line: distinct named items owned out of the catalog total.
- Filtering by one character shows everything that character holds; there is no separate character page.

### 5.5 Characters (in Settings)

- List grouped by server. Add, rename, delete (with the choice from 4.2).
- **Import from DDO Life Tracker** (section 7.2).

### 5.6 Settings

- Characters (5.5).
- Catalog version (upstream commit short SHA and date), **Check for catalog update**, **Reset to built-in catalog**.
- Back up and restore (section 7.1).
- Theme and What's New, as in DDO Life Tracker.

### 5.7 Layout

On Windows, Catalog and My Items show list and item detail side by side. On phones, item detail is a pushed page.

## 6. Catalog pipeline

### 6.1 Build time

`tools/CatalogBuilder` downloads (or reads local copies of) the two upstream files pinned to a given commit, runs `CatalogConverter` and `CatalogValidator`, and writes `src/DdoItemTracker/Resources/Raw/catalog.json`. The built-in catalog is refreshed by re-running the tool before a release.

### 6.2 Conversion

1. Parse items and sets.
2. Drop records missing `name`, `ml` or `slot`, counting them.
3. Drop byte-identical duplicate records (after canonical JSON serialization), counting them.
4. Map each record to `CatalogItem`, computing `Key`.
5. Map sets to `CatalogSet`, tiers ordered by `threshold`.
6. Record set names referenced by items but absent from the sets file.

The converter returns the catalog plus a report: counts of items, sets, dropped-invalid, dropped-duplicate, and unresolved set names.

### 6.3 Validation (an update is refused if any hard rule fails)

Hard rules:

- At least one item and one set.
- No two distinct items share a `Key`.
- Item count is not below 95% of the current catalog's item count.
- Dropped-invalid records are no more than 1% of input records.

Soft rules (reported in the summary, do not block):

- Unresolved set names.
- Items removed and added relative to the current catalog.

### 6.4 Update manifest

Before checking, the app fetches a small JSON manifest from the owner's GitHub repo (the DdoItemTracker repo, for example `catalog-manifest.json` on its default branch):

```jsonc
{
  "schemaVersion": 1,
  "repo": "illusionistpm/ddo-gear-planner",
  "branch": "master",
  "itemsPath": "data/items.json",
  "setsPath": "data/sets.json"
}
```

If the manifest cannot be fetched or parsed, the app uses the same values compiled into it. If upstream moves its files, editing this manifest fixes every installed copy without a store release.

### 6.4a Alternative under consideration: self-hosted catalog

**Status: undecided.** Sections 6.4 and 6.5 describe the current design. This alternative would replace them if adopted.

A scheduled GitHub Action in the owner's DdoItemTracker repo publishes a ready-made catalog, and the app downloads only from the owner's repo.

**Action (daily, for example 06:00 UTC, after gear-planner's 05:18 UTC run):**

1. Look up the latest upstream commit touching `data/items.json` or `data/sets.json`. Stop if it equals the commit in the currently published catalog.
2. Download both files pinned to that commit.
3. Run `tools/CatalogBuilder` (the same `CatalogConverter` and `CatalogValidator` from Core).
4. If validation passes, publish `catalog.json` (containing `Version.UpstreamCommit`) plus a small `catalog-version.json` (`{ UpstreamCommit, UpstreamCommitDateUtc, BuiltUtc, ItemCount, SetCount }`), for example to a `catalog` branch or as a GitHub Release asset.
5. If validation fails, publish nothing and fail the run so the owner is notified by GitHub.

**App update flow becomes:**

1. Fetch `catalog-version.json` from the owner's repo. If `UpstreamCommit` matches the loaded catalog, report "Catalog is up to date".
2. Download `catalog.json`, validate it again on the device (section 6.3), show the summary, and apply as in section 6.5 steps 5 and 6.

**Effect on the design:**

| | Current (6.4 / 6.5) | Self-hosted (6.4a) |
|---|---|---|
| App depends on | gear-planner repo layout and format, plus the manifest | the owner's repo only |
| Upstream moves or reshapes files | edit manifest (move) or ship an app release (format change) | fix the Action; no app change for either |
| On-device conversion | yes | no, the app only validates |
| Update manifest (6.4) | needed | removed |
| Extra moving part | manifest file | the Action and its published output |
| Permission from illusionistpm | needed to ship their data in the app | same, and also covers republishing it from the owner's repo |

`CatalogConverter` stays in Core either way, because `tools/CatalogBuilder` still builds the built-in catalog shipped in the app.

### 6.5 Run-time update flow

1. Fetch the manifest (fallback to built-in values).
2. Look up the latest commit touching each path. Take the newer of the two as the target commit. If it equals `Catalog.Version.UpstreamCommit`, report "Catalog is up to date" and stop.
3. Download both files pinned to the target commit.
4. Convert and validate.
5. Show a summary: old and new dates, items added, items removed, number of the user's copies whose key is not in the new catalog, and any soft-rule findings. The user chooses **Apply** or **Cancel**.
6. On Apply, write `catalog.json` in AppData via temp file and atomic replace, then reload.

### 6.6 Failure handling

| Situation | Behaviour |
|---|---|
| No network, GitHub unreachable, HTTP error, rate-limited | Message "Couldn't reach GitHub, try again later". Nothing changes. |
| Validation hard rule fails | Update refused, reason shown (for example "item count dropped from 8,207 to 312"). Current catalog kept. |
| AppData `catalog.json` missing or unreadable at start-up | Load the built-in catalog and tell the user once. |
| User chooses Reset to built-in catalog | Delete AppData `catalog.json`, load built-in. |

A catalog update never modifies user data. Copies whose `ItemKey` is not in the loaded catalog are shown with their `ItemName` snapshot and flagged "Not in current catalog".

## 7. Persistence, backup and import

### 7.1 Own data

- Every change is saved immediately to `tracker.json` by writing a temp file and atomically replacing, keeping the previous file as `tracker.json.bak`.
- At start-up, if `tracker.json` is unreadable, load `tracker.json.bak` and tell the user. If both are unreadable, start empty only after telling the user and leaving the unreadable files in place.
- **Export backup**: `{ SchemaVersion, ExportedUtc, Characters, OwnedCopies }` saved or shared as a file.
- **Import backup**: Merge (match by `Id`, incoming wins, others kept) or Replace. Importing the same file twice under Merge changes nothing.
- The catalog is not part of the backup.

### 7.2 DDO Life Tracker character import

- Input: a DDO Life Tracker backup file (`BackupPayload`, schema version 2: `Characters[]` with `Id`, `Server`, `Name`). Folders, past lives and tomes are ignored.
- Matching: first by `LifeTrackerId` equal to the incoming `Id`; otherwise by `(Server, Name)` case-insensitive. A match updates the name; no match adds a character with `LifeTrackerId` set.
- Characters on servers not in the server list are skipped and counted.
- A preview ("5 new, 2 already here, 1 skipped") is shown before anything changes.
- A file that is not a DDO Life Tracker backup produces a plain error and no change.

## 8. Testing

xUnit tests on `DdoItemTracker.Core`, using a small checked-in fixture cut from real gear-planner data.

- **Converter**: identical duplicates dropped; same-name different-slot items kept as two keys; level-scaled items get distinct keys; `Bool` affixes become toggles; set tiers ordered; unresolved set names reported; records missing required fields dropped and counted.
- **Validator**: each hard rule proven to reject a deliberately broken input (duplicate key, 94% item count, over 1% invalid, empty catalog), and a valid catalog passes.
- **Key matching across updates**: copies whose key disappears are flagged, never removed.
- **Ownership rules**: every location level in 4.2 accepted; storage or character without a server rejected; Shared Bank with a character rejected; Inventory or Bank without a character rejected; character on another server rejected; delete-character with each choice.
- **Filtering**: each filter alone and combined, including the Not recorded options; ownership filter with and without server scope (copies with no server excluded from a server scope).
- **Imports**: DDO Life Tracker backup (new, matched by id, matched by server+name, unknown server, malformed); own backup Merge and Replace, and idempotent re-import.
- **Persistence**: atomic save, recovery from a corrupt `tracker.json` using `.bak`.

Manual UI checklist on Windows and Android before each release: browse and filter catalog, open a set item and navigate between set members, add a copy with I have one, add and edit copies at each location level, import Life Tracker characters, run a catalog update (up to date, and a real update), export and restore a backup.

## 9. Open items

- Permission from illusionistpm before store release (owner action).
- Final app name and icon.
- Choose between the upstream manifest (6.4) and the self-hosted catalog (6.4a) before implementing the update feature.
