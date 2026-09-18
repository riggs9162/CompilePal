# CompilePal++ validation

Validated on Windows x64 on 2026-09-18, based on upstream `adf6895afe325fbb76980e4097cebafef2d3191c`.

## Passed

- Guarded source application against the exact upstream revision; all 10 Python applier tests passed.
- Windows x64 self-contained publish with .NET SDK 10.0.401. Microsoft SDK archive SHA-512 verified against official release metadata. Compiler warnings remain; there are no build errors.
- 62 linked-production regression checks for path resolution, Hammer imports, executable launching, GMod REPACK gating, stdout/stderr drainage, failure handling, cancellation and CUBEMAPS mode detection.
- 24 compile-manager lifecycle checks using the production manager and a real WPF dispatcher. Cancellation keeps the manager busy until the worker exits; failed or cancelled stages restore the configuration, skip later stages and never report successful completion.
- 42 application-level smoke checks using the actual application assembly, configuration managers and compiler/packing classes. Five interactive steps were deliberately skipped in this harness and are listed below.
- Actual standalone VBSP++, VVIS++, VRAD++, COPY, PACK and REPACK from directories containing spaces. Tools++ binaries tested report Sep 15 2026 builds.
- Actual stock GMod VBSP, VVIS, VRAD, COPY and PACK; stock GMod REPACK is rejected.
- Unchanged private Fast, Normal, Full, Publish - HDR and Publish - Both presets passed their compiler and packing stages on a sealed-room fixture. Private preset bytes remained unchanged. These presets and their paths are not in source control or the build package.
- Game configuration save/reload through the real JSON configuration manager preserved paths.
- Real compiler cancellation terminated the live process. Leaked and missing map inputs returned failure; application checks classify standalone failures as fatal.
- Standalone packing, compressed payload extraction and decompression round trip; packed test payload verified byte-for-byte.
- Actual installed VBSPInfo read the standalone output and detected both lighting modes. The production CUBEMAPS code selected two passes using a fake game executable. Compressed input failed before game launch as expected.
- WPF resources loaded; pink controls rendered for visual inspection. The v029.3 icon update restores the original hammer design in pink; its transparent background and all seven Windows icon sizes were checked, and the Windows build passed. Compiler behavior is unchanged from v029.2.

## Runtime limits

The application smoke harness skipped GAME for Fast/Normal/Full and the real game launch in both Publish CUBEMAPS steps. Separate bounded GMod launches using a scratch game directory failed before loading the test map: missing base shader/material resources, one access violation, and one stalled startup. The created process was stopped. Normal game configuration hashes were unchanged.

Therefore actual map rendering, visual lighting, compressed map loading in GMod, and real in-game cubemap generation are not verified. This remains a development branch in the user's fork, with no pull request. The normal desktop picker interaction and a full graphical end-to-end Publish run also remain manual checks; the configuration persistence code and compiled XAML were tested independently.

VBSPInfo and VPK remain external game utilities. CUBEMAPS must precede REPACK because the installed stock VBSPInfo fails on compressed BSP lumps. CompilePal's PACK scanner likewise requires an uncompressed BSP. BSPZIP++ `-dir` failed on the compressed fixture while `-extract` succeeded. A successful packer exit is not a substitute for an in-game load test.

VPK packing and the STATS stage were not exercised end to end. Their utility dependencies remain requirements rather than claims of standalone coverage.

Cancellation kills the live tool process tree and cancels pending output reads. If a tool exits after spawning a detached child, Windows process-tree termination cannot find that child through the exited parent; cancellation still returns instead of hanging on inherited output pipes.

The updater targets this fork's `toolspp/standalone-support` default branch. The first fork build is published as prerelease `v029.2` with these runtime limits. No Tools++ binaries, user configurations, content directories, credentials, maps, game assets or private presets are uploaded. Detailed local smoke logs are excluded because they contain machine-specific mounted-content paths.

## Repeat the portable checks

```powershell
dotnet run --project ToolsPlusPlus.Tests/ToolsPlusPlus.Tests.csproj -c Release
dotnet run --project ToolsPlusPlus.ManagerTests/ToolsPlusPlus.ManagerTests.csproj -c Release
dotnet publish CompilePalX/CompilePalX.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/CompilePalPlusPlus
```

Optional real utility check, with a fake game executable so it does not start the game:

```powershell
dotnet run --project ToolsPlusPlus.Tests/ToolsPlusPlus.Tests.csproj -c Release -- --verify-vbspinfo "path/to/vbspinfo.exe" "path/to/uncompressed-map.bsp" 2
```

Use an expected pass count of 1 for a single lighting mode, or 0 to assert that inspection fails without launching the game.

## Workspace update, v029.4

The workspace update passed 62 compiler checks, 32 compile-lifecycle checks (including stage and outcome reporting), and 32 production-WPF workspace checks. The application-assembly smoke suite again passed 42 checks with the same five GAME/CUBEMAPS skips. Private fixture preset bytes remained unchanged. Dark/light workspace content and Settings renderings were inspected, including a 1000 by 680 layout. Windows x64 self-contained publication succeeded. See [workspace validation and manual limits](workspace.md#validation-and-limits).
