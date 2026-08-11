# BlazorEditor

A drag-and-drop editor for hierarchical form field definitions. The hierarchy lives in
colon-delimited `FullId` / `ParentId` paths on a flat `Field` list, so the tree can be stored
in a single table with no self-referencing keys.

The app opens straight onto the editor with a blank form. It also accepts an existing
definition to edit — see [Opening on an existing definition](#opening-on-an-existing-definition).

## Running it

```bash
git clone https://github.com/ztrv/BlazorEditor.git
cd BlazorEditor
dotnet run
```

Then open the URL `dotnet run` prints (http://localhost:5188 by default). No database, no
NuGet packages beyond the framework.

Requires the .NET 8 SDK. To move to .NET 9 or 10, change `<TargetFramework>` in
`BlazorEditor.csproj`; nothing else in the project is version-specific.

## The model

```csharp
class Field
{
    string  Id;              // 3-5 chars, unique only among siblings
    string  FullId;          // ParentId + ":" + Id, or just Id at the root
    string  ParentId;        // the parent's FullId, empty at the root
    string  Name;
    int     SortOrder;       // zero-based position among siblings
    char    FieldType;
    string? OriginalFullId;  // FullId at load time; null for fields added since
}
```

A three-level field ends up as `cust:addr:postc` — its `ParentId` is `cust:addr` and its
`Id` is `postc`.

## How paths stay correct

The editor doesn't mutate `Field` objects directly. `FieldTree.Build` converts the flat list
into a `FieldNode` tree where `FullId` is a **computed** property that walks the parent chain,
never a stored string. Renaming an id or dragging a subtree therefore repairs every descendant
path for free — there's no cascade to write and no way for a stale path to survive a move.

`FieldTree.Flatten` walks the tree depth-first on the way out and stamps the final `FullId`,
`ParentId` and `SortOrder`. That's what `OnSave` and `FieldsChanged` hand back.

Because ids aren't unique, nodes are tracked by an internal `Guid Key` rather than by id or
path. That key is also what `@key` uses, so Blazor's diffing survives reordering.

`Build` is deliberately tolerant of imperfect input: a missing `FullId` is recomputed from
`ParentId` + `Id`, and a field whose parent doesn't exist lands at the root rather than
vanishing. Nothing is silently dropped.

## Opening on an existing definition

The app starts blank because `FormDesignerState.Fields` is null until something loads it.
Three ways to change that:

**1. From the UI** — "Load a definition" opens a panel with a sample form and a JSON box.
Useful for checking that a definition round-trips before you wire up storage.

**2. Programmatically** — inject `FormDesignerState` and load before the editor page initialises:

```csharp
@inject FormDesignerState State
@inject NavigationManager Nav

private async Task EditForm(int formId)
{
    State.Load(await Repository.GetFieldsAsync(formId));
    Nav.NavigateTo("/");
}
```

`Load` copies the fields, so the caller's list is left untouched. `LoadJson(string)` does the
same from serialized input and throws `JsonException` on malformed data.

**3. Use the component directly** and skip the service entirely:

```razor
<FieldHierarchyEditor Fields="_fields"
                      FieldsChanged="f => _fields = f"
                      OnSave="Persist" />
```

`Fields` is only re-read when a *different* list instance arrives, so echoing `FieldsChanged`
back into `Fields` won't wipe in-progress edits.

## Saving

`Editor.razor`'s `HandleSave` currently just records a timestamp. Replace it with your own
persistence:

```csharp
private async Task HandleSave(List<Field> fields)
{
    await Repository.SaveFormAsync(fields);
}
```

`FormDesignerState.Saved` also fires on every save if you'd rather subscribe than override.

## Change tracking

When a form is loaded, every field records the `FullId` it arrived with. That baseline
survives all editing, so after renaming `addr` to `loc` the postcode field reports:

```
FullId          cust:loc:postc
OriginalFullId  cust:addr:postc
```

Note that the *descendants* of a renamed field change path too, even though nobody touched
them. That's the main reason to track originals rather than trying to infer changes after
the fact.

`Field.OriginalFullId` is null for fields added during the session, so `IsNew` and `HasMoved`
distinguish the three states. The property is populated on the way out and isn't part of the
form definition — use it to reconcile against what you have stored, then discard it.

### Deletions

A deleted field is absent from the saved list, so `OnSave` alone cannot tell you about it.
`OnSaveChanges` compares the saved form against the load-time baseline and reports removals
as well:

```razor
<FieldHierarchyEditor Fields="_fields" OnSaveChanges="Reconcile" />
```

```csharp
private async Task Reconcile(FieldChangeSet changes)
{
    foreach (var (wasPath, nowPath) in changes.PathMap)
        await Answers.RepathAsync(wasPath, nowPath);

    foreach (var c in changes.Removed)
        await Answers.DropAsync(c.OriginalFullId!);

    foreach (var c in changes.Added)
        await Answers.CreateAsync(c.Field!);
}
```

Deleting a group reports the group *and* every descendant as removed, so nothing is orphaned.

| Kind | Meaning |
|---|---|
| `Unchanged` | Existed at load time, still at the same path |
| `Added` | Created during this session; `OriginalFullId` is null |
| `Renamed` | Same parent, different id |
| `Reparented` | Moved under a different parent |
| `Removed` | Was in the baseline, isn't in the saved form; `Field` is null |

`PathMap` maps original path to current path for everything that survived and moved — the
map to replay against stored answers, validation rules, or anything else keyed by path.

`FieldChangeSet.Compare(original, current)` is also callable directly, as long as `current`
carries `OriginalFullId`. Comparing two plain lists can't tell a rename from a delete-plus-add.

### Rebasing

Loading always rebases: a field's original path is where it sat when it was handed to the
editor, not where it sat in some earlier session. Any `OriginalFullId` already on an incoming
field is ignored. So save, persist, reload, and the change set starts empty again.

The baseline is captured when a *different* list instance arrives in `Fields` — the same
moment the tree is rebuilt. Saving doesn't reset it, so you can save twice and the second
change set is still measured against the original load.

## Drop semantics

| Drop target | Result |
|---|---|
| A row | Becomes the last child of that row; the row auto-expands |
| The line between two rows | Becomes a sibling of the row below, inserted before it |
| The tail area below the tree | Moves to the end of the root level |

To append to the end of a nested group, drop onto the group row itself. Dropping a node into
its own descendant is rejected in `FieldTree.CanReparent`, so cycles can't be created.

The drag handle only sets `draggable="true"` on mousedown. Without that, a permanently
draggable row blocks caret placement and text selection inside the row's own inputs in
Firefox. Arming on mousedown works because the browser doesn't check `draggable` until the
pointer has moved a few pixels, by which point Blazor has re-rendered.

HTML5 drag events don't fire on touch devices, so every row also carries `↑ ↓ → ←` buttons
that perform the same four moves. Those are keyboard reachable, which keeps the whole editor
usable without a mouse.

## Validation

Save is disabled while any field fails:

- id present, 3–5 characters, no colon
- id unique among its own siblings

The sibling rule is the one constraint the model implies but doesn't state: two siblings
sharing an id would produce identical `FullId` values and collide.

## Layout

```
BlazorEditor/
├── Components/
│   ├── App.razor                        host document
│   ├── Routes.razor                     router
│   ├── _Imports.razor
│   ├── FieldHierarchyEditor.razor       the editor
│   ├── FieldHierarchyEditor.razor.css   scoped styles
│   ├── Layout/MainLayout.razor
│   └── Pages/
│       ├── Editor.razor                 "/" — the only real page
│       └── Error.razor
├── Models/
│   ├── Field.cs                         persisted record + field-type catalog
│   ├── FieldNode.cs                     working node, computed FullId
│   ├── FieldTree.cs                     Build / Flatten / CanReparent
│   ├── FieldChanges.cs                  FieldChangeSet — diff against the baseline
│   └── SampleForms.cs                   demo definition
├── Services/FormDesignerState.cs        carries a definition into the editor
├── wwwroot/app.css
└── Program.cs
```

## Field types

Edit `FieldTypeCatalog.All` in `Models/Field.cs` to change the offered types. Each is a
`char` code and a display label; nothing else in the project depends on the specific codes.
