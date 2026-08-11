namespace BlazorEditor.Models;

/// <summary>
/// The editor's working copy of a field. Ids are not unique across the form, so the
/// designer tracks nodes by <see cref="Key"/> instead and recomputes paths on demand.
/// </summary>
public sealed class FieldNode
{
    public Guid Key { get; } = Guid.NewGuid();

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public char FieldType { get; set; } = 'T';

    /// <summary>
    /// The path this node had when the form was loaded, captured before any editing.
    /// Null when the node was added during this session.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="FullId"/> this is a stored value, deliberately. It is the one
    /// thing that must not follow the node when it moves.
    /// </remarks>
    public string? OriginalFullId { get; init; }

    public bool IsNew => OriginalFullId is null;

    public bool PathChanged =>
        OriginalFullId is not null &&
        !string.Equals(OriginalFullId, FullId, StringComparison.OrdinalIgnoreCase);

    public bool Expanded { get; set; } = true;

    public FieldNode? Parent { get; set; }
    public List<FieldNode> Children { get; } = new();

    /// <summary>Path from the root, always derived — never stored — so moves stay correct.</summary>
    public string FullId => Parent is null ? Id : $"{Parent.FullId}:{Id}";

    public string ParentFullId => Parent?.FullId ?? string.Empty;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    public bool IsAncestorOf(FieldNode? other)
    {
        for (var p = other?.Parent; p is not null; p = p.Parent)
        {
            if (ReferenceEquals(p, this)) return true;
        }
        return false;
    }

    public IEnumerable<FieldNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var d in child.DescendantsAndSelf()) yield return d;
        }
    }
}
