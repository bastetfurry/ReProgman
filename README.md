# ReProgman - Remember Progman?

A clone of the Windows 3.1 Program Manager (progman.exe), written for everyone who misses it.

<img src="docs/assets/screenshot-en.png">

The UI follows the display language of the OS — here it is on a Japanese Windows 11:

<img src="docs/assets/screenshot-ja.png">

ReProgman also runs on macOS:

<img src="docs/assets/screenshot-ja-macos.png">

And on Linux (X11), where the groups are built from the application menu
categories and the icons come from the desktop's icon theme.

## Requirements
- Windows 11 (x64, Arm)
- macOS (Apple Silicon)
- Linux (x64, Arm64) with X11 or XWayland. SVG icon themes need `rsvg-convert` (librsvg)

## Install & Run
Download the executable from Releases and run it.

The macOS build is not notarized, so its extended attributes have to be removed first:

```
xattr -cr ReProgman.app
```

On Linux, unpack the tarball and run `ReProgman` from the extracted folder. Settings
are kept next to the executable, or in `~/.config/ReProgman` when that folder is not
writable.

## Build & Debug

Building and running needs the .NET SDK 10.0+.

```csharp
dotnet run --project src/ReProgman
```

## License

MIT License
