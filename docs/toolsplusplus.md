# CompilePal++ standalone Tools++ support

CompilePal++ is a bright pink fork of Compile Pal based on upstream commit `adf6895afe325fbb76980e4097cebafef2d3191c`. This branch adds standalone tool selection, safer compiler execution, and the CompilePal++ name and artwork. The executable remains `CompilePalX.exe` so existing shortcuts and integrations can keep using it.

## Configure the tools

In Game Configuration, choose **Select Tools++ folder...** and select a folder containing `vbsp++.exe`, `vvis++.exe`, `vrad++.exe`, and `bspzip++.exe`. All four must exist before the fields change. Press **Save** to persist the selection.

Game Folder still points to the game/mod directory containing `gameinfo.txt`. Bin Folder stays the game's bin directory. Compiler paths can live elsewhere and contain spaces. Do not replace the stock game executables. Existing configurations take precedence over imported Hammer configurations, so use the picker to update an existing configuration.

Hammer imports prefer the adjacent BSPZIP++ when VBSP++ is selected. Stock configurations retain stock-tool preference. Missing optional utilities no longer overwrite the shared game bin directory. GMod REPACK is enabled only for an existing `bspzip++.exe`; this filename is a capability hint and assumes a genuine, unrenamed distribution.

## Existing presets

The preset format and parameters are unchanged. User presets, content paths, game configurations, maps and compiler binaries are not included in this fork. Test private presets in a separate local installation and keep a backup of the original installation.

Standalone tools run from their own directory unless a plugin specifies a custom working directory. Packing passes absolute file paths. Compiler stderr is drained concurrently with stdout and displayed after stdout ends. A nonzero standalone compiler exit stops the compile, even when the original stock compiler metadata disabled exit checks. Stock/custom opt-outs remain supported.

PACK utility failures stop the compile before copying the BSP or reporting success. Cancellation terminates the active tool's process tree. The application waits for the compile worker to finish before allowing another compile, preventing overlapping packing runs.

## Utility dependencies and ordering

CUBEMAPS and STATS still need the game's `vbspinfo.exe`; VPK workflows still need `vpk.exe`. The standalone suite does not supply these utilities. Configure their existing game paths explicitly if discovery cannot find them.

CUBEMAPS requires a successful VBSPInfo inspection and a successful game process. Missing, failed or unreadable utility output now stops the stage instead of guessing a lighting mode and reporting success. Single-mode builds explicitly select the detected HDR/LDR mode. Real cubemap generation still requires the installed game and a usable graphics environment.

Run CUBEMAPS before REPACK. The installed stock GMod VBSPInfo cannot inspect compressed Tools++ output. CompilePal's PACK asset scanner also cannot read compressed BSP lumps; decompress first if packing an already compressed BSP. BSPZIP++ compression support does not remove these application/utility limitations.

The update checker and release links point to `riggs9162/CompilePal`. No automatic installation is performed. The fork's default branch is `toolspp/standalone-support`; version files are maintained there. The first fork prerelease is `v029.2`; `v029.3` restores the original hammer icon recolored pink. Use its complete Windows ZIP for a fresh installation; the separate EXE still needs the application's configuration and resource files.

## Build and regressions

On Windows with .NET 10:

```powershell
dotnet run --project ToolsPlusPlus.Tests/ToolsPlusPlus.Tests.csproj -c Release
dotnet run --project ToolsPlusPlus.ManagerTests/ToolsPlusPlus.ManagerTests.csproj -c Release
dotnet publish CompilePalX/CompilePalX.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/CompilePalPlusPlus
```

The regression projects link the production parser, path resolver, executable launcher, utility runner, cubemap process and compile manager. Fake executable modes exercise paths with spaces, stream flooding, exit failures, process-tree cancellation and compatibility checks. The manager checks use a real WPF dispatcher to verify cancellation, fatal errors, unexpected exceptions and successful completion without opening the desktop app. These tests complement actual compiler smoke tests; they do not prove in-game rendering or lighting quality.

See [validation](toolsplusplus-validation.md) for the checks actually run and remaining limitations. This remains a development branch until the remaining runtime checks are accepted.

## Sources and attribution

- [Tools++ author installation instructions and changelog](https://ficool2.github.io/HammerPlusPlus-Website/tools.html)
- [Original Compile Pal repository](https://github.com/ruarai/CompilePal)
- [Reviewed upstream snapshot](https://github.com/ruarai/CompilePal/tree/adf6895afe325fbb76980e4097cebafef2d3191c)

The upstream license and authorship are retained. Tools++ compiler binaries are not bundled or uploaded.
