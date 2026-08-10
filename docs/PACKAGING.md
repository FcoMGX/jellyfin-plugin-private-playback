# Packaging and distribution

## Installable archive

`scripts/package.sh` creates `artifacts/private-playback_0.9.0.0.zip` with exactly:

```text
Jellyfin.Plugin.PrivatePlayback.dll
meta.json
```

No Jellyfin, .NET, analyzer or test binary is bundled. The packaging script normalises file timestamps from `SOURCE_DATE_EPOCH` (default `1786320000`), sorts archive entries and uses `zip -X` to remove extra ZIP metadata.

For byte-for-byte reproducibility, compare builds made from the **same source revision and tool inputs**. With .NET 8 and later, Source Link appends the Git `SourceRevisionId` to `AssemblyInformationalVersion` by default. Therefore a DLL built from a later commit can differ even when that later commit changes only repository metadata or documentation. Release verification must compare the published binary with the CI artifact produced from the **release tag commit**, not with an arbitrary later `main` build.

The package is installed by Jellyfin from the repository manifest or, as a fallback, by extracting the two files into a dedicated versioned plugin directory while the server is stopped.

## Metadata

- `build.yaml` follows the Jellyfin plugin build descriptor and declares ABI `10.11.11.0`, framework `net9.0` and the single DLL artifact.
- `packaging/meta.json` is included in the installable ZIP and identifies the plugin as version `0.9.0.0`, GUID `bb23ffd1-026a-4598-8133-e77ae50ccad7`, owner `FcoMGX` and target ABI `10.11.11.0`.
- The root `manifest.json` is the public third-party Jellyfin repository manifest.
- The public repository URL is:

```text
https://raw.githubusercontent.com/FcoMGX/jellyfin-plugin-private-playback/main/manifest.json
```

- Version `0.9.0.0` points to the installable ZIP attached to GitHub pre-release `v0.9.0`.
- Jellyfin Server 10.11.11 validates the repository manifest's `checksum` field using **MD5** when installing the ZIP. This MD5 is required for Jellyfin's plugin repository protocol; it is not presented as a modern security hash.
- `SHA256SUMS` is published separately for stronger user/operator integrity verification of the release assets.

The release remains unofficial and beta, and compatibility is claimed only for Jellyfin Server 10.11.11.

## Source and verification archives

- `private-playback_0.9.0_source.zip` contains the complete source tree from the CI build revision, excluding build output, Git internals and generated artifacts.
- `private-playback_0.9.0_verification.zip` is generated when the expected final TRX and coverage files are present.
- `SHA256SUMS` covers the generated ZIP files included by the packaging run.

GitHub also provides its own automatically generated source archives for the release tag; those are separate from the project's CI-generated source ZIP.

## Verification

For a downloaded release:

```bash
sha256sum --check SHA256SUMS
unzip -l private-playback_0.9.0.0.zip
```

For release provenance, compare `private-playback_0.9.0.0.zip` with the successful GitHub Actions CI artifact produced from the commit referenced by tag `v0.9.0`.

Do **not** require a later `main` build to be byte-identical to an older tagged build merely because no C# file changed: the .NET SDK can embed the Git source revision in assembly informational metadata.

The real-server harness accepts `PRIVATE_PLAYBACK_PLUGIN_ZIP` and validates that both expected package files exist after extraction before Jellyfin starts.
