# Changelog

## [1.0.0] - 2026-07-03
### Added
- Initial release of the Unity package.
- Added core functionality for Structure of Arrays code generation:
  - Native Container generation for safe context. 
  - Nested struct flattening to remove any memory paddings providing zero wasted memory.
  - Generation of accessor methods to retrieve flattened entries.
  - Boolean field bit-packing to reduce memory consumption up to 87,5%.
- Included documentation and example usage.