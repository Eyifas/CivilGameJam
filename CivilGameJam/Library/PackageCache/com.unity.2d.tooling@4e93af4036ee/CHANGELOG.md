# Changelog

## [4.0.1] - 2026-08-25
### Fixed
- Improved report generation speed.
- Inform user if there are reports that are still generating when 'Analyze' button is clicked.
- Fixed warning when analyzing during play mode. (UUM-141203)

## [4.0.0] - 2026-05-19
### Fixed
- Fixed sprite atlas variant is not reporting correctly in Sprite Atlas report. Sprite atlas variant will now show as a child node of the main atlas. (DANB-1065)
- Fixed sprites does not show which page they are in All Sprite Atlas report.
- Fixed report and sample when sprites are packed in multiple atlases. (UUM-140052)

## [3.0.0] - 2026-01-20

### Changed
- Update Unity min version for package.

### Added
- Columns for all Sprite Atlas reports can be sorted.

### Fixed
- Fixed secondary texture in Sprite Atlas mistaken as atlas with more than 1 texture. (DANB-1210)

## [2.0.0] - 2025-09-30
### Changed
- Update Unity min version for package.

## [1.0.0] - 2025-09-25
### Fixed
- Fixed All Sprite Atlas report not showing correct total memory size. (DANB-1066)
- Fixed All Sprite Atlas reporting columns data not showing sensible data.
- Fixed redundant revert button on report settings.
- Fixed obsolete usage of API.

## [1.0.0-pre.1] - 2025-07-17
### Added
- New package that provides Sprite Atlas analyzing in Unity Editor.
