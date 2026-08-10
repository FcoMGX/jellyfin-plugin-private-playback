# Packaging and distribution

## Installable archive

`scripts/package.sh` creates `artifacts/private-playback_0.9.0.0.zip` with exactly:

```text
Jellyfin.Plugin.PrivatePlayback.dll
meta.json
```

No Jellyfin, .NET, analyzer or test binary is bundled. The script normalises file timestamps from `SOURCE_DATE_EPOCH` (default `1786320000`), sorts archive entries and uses `zip -X` to remove extra metadata. Rebuilding from identical source/tool inputs therefore produces byte-identical install/source archives.

The package is installed by extracting those two files directly into a dedicated versioned directory below Jellyfin's data-directory plugin path while the server is stopped.

## Metadata

- `build.yaml` follows the current official Jellyfin plugin-template build descriptor and declares exact ABI `10.11.11.0`, framework `net9.0` and the single DLL artifact.
- `packaging/meta.json` is the local plugin manifest included in the ZIP and was loaded by the real-server test.
- Version `0.9.0`/assembly `0.9.0.0` and GUID `bb23ffd1-026a-4598-8133-e77ae50ccad7` are consistent across project metadata.

No repository `manifest.json` with a downloadable `sourceUrl` is emitted because this work has not been authorized for publication and no stable HTTPS release URL exists. Inventing one would create a non-functional catalog. Once a release is hosted, generate the official `PackageInfo` entry with the real URL, SHA-256, timestamp, repository identity and target ABI, then test installation through Jellyfin's catalog UI.

## Source and verification archives

- `private-playback_0.9.0_source.zip` contains the complete source tree, tests, lock files, workflows and documentation, excluding build output and artifacts.
- `private-playback_0.9.0_verification.zip` contains the final TRX, Cobertura/OpenCover reports and human-readable test result when those files exist.
- `SHA256SUMS` covers every generated ZIP.

## Verification

```bash
cd artifacts
sha256sum --check SHA256SUMS
unzip -l private-playback_0.9.0.0.zip
```

The real-server harness accepts `PRIVATE_PLAYBACK_PLUGIN_ZIP` and validates that both expected files exist after extraction before Jellyfin starts.
