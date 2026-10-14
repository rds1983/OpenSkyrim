# Myra notes

Notes about defining OpenSkyrim.Viewer's UI with Myra and exporting it to C#.

## Workflow

The UI lives in [`OpenSkyrim.Viewer/UI/mainForm.xmmp`](OpenSkyrim.Viewer/UI/mainForm.xmmp) (a MyraPad XML project). It is
exported to C# with the globally installed `myra-export` console tool:

```
myra-export "D:\Projects\OpenSkyrim\OpenSkyrim.Viewer\UI\mainForm.xmmp"
```

The export options are embedded in the xmpp:

```xml
<Project.ExportOptions Namespace="OpenSkyrim.Viewer.UI" Class="MainForm" FieldsVisibility="Private" />
```

`OutputPath` is optional: when omitted, `myra-export` writes next to the xmpp (see
`MyraExport/Program.cs`, which defaults `OutputPath` to the xmpp's own directory). The
generated files therefore land in `OpenSkyrim.Viewer/UI` without needing an explicit path.

The tool writes two files into `OutputPath`:

- `MainForm.Generated.cs` — **always** regenerated. Contains `BuildUI()` and the widget
  field declarations. **Never hand-edit it**; changes are lost on the next export.
- `MainForm.cs` — written **only if it does not already exist**, so the code-behind is
  safe to edit. (In OpenSkyrim.Viewer it already exists and is not touched by the tool.)

Behavior code (event wiring, model/viewer setup) goes in `MainForm.cs`; layout and widget
declarations go in `mainForm.xmmp`.

## Gotchas

### The exporter emits invalid code for widgets with read-only public properties

`ExporterCS.BuildProperties` collects every public property with a public getter, and
`ShouldSerializeProperty` only filters a property out if it is tagged `[DefaultValue]`
(or is a default Thickness/proportion). Read-only properties therefore get an assignment
generated for them, e.g. `TreeView` exposes get-only `SubNodesCount`/`TotalNodesCount`:

```csharp
_myTree.SubNodesCount = 0;   // CS0200: property is read-only
_myTree.TotalNodesCount = 0; // CS0200: property is read-only
```

`SubNodesCount`/`TotalNodesCount` are get-only and have no `[DefaultValue]`, so a
`TreeView` **cannot** be declared in the xmpp. Instead declare a container (e.g. a
`ScrollViewer`) in the xmpp and create the widget in code-behind, then attach it:

```csharp
_myTree = new TreeView();
_myScroll.Content = _myTree;
```

(The same applies to any other Myra widget that exposes read-only public properties
without `[DefaultValue]`.)

### `SplitPane` proportions reset when widgets change

Adding/removing a widget on a `SplitPane` raises `Reset()`, which rebuilds the layout
proportions (all children `Part 1.0`, last child `Fill`). Re-apply the splitter positions
after every add/remove, e.g.:

```csharp
_mainSplit.Widgets.Add(somePanel);
_mainSplit.SetSplitterPosition(0, 0.2f);
_mainSplit.SetSplitterPosition(1, 0.6f);
```

## Notes file convention

Keep this file updated with anything learned about the MyraPad / `myra-export` tooling
and Myra widget behavior that isn't obvious from the code.
