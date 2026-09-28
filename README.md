# DDO Item Tracker

Track the named items you own in Dungeons & Dragons Online, and where each copy is held, across characters and servers.

Item and set data comes from [illusionistpm/ddo-gear-planner](https://github.com/illusionistpm/ddo-gear-planner), which is scraped from [ddowiki](https://ddowiki.com).

## Build and test

    dotnet test tests/DdoItemTracker.Core.Tests
    dotnet test tests/DdoItemTracker.Presentation.Tests

## Run the app

    dotnet build src/DdoItemTracker -f net10.0-windows10.0.19041.0
    dotnet run --project src/DdoItemTracker -f net10.0-windows10.0.19041.0

Android: `dotnet build src/DdoItemTracker -f net10.0-android`. iOS needs a paired Mac, as for DDO Life Tracker.

## Refresh the built-in catalog

    dotnet run --project tools/CatalogBuilder -- --out src/DdoItemTracker/Resources/Raw/catalog.json

See `docs/superpowers/specs/` for the design.
