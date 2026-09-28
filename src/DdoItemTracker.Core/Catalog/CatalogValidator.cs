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
