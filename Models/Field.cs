namespace BlazorEditor.Models;

/// <summary>
/// Flat, persistable representation of one field on a form.
/// Hierarchy is encoded in <see cref="ParentId"/> / <see cref="FullId"/> paths.
/// </summary>
public class Field
{
    /// <summary>3–5 character identifier. Only has to be unique among siblings.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Colon-delimited path: ParentId + ":" + Id (or just Id at the root).</summary>
    public string FullId { get; set; } = string.Empty;

    /// <summary>FullId of the parent field. Empty when the field sits at the root.</summary>
    public string ParentId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Position among siblings, zero based.</summary>
    public int SortOrder { get; set; }

    public char FieldType { get; set; } = 'T';

    /// <summary>
    /// The <see cref="FullId"/> this field had when the form was loaded into the editor,
    /// before any editing. Null for fields added during the session.
    /// </summary>
    /// <remarks>
    /// Set by the editor on the way out. It is not part of the form definition itself, so
    /// there is no need to persist it — use it to reconcile the edited form against what
    /// you already have stored, then discard it.
    /// </remarks>
    public string? OriginalFullId { get; set; }

    /// <summary>True when this field did not exist when the form was loaded.</summary>
    public bool IsNew => OriginalFullId is null;

    /// <summary>
    /// True when the field existed at load time and its path has since changed, whether
    /// through an id rename, a move to a different parent, or both.
    /// </summary>
    public bool HasMoved =>
        OriginalFullId is not null &&
        !string.Equals(OriginalFullId, FullId, StringComparison.OrdinalIgnoreCase);

    public Field Clone() => (Field)MemberwiseClone();

    public override string ToString() => $"{FullId} ({FieldType}) {Name}";
}

/// <summary>The field types offered in the designer. Swap these for your own codes.</summary>
public static class FieldTypeCatalog
{
    public static readonly IReadOnlyList<FieldTypeOption> All = new List<FieldTypeOption>
    {
        new('G', "Group"),
        new('T', "Text"),
        new('A', "Long text"),
        new('N', "Number"),
        new('D', "Date"),
        new('B', "Checkbox"),
        new('L', "Dropdown"),
        new('F', "File"),
    };

    public static string LabelFor(char code) =>
        All.FirstOrDefault(t => t.Code == code)?.Label ?? code.ToString();

    /// <summary>Types that are containers by nature. Used only for the icon hint.</summary>
    public static bool IsContainer(char code) => code == 'G';
}

public sealed record FieldTypeOption(char Code, string Label);
