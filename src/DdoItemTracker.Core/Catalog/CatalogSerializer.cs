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
