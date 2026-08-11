namespace BlazorEditor.Models;

/// <summary>Conversion between the flat <see cref="Field"/> list and the editable node tree.</summary>
public static class FieldTree
{
    /// <summary>
    /// Builds a tree from a flat list. Tolerates missing FullIds, unknown parents
    /// (those fields land at the root) and duplicate paths (last one wins).
    /// </summary>
    public static List<FieldNode> Build(IEnumerable<Field>? fields)
    {
        var roots = new List<FieldNode>();
        if (fields is null) return roots;

        var source = fields.ToList();
        var byPath = new Dictionary<string, FieldNode>(StringComparer.OrdinalIgnoreCase);
        var pairs = new List<(Field Field, FieldNode Node, int Depth)>();

        foreach (var f in source)
        {
            var path = PathOf(f);

            var node = new FieldNode
            {
                Id = f.Id ?? string.Empty,
                Name = f.Name ?? string.Empty,
                FieldType = f.FieldType == '\0' ? 'T' : f.FieldType,

                // The baseline for this editing session. Loading always rebases: a field's
                // original path is where it sat when it arrived, not where it sat in some
                // earlier session. Anything already in f.OriginalFullId is ignored.
                OriginalFullId = path,
            };

            byPath[path] = node;
            pairs.Add((f, node, PathDepth(path)));
        }

        // Parents must exist before children, then siblings honour SortOrder.
        foreach (var (field, node, _) in pairs.OrderBy(p => p.Depth).ThenBy(p => p.Field.SortOrder))
        {
            var parentPath = field.ParentId ?? string.Empty;

            if (parentPath.Length > 0
                && byPath.TryGetValue(parentPath, out var parent)
                && !ReferenceEquals(parent, node)
                && !node.IsAncestorOf(parent))
            {
                node.Parent = parent;
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>Walks the tree depth first and stamps FullId, ParentId and SortOrder.</summary>
    public static List<Field> Flatten(IEnumerable<FieldNode> roots)
    {
        var result = new List<Field>();
        Walk(roots);
        return result;

        void Walk(IEnumerable<FieldNode> nodes)
        {
            var order = 0;
            foreach (var n in nodes)
            {
                result.Add(new Field
                {
                    Id = n.Id,
                    FullId = n.FullId,
                    ParentId = n.ParentFullId,
                    Name = n.Name,
                    SortOrder = order++,
                    FieldType = n.FieldType,
                    OriginalFullId = n.OriginalFullId,
                });
                Walk(n.Children);
            }
        }
    }

    /// <summary>True when <paramref name="node"/> may be placed under <paramref name="newParent"/>.</summary>
    public static bool CanReparent(FieldNode node, FieldNode? newParent) =>
        newParent is null || (!ReferenceEquals(node, newParent) && !node.IsAncestorOf(newParent));

    /// <summary>The list a node currently lives in: its parent's children, or the roots.</summary>
    public static List<FieldNode> SiblingsOf(List<FieldNode> roots, FieldNode node) =>
        node.Parent?.Children ?? roots;

    /// <summary>
    /// Moves a node under <paramref name="newParent"/> at <paramref name="index"/>.
    /// Returns false when the move would create a cycle or the node isn't in the tree.
    /// </summary>
    public static bool Move(List<FieldNode> roots, FieldNode node, FieldNode? newParent, int index)
    {
        if (!CanReparent(node, newParent)) return false;

        var from = SiblingsOf(roots, node);
        var to = newParent?.Children ?? roots;

        var oldIndex = from.IndexOf(node);
        if (oldIndex < 0) return false;

        from.RemoveAt(oldIndex);

        // Removing the node first shifts everything after it down one, so a target index
        // beyond the old position in the same list has to compensate.
        if (ReferenceEquals(from, to) && oldIndex < index) index--;

        to.Insert(Math.Clamp(index, 0, to.Count), node);
        node.Parent = newParent;
        return true;
    }

    /// <summary>Dropped on a row: becomes that row's last child.</summary>
    public static bool DropInto(List<FieldNode> roots, FieldNode node, FieldNode target)
    {
        if (!CanReparent(node, target)) return false;
        target.Expanded = true;
        return Move(roots, node, target, target.Children.Count);
    }

    /// <summary>Dropped on the line above a row: becomes that row's sibling, just before it.</summary>
    public static bool DropBefore(List<FieldNode> roots, FieldNode node, FieldNode target)
    {
        if (!CanReparent(node, target.Parent)) return false;
        var siblings = target.Parent?.Children ?? roots;
        return Move(roots, node, target.Parent, siblings.IndexOf(target));
    }

    /// <summary>Nests a node under its immediately preceding sibling.</summary>
    public static bool Indent(List<FieldNode> roots, FieldNode node)
    {
        var siblings = SiblingsOf(roots, node);
        var i = siblings.IndexOf(node);
        if (i <= 0) return false;

        var newParent = siblings[i - 1];
        newParent.Expanded = true;
        return Move(roots, node, newParent, newParent.Children.Count);
    }

    /// <summary>Moves a node out one level, landing just after its former parent.</summary>
    public static bool Outdent(List<FieldNode> roots, FieldNode node)
    {
        var parent = node.Parent;
        if (parent is null) return false;

        var grandSiblings = parent.Parent?.Children ?? roots;
        return Move(roots, node, parent.Parent, grandSiblings.IndexOf(parent) + 1);
    }

    /// <summary>
    /// The path a stored field represents. Prefers <see cref="Field.ParentId"/> + <see cref="Field.Id"/>,
    /// falling back to <see cref="Field.FullId"/> when the parent path is empty.
    /// </summary>
    public static string PathOf(Field f)
    {
        if (!string.IsNullOrEmpty(f.ParentId)) return $"{f.ParentId}:{f.Id}";
        return string.IsNullOrEmpty(f.FullId) ? f.Id ?? string.Empty : f.FullId;
    }

    private static int PathDepth(string path) =>
        string.IsNullOrEmpty(path) ? 0 : path.Count(c => c == ':');
}
