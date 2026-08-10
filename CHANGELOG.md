# Changelog

All notable changes follow Semantic Versioning. Dates use ISO 8601.

## [0.9.0] - 2026-08-10

Initial beta targeting Jellyfin Server 10.11.11 only.

- Added per-user normal, fully private and custom policies.
- Added pre-transaction server-side protection for resume position, watched state, play count and last-played date.
- Added enforcement for playback and direct/manual UserData API paths.
- Added explicit previewed, confirmed and idempotent cleanup of existing playback data.
- Added dashboard configuration and nine localisations with `en-GB` fallback.
- Added exact-version/descriptor activation guard and normal-behaviour fail-safe.
- Added unit, concurrency, performance and real Jellyfin integration tests.
- Added deterministic install/source packaging, checksums and CI workflows.
