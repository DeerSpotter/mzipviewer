# Ported MagicDraw parser

The standalone parser in `Services/PortedMagicDrawParser.cs` is derived from the parsing approach in:

- `GeertBellekens/Enterprise-Architect-Toolpack/MagicdrawMigrator/MagicDrawReader.cs`
- `GeertBellekens/Enterprise-Architect-Toolpack/MagicdrawMigrator/UML/MDDiagram.cs`
- `GeertBellekens/Enterprise-Architect-Toolpack/MagicdrawMigrator/UML/MDDiagramObject.cs`
- `JPLOpenSource/SCA/.../MagicDrawReader.java` for packed-project and XMI compatibility concepts

The port intentionally removes Enterprise Architect framework dependencies. It writes into MDZip Viewer records instead of creating Sparx Enterprise Architect objects.

## Ported processing areas

- Native `.mdzip` ZIP traversal
- Primary and shared MagicDraw model documents
- UML elements and common relationships
- Constraints and opaque specifications
- Lifelines and represented properties/types
- Messages, send/receive events, and covered lifelines
- `ownedDiagram` records
- `binaryObject streamContentID` resolution
- `BINARY-*` diagram payload processing
- `mdElement`, `elementID`, `elementClass`, and `geometry`
- Notes and linked model elements
- Control-flow guard text and endpoint resolution
- MagicDraw `x,y,right,bottom` geometry conversion
- Foreign `href#id` and local `xmi:idref` normalization

## Deliberately not copied

Enterprise Architect-specific model creation, logging, add-in UI, correction passes, COM wrappers, and EA database operations are excluded. Those outputs are replaced with viewer-native immutable records.

## Next porting targets

- Combined fragments and owned splits
- Partitions and activity object states
- Association-table and special ASMA mappings
- Cross-MDZIP shared-model resolution
- More MagicDraw version-specific relationship endpoint structures
