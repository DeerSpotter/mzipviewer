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
- Count packages, elements, relationships, and diagrams
- Export the inventory to formatted JSON
- Accept an `.mdzip` path as the first command line argument

This is the first standalone viewer milestone. Exact graphical reconstruction of Cameo diagrams is not included yet.

## Requirements

Install the .NET 8 SDK for Windows.

Confirm it is available:

```powershell
dotnet --version
```

The version should begin with `8.` or newer.

## Build

From the repository root:

```powershell
dotnet build .\MDZipViewer\MDZipViewer.csproj -c Release
```

## Run

```powershell
dotnet run --project .\MDZipViewer\MDZipViewer.csproj -c Release
```

Or use the included launcher:

```powershell
powershell -ExecutionPolicy Bypass -File .\MDZipViewer\build-and-run.ps1
```

Open a specific project immediately:

```powershell
powershell -ExecutionPolicy Bypass -File .\MDZipViewer\build-and-run.ps1 "C:\Path\Project.mdzip"
```

## Manual test procedure

1. Start the application.
2. Select **Open .mdzip**.
3. Choose a non-sensitive test project.
4. Confirm the summary displays package, element, relationship, and diagram counts.
5. Open the **Elements** tab and verify named model elements appear.
6. Open the **Model documents** tab and confirm the primary MagicDraw model entry is listed.
7. Open the **Archive entries** tab and confirm the complete archive contents are shown.
8. Select **Export inventory JSON**.
9. Open the exported JSON and confirm it contains `Documents`, `Elements`, and `Diagrams` collections.

## Publish a standalone executable

```powershell
dotnet publish .\MDZipViewer\MDZipViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output is written under:

```text
MDZipViewer\bin\Release\net8.0-windows\win-x64\publish\
```

## Known limitations

- Diagram detection is currently heuristic.
- Diagram canvas positions and connector routing are not reconstructed.
- Custom profiles and stereotypes are listed as model elements but are not yet normalized into a dedicated SysML view.
- Teamwork Cloud metadata is not resolved.
- Cross-project references are not yet followed into external `.mdzip` files.
