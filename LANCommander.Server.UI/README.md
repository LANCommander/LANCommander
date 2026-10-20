# LANCommander.Server.UI

The server's component library. Pages and components in `LANCommander.Server` use the **Controls**
in this project and never Radzen directly. Radzen is an implementation detail: it is referenced with
`PrivateAssets="compile"`, so `@using Radzen` does not even compile in the server, and
`RadzenBoundaryTests` catches anything that slips past that.

## Namespaces

| Namespace | Contents | Imported by |
| --- | --- | --- |
| `LANCommander.Server.UI.Controls` | Radzen-backed and custom controls (`Button`, `Flex`, `Alert`, ...) | Migrated server folders |
| `LANCommander.Server.UI.Services` | `NotificationService`, `DialogService`, `AlertService`, `UploadTracker` | Migrated server folders |
| `LANCommander.Server.UI.Components` | Framework-independent components (`MouseArea`, `ByteSize`, `CodeInput`, ...) | Everywhere |
| `LANCommander.Server.UI.Legacy` | AntDesign-based components awaiting replacement | Unmigrated server folders only |

While the migration is in progress, each server folder's `_Imports.razor` imports either the legacy
set (AntDesign + `Legacy`) or the new set (`Controls`), never both: many control names match
AntDesign's and Razor rejects ambiguous tags.

## Writing a control

- One folder per control or family under `Controls/`, with `@namespace LANCommander.Server.UI.Controls`.
- Inherit `ControlBase` for `Class`, `Style` and captured `AdditionalAttributes`, and apply them to
  the outermost element. Build classes and styles with `ClassBuilder` / `StyleBuilder`.
- **Style modifiers are bools**, written as bare attributes: `<Button Primary Small>`, `<Tag Success>`,
  `<Alert Warning>`. When several conflict, document the precedence (see `Button`, `StatusResolver`).
- Status colours are always `Primary`, `Success`, `Info`, `Warning`, `Danger`.
- **Enums** for values that are mapped or switched on: `IconType`, `Gap`, `Align`, `Justify`,
  `Placement`, `DialogSize`.
- **Binding** uses `Value` / `ValueChanged` (`Values` / `ValuesChanged` for collections).
- Text or rich content: a `string` parameter plus a `...Template` `RenderFragment` that replaces it
  (`Title` / `TitleTemplate`, `Description` / `DescriptionTemplate`).
- Our CSS classes use the `lc-` prefix. Theme through `--rz-*` / `--lc-*` variables in
  `Styles/_theme.scss`; don't restyle Radzen classes elsewhere.
- Styles go in `<Control>.razor.scss` (or a shared `<Family>.scss`), forwarded from
  `_Imports.razor.scss`. TypeScript goes in `<Name>.ts`, exported from `_Imports.razor.ts` and loaded
  through `ScriptProvider`.
- Don't name a control after an HTML void element (`col`, `input`, `link`, `source`, `img`, ...):
  Razor parses those tags as void elements, so the control can't take child content. Likewise avoid
  `Text`: inside `@if { }` blocks Razor reads `<text>` as its own markup tag (hence `Typography`).
- Keep Radzen types out of public signatures; consumers can't compile against them.

Every control needs:

1. bUnit tests in `LANCommander.Server.UI.Tests/Controls`.
2. At least one fixture in `LANCommander.Server.UI.Fixtures/Components/Sets` listing it in `Covers`
   (`FixtureCatalogTests` fails otherwise). Fixtures render at `/_fixtures` in Debug builds.
3. A visual baseline, generated on Linux CI by the *Server UI Tests — Update Visual Baselines* workflow.

## Services

```csharp
@inject NotificationService Notifications
@inject DialogService Dialogs
@inject AlertService Alerts

Notifications.Success("Game saved");
Notifications.Error(ex, "Could not save game");   // logs, and shows ex.Message as the detail

if (await Alerts.ConfirmAsync("Delete Arena Blitz?", danger: true)) { ... }

var result = await Dialogs.OpenAsync<AddToCollectionDialog, AddToCollectionOptions, IEnumerable<Collection>>(
    "Add to Collection", options, new DialogSettings { OkText = "Add" });
```

Dialogs inherit `Dialog<TOptions, TResult>`, override `OnOkAsync` to set `Result` (return `false` to
stay open), and get the OK/Cancel footer, busy state and in-dialog error display from the frame.

## Loading and errors

- `PageContent Loading` / `Spin` / `Button Loading` for work in progress.
- `AsyncContent<T>` loads data and renders loading, error-with-retry, empty and loaded states.
- `ErrorHandler` catches render and event exceptions; `Inline` for a section, full-page otherwise.
  `PageContent` already wraps its content in an inline handler.

## Icons

`<Icon Type="IconType.Trash" />` draws a Phosphor icon inline, sized and coloured like the text around
it; `Bold`, `Fill` and `DuoTone` pick the weight. Regenerate the data with
`python Controls/Icon/generate-icons.py`.

| AntDesign | IconType |
| --- | --- |
| Close | X |
| Edit | PencilSimple |
| Delete | Trash |
| Upload | UploadSimple |
| Download | DownloadSimple |
| Search | MagnifyingGlass |
| Plus | Plus |
| Folder / FolderOpen | Folder / FolderOpen |
| Ellipsis | DotsThree |
| Check | Check |
| Question / QuestionCircle | Question |
