# MDZip Viewer

A standalone Windows viewer for CATIA Magic / Cameo / MagicDraw `.mdzip` project archives.

## Language and platform

- C#
- .NET 8
- Windows Forms
- No third party NuGet packages

The viewer reads the `.mdzip` as a ZIP archive and processes MagicDraw model content such as:

- `com.nomagic.magicdraw.uml_model.model`
- `com.nomagic.magicdraw.uml_model.shared_model`
- `ownedDiagram` records
- diagram payloads referenced by `binaryObject streamContentID`

It does not require Enterprise Architect or Cameo to be installed for archive inspection.

## Current functionality

- Open one `.mdzip` file
- Inventory all archive entries
- Parse UML and SysML model elements and relationships
- Read real MagicDraw `ownedDiagram` records
- Resolve each diagram's referenced `BINARY-*` payload
- Parse `mdElement`, `elementID`, `elementClass`, and `geometry`
- Preserve stored symbol coordinates and dimensions
- Preserve stored connector waypoint sequences when represented in geometry
- Render notes and split presentation elements
- Place only unresolved elements in a fallback area
- Export model and presentation records to formatted JSON

Diagram IDs follow the same convention used by the Enterprise Architect MagicDraw migrator: `ownerOfDiagram + separator + diagram name`.

MagicDraw diagram geometry is interpreted as:

```text
x,y,right,bottom
```

The viewer converts this to display width and height using:

```text
width  = right - x
height = bottom - y
```

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

## Diagram processing test

1. Start the application and open a non-sensitive `.mdzip`.
2. Open **Diagram Preview** and select several diagrams.
3. Confirm the diagram selector lists names from actual `ownedDiagram` records.
4. Confirm the status reports positioned symbols when the referenced binary payload was parsed.
5. Compare symbol positions, relative spacing, dimensions, and connector paths with Cameo.
6. Export inventory JSON and inspect `StreamContentId` and `Presentations`.
7. Confirm presentation records reference the actual binary archive entry.

## Reference implementations

The real diagram-processing path is based on the open-source implementations in:

- `GeertBellekens/Enterprise-Architect-Toolpack`, particularly `MagicdrawMigrator/MagicDrawReader.cs`, `MDDiagram.cs`, and `MDDiagramObject.cs`
- `JPLOpenSource/SCA`, particularly its `MagicDrawReader.java` archive and XMI handling

## Publish a standalone executable

```powershell
dotnet publish .\MDZipViewer\MDZipViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output:

```text
MDZipViewer\bin\Release\net8.0-windows\win-x64\publish\
```

## Known limitations

- Custom Cameo symbol shapes, colors, fonts, compartments, ports, and icons are not yet reproduced exactly.
- Some edge geometries may use version-specific encodings beyond semicolon-separated waypoints.
- Custom profiles and stereotypes are not yet normalized into a dedicated SysML view.
- Teamwork Cloud metadata and external project references are not yet resolved.
