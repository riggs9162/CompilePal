# Compile workspace

CompilePal++ 029.4 replaces the separate configuration and output tabs with a persistent map queue, preset list, compile stages, option inspector, and output panel. Drag the dividers to change panel sizes. Existing preset files keep their format; opening the option catalog does not add arguments to a preset.

## Configure stages without writing commands

Select a stage to see its available options. Check an option to enable it, then enter its value if needed. File and folder options include a browse button. Unchecking an option removes it from the selected preset. The argument preview updates as you edit. The map and game placeholders are expanded when compilation starts.

Search matches option names, command flags, descriptions, and warnings. Use **Ctrl+K** to focus the search. Enabled options appear first when selecting a stage. Repeatable options have **Add another value**. **Custom argument** remains available for advanced flags, plugins, and future compiler versions.

The bundled catalog covers the 93 VBSP++, 14 VVIS++, and 101 VRAD++ options printed by the September 15, 2026 Tools++ binaries, combined with CompilePal's existing definitions. Duplicate flags retain their original preset names for compatibility. REPACK exposes the relevant BSPZIP++ thread and verbosity controls; its compression operation and map operand remain managed by the stage. PACK and other built-in stages expose their own CompilePal options, not arbitrary BSPZIP operations.

Tools++-only additions appear when the corresponding executable is named `vbsp++.exe`, `vvis++.exe`, `vrad++.exe`, or `bspzip++.exe`. Already-enabled options remain visible when switching tools so they cannot disappear silently from a preset. The `-game` option is displayed as managed by the app. The catalog is a versioned reference, not live introspection or a guarantee that every flag is supported by every game or later binary. Existing game compatibility warnings are retained.

## Settings

Open the **cogwheel** to choose:

- **System**, **Dark**, or **Light** appearance. System follows Windows' app theme and listens for preference changes while open. It does not change Windows settings.
- Compact option spacing.
- Remembered window and panel sizes.
- Log text size from 10 to 20.
- Automatic scrolling when already at the end of the output.
- The existing compile completion sound and advanced error-cache settings.

Appearance and layout preferences are per Windows user, stored in `%LocalAppData%/CompilePal++/workspace.json`. General compiler settings keep the existing `Settings.json` location. Cancel closes Settings without applying the edited controls.

## Compile feedback

**Check tools** inspects included maps, their presets, relevant executable paths, empty option values, and PACK/CUBEMAPS ordering relative to REPACK. The same checks appear before starting a compile if they find issues. They are advisory: custom workflows can choose **Compile anyway**. They do not launch tools, validate compiler versions, inspect all content dependencies, or parse custom-program command lines.

Stages report running, finished duration, stopped, or failed status. The footer reports overall completion or an incomplete compile. Output remains visible while configuring stages. **Ctrl+F** focuses output search. The warnings/errors view includes detected diagnostic messages and text containing warning, error, or failed. Filtering leaves the original log and clickable diagnostic links intact; return to the unfiltered view to use those links. **Copy log** copies the complete log.

## Validation and limits

The workspace regression project loads the production WPF controls in an isolated fixture. It covers argument editing, repeated values, all captured compiler flags, stock-tool filtering, preset save/reload, log filtering, theme resources, bounded preferences, and custom-program controls. Content renderings were inspected at 1240 by 820 and 1000 by 680. These are WPF layout renderings, not a full desktop interaction test.

Native folder dialogs, live Windows theme-change notifications, every monitor/DPI combination, and a complete graphical Publish run remain manual checks. The [Tools++ runtime limitations](toolsplusplus-validation.md) still apply, including the unverified real in-game CUBEMAPS step and external VBSPInfo/VPK dependencies.

## Build checks

```powershell
dotnet run --project Workspace.Tests/Workspace.Tests.csproj -c Release -r win-x64 -- CompilePalX artifacts/ui-checks
```

The tests use temporary synthetic profiles and presets. They do not load the user's installation or run map compilers.
