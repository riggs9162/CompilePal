# Cubemaps without starting the game

The SOURCECUBEMAP stage builds a map's cubemaps with [SourceCubemap](https://github.com/riggs9162/SourceCubemap), a separate tool that renders them on your graphics card. A compile no longer has to open the game, and most maps finish in a second or two.

Reflections look close to the real thing, but they are not pixel-identical to the game's `buildcubemaps`. Test your map in-game before shipping it. The existing CUBEMAPS stage is still there for a final build.

## Set it up

You need SourceCubemap 0.7.00 or newer, 64-bit Windows 10 or 11, and a graphics driver with Vulkan 1.3. SourceCubemap is not included with CompilePal++.

1. Download the latest zip from the [SourceCubemap releases](https://github.com/riggs9162/SourceCubemap/releases) and unzip it anywhere. Keep its files together.
2. Open **Game & tools**, set **SourceCubemap** to the path of `sourcecubemap.exe`, and press **Save**. Each game has its own path.
3. Choose a preset, press **+** beside **Compile stages**, and add **SOURCECUBEMAP**.
4. If the preset also has CUBEMAPS, remove that stage, because both would build the same cubemaps.

**Check tools** reports a missing path, both cubemap stages in one preset, and a cubemap stage that runs after REPACK.

## What it does

The stage runs after COPY and PACK and before REPACK, on the BSP in the game's map folder. It uses the content of your configured game and writes the cubemaps straight into that BSP. A failed or cancelled build leaves the map as it was.

A map compiled from a VMF keeps no backup, because you can compile it again. A `.bsp` you put in the map queue keeps a `.bak` beside it. Compiling never leaves cache files behind.

Anything SourceCubemap can't reproduce exactly is replaced with the closest match, so most maps finish on the first try. Missing materials and models appear as a warning at the end of the compile, and the output lists what happened.

## Options

| Option | What it does |
| --- | --- |
| Quality | `fast`, `standard`, or `high`. `high` renders at double resolution. |
| Lighting | `auto`, `ldr`, `hdr`, or `both`. `auto` builds whatever the map has. |
| Game Rules | `gmod` or `hl2`. Chosen from the game unless you set it. |
| Extra Content | Adds a content folder or archive that the game doesn't load by itself. |
| Graphics Card | Picks a graphics card by a word in its name, such as `3050`. |
| Strict | Stops at the first thing that can't be reproduced exactly. Many maps need this left off. |
| Command Line Argument | Passes anything else to `sourcecubemap build`. |

Garry's Mod uses `gmod` rules, which also read its addons and `cfg/mount.cfg`. Every other game uses `hl2` rules. Set **Game Rules** to `hl2` to keep Garry's Mod from reading content mounted from other drives.

## Limits

- Only desktop BSP version 20 maps are supported. Other versions stop the compile with an error.
- SourceCubemap reads your game's files, so a map with missing content shows gray surfaces or missing props in its reflections.
- Run REPACK after this stage, not before it. SourceCubemap can read a compressed map, but it stores its output uncompressed.

## Validation

These checks ran on Windows 11 with an RTX 3050, SourceCubemap 0.7.00, and the real compile manager:

- `gm_flatgrass` compiled from a VMF and as a queued BSP, in folders with spaces. Both passed `sourcecubemap validate`.
- The VMF compile left no backup, and the queued BSP kept one.
- `gm_construct` with Strict stopped with SourceCubemap's own reason and left the map untouched.
- `intro`, a BSP version 19 map, stopped the compile with a clear error.
- Cancelling `d2_coast_09` mid-build left the map untouched, left no extra files, and ended the tool.
- `background02` finished with a warning about its 7 missing assets.
- A missing tool path stopped the stage and named **Game & tools**, and **Check tools** reported it first.

The final maps were not loaded in-game, and no compile ran through VBSP, VVIS, and VRAD before this stage. Those checks used maps that were already compiled.
