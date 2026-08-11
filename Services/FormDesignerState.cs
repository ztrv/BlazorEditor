using System.Text.Json;
using BlazorEditor.Models;

namespace BlazorEditor.Services;

/// <summary>
/// Holds the definition the editor opens with, and receives it back on save.
///
/// The app starts blank because <see cref="Fields"/> is null until something loads it.
/// To open the editor on an existing form instead, call <see cref="Load"/> before the
/// editor page initialises — from a parent page, a startup hook, or a repository fetch.
/// </summary>
public class FormDesignerState
{
    /// <summary>The definition to open with. Null means "start blank".</summary>
    public IReadOnlyList<Field>? Fields { get; private set; }

    /// <summary>Raised whenever the editor saves.</summary>
    public event Action<IReadOnlyList<Field>>? Saved;

    /// <summary>Loads a definition to edit. Copies the fields so the caller's list is untouched.</summary>
    public void Load(IEnumerable<Field>? fields) =>
        Fields = fields?.Select(f => f.Clone()).ToList();

    /// <summary>Loads a definition from JSON. Throws <see cref="JsonException"/> on malformed input.</summary>
    public void LoadJson(string json)
    {
        var parsed = JsonSerializer.Deserialize<List<Field>>(json, JsonOptions)
                     ?? throw new JsonException("The JSON did not contain a field array.");
        Load(parsed);
    }

    /// <summary>Resets to a blank form.</summary>
    public void Clear() => Fields = null;

    public void Save(IReadOnlyList<Field> fields)
    {
        Fields = fields;
        Saved?.Invoke(fields);
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
}
