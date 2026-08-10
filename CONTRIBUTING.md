# Contributing

Changes must remain narrowly focused on Jellyfin core UserData protection and must not patch Jellyfin or jellyfin-web. Start from the exact Jellyfin 10.11.11 source baseline documented in `docs/JELLYFIN_RESEARCH.md`.

Before proposing a change:

1. explain the exact core call path and public plugin contract involved;
2. add a result-oriented test, including an API/runtime case for persistence changes;
3. run locked restore, Release build, unit tests, `scripts/verify.sh` and packaging;
4. run `scripts/integration-test.sh` against a fresh Jellyfin 10.11.11 portable installation;
5. update documentation and `CHANGELOG.md` without claiming unexecuted results.

Do not add a runtime dependency when .NET or Jellyfin already supplies the required capability. Never commit real credentials, server data, media libraries or integration-test roots. Do not use `NOSONAR` or broad analysis exclusions.

Code follows the repository `.editorconfig`, Jellyfin ruleset, nullable annotations, warnings-as-errors policy and GPL-3.0-only licence.
