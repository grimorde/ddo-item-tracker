# DDO Item Tracker

Track the named items you own in Dungeons & Dragons Online, and where each copy is held, across characters and servers.

Item and set data comes from [illusionistpm/ddo-gear-planner](https://github.com/illusionistpm/ddo-gear-planner), which is scraped from [DDO Wiki](https://ddowiki.com).

## Licence

Copyright (C) 2026 grimorde

The source code is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. It is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See [LICENSE](LICENSE) for the full text.

The GPL covers the code only. The item catalog data is licensed separately, as below.

### Data licence

The item catalog (`src/DdoItemTracker/Resources/Raw/catalog.json`) is converted from the ddo-gear-planner JSON files and is shared under [CC BY-SA 2.5](https://creativecommons.org/licenses/by-sa/2.5/), the licence DDO Wiki content is published under. illusionistpm gave permission to use the files on the condition that both ddo-gear-planner and DDO Wiki are credited with links. The app shows these credits in Settings.

## Build and test

    dotnet test tests/DdoItemTracker.Core.Tests
    dotnet test tests/DdoItemTracker.Presentation.Tests

## Run the app

    dotnet build src/DdoItemTracker -f net10.0-windows10.0.19041.0
    dotnet run --project src/DdoItemTracker -f net10.0-windows10.0.19041.0

Android: `dotnet build src/DdoItemTracker -f net10.0-android`. iOS needs a paired Mac.

## Catalog updates

The app does not need a new build when items change in the game. The **Publish catalog** workflow (`.github/workflows/publish-catalog.yml`) checks ddo-gear-planner every day at 06:00 UTC. Most days nothing has changed and nothing is published. When the data has changed, usually after a game update, and the rebuilt catalog passes validation, the workflow uploads `catalog.json` and `catalog-version.json` to the `catalog-latest` release. The app checks that release once a day when it starts, and from **Settings > Check for catalog update**.

To publish straight away, open **Actions > Publish catalog > Run workflow** on GitHub. If validation fails, the run fails and nothing is published.

## Refresh the built-in catalog

The built-in catalog is what a new install starts with, and what **Reset to built-in catalog** goes back to. Refresh it before a release:

    dotnet run --project tools/CatalogBuilder -- --out src/DdoItemTracker/Resources/Raw/catalog.json

The builder exits with 3 and writes nothing when the catalog is already at the latest upstream commit.

See `docs/superpowers/specs/` for the design.
