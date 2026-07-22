# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-07-18

### Changed

- Raised `com.saesentsessis.unity-collections-specialized` dependency from 0.1.1 to 0.2.0.

## [1.0.0] - 2026-07-03

### Added

- Initial release of the Unity package.
- Added core functionality for Structure of Arrays code generation:
  - Native Container generation for safe context. 
  - Nested struct flattening to remove any memory paddings providing zero wasted memory.
  - Generation of accessor methods to retrieve flattened entries.
  - Boolean field bit-packing to reduce memory consumption up to 87,5%.
- Included documentation and example usage.
- Editor window to preview generated SoA information.

[1.0.1]: https://github.com/Saesentsessis/Unity-SoA-Generator/compare/1.0.0...1.0.1
[1.0.0]: https://github.com/Saesentsessis/Unity-SoA-Generator/releases/tag/1.0.0