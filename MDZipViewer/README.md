# MDZip Viewer

A standalone Windows viewer for CATIA Magic / Cameo / MagicDraw `.mdzip` project archives.

## Language and platform

- C#
- .NET 8
- Windows Forms
- No third party NuGet packages

The viewer reads the `.mdzip` as a ZIP archive and inspects MagicDraw model XML such as:

- `com.nomagic.magicdraw.uml_model.model`
- `com.nomagic.magicdraw.uml_model.shared_model`
- `.mdxml` and XML model entries

It does not require Enterprise Architect or Cameo to be installed for basic archive inspection.

## Current functionality

- Open one `.mdzip` file
- Inventory all archive entries
- Detect and parse MagicDraw model XML
- List model elements with ID, name, type, owning element, and source document
- Detect likely diagram records
- Parse relationship source and target references
- Preview diagrams visually
- Preserve stored symbol coordinates and dimensions when available
- Preserve stored connector waypoint sequences when available
- Place only unresolved presentation elements in a fallback layout area
- Count packages, elements, relationships, and diagrams
- Export the inventory, including presentation records, to formatted JSON
- Accept an `.mdzip` path as the first command line argument

## Requirements

Install the .NET 8 SDK for Windows.

```powershell
dotnet --version
```

The version should begin with `8.` or newer.

## Build and run

From the repository root:

```powershell
dotnet build .\MDZipViewer\MDZipViewer.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\MDZipViewer\build-and-run.ps1
```

Open a specific project immediately:

```powershell
powershell -ExecutionPolicy Bypass -File .\MDZipViewer\build-and-run.ps1 "C:\Path\Project.mdzip"
```

## Diagram preservation test

1. Start the application and open a non-sensitive `.mdzip`.
2. Open **Diagram Preview** and select several diagrams.
3. Confirm the banner reports **Preserved layout** when stored presentation records are found.
4. Compare symbol positions, relative spacing, sizes, and connector routes with Cameo.
5. Confirm unresolved symbols appear in a separate fallback area instead of replacing preserved positions.
6. Export inventory JSON and confirm it contains `Presentations`.

The reader recognizes multiple presentation encodings used by MagicDraw releases:

- `x`, `y`, `width`, and `height`
- abbreviated `w` and `h`
- `bounds`, `geometry`, `rectangle`, and `rect` strings
- model references including `elementID`, `modelElement`, `representedElement`, and `subject`
- diagram references including `diagramID`, `diagram`, and ancestor diagram ownership
- connector routes in `points`, `path`, `waypoints`, `route`, and edge geometry

The coordinate origin is normalized for display while relative positions, sizes, and stored route shapes are retained. Very large coordinate systems are scaled down uniformly.

## Publish a standalone executable

```powershell
dotnet publish .\MDZipViewer\MDZipViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output:

```text
MDZipViewer\bin\Release\net8.0-windows\win-x64\publish\
```

## Known limitations

- Presentation storage varies by Cameo and MagicDraw version, so a sample project may reveal another mapping that must be added.
- Custom symbol shapes, colors, fonts, compartments, ports, and icons are not yet reproduced exactly.
- Some relationship endpoint formats may still require version-specific mappings.
- Custom profiles and stereotypes are not yet normalized into a dedicated SysML view.
- Teamwork Cloud metadata and external project references are not yet resolved.
