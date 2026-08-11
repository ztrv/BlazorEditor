namespace BlazorEditor.Models;

/// <summary>What happened to a field between loading the form and saving it.</summary>
public enum FieldChangeKind
{
    /// <summary>Existed at load time and still sits at the same path.</summary>
    Unchanged,

    /// <summary>Added during this editing session. Has no original path.</summary>
    Added,

    /// <summary>Kept the same parent but its id changed.</summary>
    Renamed,

    /// <summary>Moved to a different parent, possibly with an id change as well.</summary>
    Reparented,

    /// <summary>Present at load time and no longer in the form.</summary>
    Removed,
}

/// <summary>
/// One field's fate. <see cref="Field"/> is null only for <see cref="FieldChangeKind.Removed"/>,
/// where nothing survives in the edited form to point at.
/// </summary>
public sealed record FieldChange(
    FieldChangeKind Kind,
    string? OriginalFullId,
    string? CurrentFullId,
    Field? Field)
{
    public override string ToString() => Kind switch
    {
        FieldChangeKind.Added => $"+ {CurrentFullId}",
        FieldChangeKind.Removed => $"- {OriginalFullId}",
        FieldChangeKind.Unchanged => $"  {CurrentFullId}",
        _ => $"~ {OriginalFullId} -> {CurrentFullId}",
    };
}

/// <summary>
/// The difference between a form as loaded and the same form as edited.
///
/// Removals are the reason this type exists: a deleted field is absent from the saved list,
/// so it can only be found by comparing against the load-time baseline.
/// </summary>
public sealed class FieldChangeSet
{
    public IReadOnlyList<FieldChange> All { get; }

    public IReadOnlyList<FieldChange> Added { get; }
    public IReadOnlyList<FieldChange> Renamed { get; }
    public IReadOnlyList<FieldChange> Reparented { get; }
    public IReadOnlyList<FieldChange> Removed { get; }
    public IReadOnlyList<FieldChange> Unchanged { get; }

    /// <summary>Every field whose path changed, whether by rename, reparent, or both.</summary>
    public IEnumerable<FieldChange> Moved => Renamed.Concat(Reparented);

    /// <summary>
    /// Original path to current path, for every surviving field that moved. This is the map
    /// to replay against stored answers, validation rules, or anything else keyed by path.
    /// </summary>
    public IReadOnlyDictionary<string, string> PathMap { get; }

    public int ChangeCount => Added.Count + Renamed.Count + Reparented.Count + Removed.Count;

    public bool HasChanges => ChangeCount > 0;

    private FieldChangeSet(IReadOnlyList<FieldChange> all)
    {
        All = all;
        Added = all.Where(c => c.Kind == FieldChangeKind.Added).ToList();
        Renamed = all.Where(c => c.Kind == FieldChangeKind.Renamed).ToList();
        Reparented = all.Where(c => c.Kind == FieldChangeKind.Reparented).ToList();
        Removed = all.Where(c => c.Kind == FieldChangeKind.Removed).ToList();
        Unchanged = all.Where(c => c.Kind == FieldChangeKind.Unchanged).ToList();

        PathMap = Moved
            .Where(c => c.OriginalFullId is not null && c.CurrentFullId is not null)
            .ToDictionary(c => c.OriginalFullId!, c => c.CurrentFullId!, StringComparer.OrdinalIgnoreCase);
    }

    public static readonly FieldChangeSet Empty = new(Array.Empty<FieldChange>());

    /// <summary>
    /// Compares an edited form against the list it was loaded from.
    /// <paramref name="current"/> must carry <see cref="Field.OriginalFullId"/>, which the
    /// editor populates — comparing two plain lists cannot tell a rename from a delete-plus-add.
    /// </summary>
    public static FieldChangeSet Compare(IEnumerable<Field>? original, IEnumerable<Field>? current)
    {
        var baseline = original?.ToList() ?? new List<Field>();
        var edited = current?.ToList() ?? new List<Field>();

        var changes = new List<FieldChange>();
        var survivors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in edited)
        {
            if (f.OriginalFullId is null)
            {
                changes.Add(new FieldChange(FieldChangeKind.Added, null, f.FullId, f));
                continue;
            }

            survivors.Add(f.OriginalFullId);

            if (!f.HasMoved)
            {
                changes.Add(new FieldChange(FieldChangeKind.Unchanged, f.OriginalFullId, f.FullId, f));
                continue;
            }

            // Same parent path means the id itself changed; otherwise the field moved.
            var kind = string.Equals(ParentOf(f.OriginalFullId), f.ParentId, StringComparison.OrdinalIgnoreCase)
                ? FieldChangeKind.Renamed
                : FieldChangeKind.Reparented;

            changes.Add(new FieldChange(kind, f.OriginalFullId, f.FullId, f));
        }

        foreach (var f in baseline)
        {
            var path = FieldTree.PathOf(f);
            if (!survivors.Contains(path))
            {
                changes.Add(new FieldChange(FieldChangeKind.Removed, path, null, null));
            }
        }

        return new FieldChangeSet(changes);
    }

    /// <summary>Everything before the last colon, or empty for a root-level path.</summary>
    private static string ParentOf(string fullId)
    {
        var i = fullId.LastIndexOf(':');
        return i < 0 ? string.Empty : fullId[..i];
    }
}
