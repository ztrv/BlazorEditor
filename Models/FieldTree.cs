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
            var node = new FieldNode
            {
                Id = f.Id ?? string.Empty,
                Name = f.Name ?? string.Empty,
                FieldType = f.FieldType == '\0' ? 'T' : f.FieldType,
            };

            var path = PathOf(f);
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
                });
                Walk(n.Children);
            }
        }
    }

    /// <summary>True when <paramref name="node"/> may be placed under <paramref name="newParent"/>.</summary>
    public static bool CanReparent(FieldNode node, FieldNode? newParent) =>
        newParent is null || (!ReferenceEquals(node, newParent) && !node.IsAncestorOf(newParent));

    private static string PathOf(Field f)
    {
        if (!string.IsNullOrEmpty(f.ParentId)) return $"{f.ParentId}:{f.Id}";
        return string.IsNullOrEmpty(f.FullId) ? f.Id ?? string.Empty : f.FullId;
    }

    private static int PathDepth(string path) =>
        string.IsNullOrEmpty(path) ? 0 : path.Count(c => c == ':');
}
