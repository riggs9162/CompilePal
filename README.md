<p align="center">
	<img
		alt="CompilePal++"
		src="CompilePalX/Branding/CompilePalPlusPlus.png"
		width="128"
	/>
</p>

<p align="center">CompilePal++ is a bright pink fork of Compile Pal with support for standalone Tools++ compilers.</p>

This fork adds standalone VBSP++, VVIS++, VRAD++ and BSPZIP++ selection while retaining Compile Pal's existing preset format and executable name, `CompilePalX.exe`. See the [Tools++ guide](docs/toolsplusplus.md) for configuration, validation and utility requirements. Tools++ binaries must be installed separately.



## Build this fork

On Windows with the .NET 10 SDK installed:

```powershell
dotnet run --project ToolsPlusPlus.Tests/ToolsPlusPlus.Tests.csproj -c Release
dotnet run --project ToolsPlusPlus.ManagerTests/ToolsPlusPlus.ManagerTests.csproj -c Release
dotnet publish CompilePalX/CompilePalX.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

The branding uses bright pink `#FF3B9D` with dark text on selected controls. Warning and error severity colors retain their original meaning. The icon uses the original Compile Pal hammer design recolored pink, with its white and gray borders retained. The [PNG source](CompilePalX/Branding/CompilePalPlusPlus.png) is shared by the app and README; regenerate its Windows icon with `powershell -STA -File CompilePalX/Branding/Export-Icon.ps1`.

CompilePal++ builds on [Compile Pal](https://github.com/ruarai/CompilePal). Its [upstream releases](https://github.com/ruarai/CompilePal/releases/latest) do not contain this fork's changes. Get the complete Windows ZIP from the [CompilePal++ 029.3 prerelease](https://github.com/riggs9162/CompilePal/releases/tag/v029.3). The separately attached EXE is for replacing the executable in an existing complete installation; it still needs the accompanying configuration and resource files.

The default branch is `toolspp/standalone-support`. The update checker reads this branch and links to releases in `riggs9162/CompilePal`; it never installs updates automatically. The Windows workflow tests and publishes build artifacts. Releases are tagged from reviewed commits and supplied with validated builds, without the upstream workflow's automatic version commits.


## Features
* Packing
* Error Checking
* Not freezing your computer while compiling
* Cubemaps
* Manifest Generation
* Nav File Generation
* Plugins and Custom Compile Steps
* Batch Compiling
* Much More!

## Guides
* [Quick Start](Guides/QuickStart.md)
* [Reporting An Issue](Guides/Issues.md)
* [Plugin Development (Beta)](Guides/Plugins.md)
* [Custom Compile Steps](Guides/Custom.md)
* [Custom Compile Step Collection](Guides/CustomCollection.md)
* [Command Line Arguments](Guides/CMDArgs.md)
* [Registry Values](Guides/Registry.md)
* [VScript Packing Hints](Guides/VScript.md)

## Contributing

Report issues with this fork in its own repository. For the original project, see [Compile Pal's issue tracker](https://github.com/ruarai/CompilePal/issues).

### Developers
- [ruarai](https://github.com/ruarai)
- [maxdup](https://github.com/maxdup)
- [Exactol](https://github.com/Exactol)
- iMilo


### Bug Testing
- wareya
- Gangleider 
- Matt2468rv 
- Sevin7 
