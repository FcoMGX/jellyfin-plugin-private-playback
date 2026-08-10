# Private Playback for Jellyfin

Private Playback is an independent, unofficial Jellyfin plugin that lets an administrator decide, per user, whether Jellyfin may persist resume progress, watched state and playback history.

Version `0.9.0` is a beta built and runtime-tested exclusively for Jellyfin Server `10.11.11` (`10.11.11.0` ABI, .NET 9). It deliberately refuses to install its enforcement layer on any other server version or unexpected `IUserDataManager` registration shape.

## What it does

The **Fully private** preset preserves the user's previously committed values whenever playback or an API request tries to change:

| User-facing concept | Jellyfin 10.11.11 fields protected |
| --- | --- |
| Resume progress and Continue Watching | `PlaybackPositionTicks` |
| Watched/unwatched state | `Played` |
| Playback history | `PlayCount`, `LastPlayedDate` |

The policy is enforced below Jellyfin's controllers at the server-side `IUserDataManager` boundary. It covers playback start, progress and stop, the watched/unwatched endpoints, legacy routes, folder recursion and the generic UserData endpoint. Sanitisation happens before the core database transaction; the plugin does not save and erase afterwards.

Normal users use ordinary Jellyfin behaviour. A custom policy may independently allow or protect the three concepts above. Play count and last-played date remain one option because separating them would create incoherent history.

Enabling protection never deletes prior data. An elevated, separately confirmed cleanup action can preview and clear existing playback fields for one user while retaining favourites, ratings, audio/subtitle choices, permissions and account data.

## Privacy boundary

Private Playback controls only Jellyfin core UserData fields listed above. It does not disable or remove authentication, security, audit, activity or diagnostic logs. It stores no playback database, item IDs, titles, positions or session timestamps of its own.

Favourites, ratings, audio selection and subtitle selection remain writable. Passwords, permissions, devices, access controls, UI preferences and security data are outside the plugin's write path.

Playback Reporting, Webhook, Trakt and similar plugins can consume session events independently and may store or transmit their own playback records. Private Playback neither suppresses those events nor edits another plugin's data. Configure exclusions in each reporting/scrobbling plugin when its own privacy model permits it.

Jellyfin 10.11.11 has no supported plugin hook to hide the watched button per user in every client. The button can remain visible and a client can briefly show an optimistic toggle. The server response and later refresh reflect the unchanged persisted state. No jellyfin-web patch or client injection is used.

## Requirements

- Jellyfin Server exactly `10.11.11`.
- The official server's .NET 9 runtime.
- Administrative access to install and configure plugins.
- A backup appropriate to your Jellyfin installation before installing any beta plugin.

No outbound service, external database or runtime dependency is added by the installable ZIP.

## Installation

1. Verify the ZIP against `SHA256SUMS`.
2. Stop Jellyfin.
3. Create a dedicated directory below Jellyfin's data-directory plugin path, for example `plugins/Private Playback_0.9.0.0`.
4. Extract `private-playback_0.9.0.0.zip` into that directory. The DLL and `meta.json` must be directly inside it.
5. Start Jellyfin.
6. Open **Dashboard → Plugins → Private Playback**.
7. Confirm that **Protection is active** and the detected server version is `10.11.11.0` before assigning any private policy.

If the status is inactive, the plugin intentionally leaves every user on normal Jellyfin behaviour. Read the displayed reason and the Jellyfin log; do not assume protection.

## Configuration

The page discovers current Jellyfin users dynamically and stores the immutable user ID. Renaming an account therefore keeps its policy. Deleted users appear as stale entries; select **Normal Jellyfin behaviour** and save to remove one.

For a guest account:

1. Create the user in **Dashboard → Users** and grant only the required libraries.
2. Open **Dashboard → Plugins → Private Playback**.
3. Find the guest by name.
4. Select **Fully private**.
5. Save and verify that protection remains active.

Custom mode provides:

- **Remember playback progress** — permits the resume position and Continue Watching to change.
- **Remember watched state** — permits automatic and manual played/unplayed changes.
- **Record playback history** — permits play count and last-played date to change.

Policy changes affect subsequent UserData operations. Playback data already committed before the change is retained until the administrator explicitly uses cleanup.

### Existing-data cleanup

For the chosen user, select **Preview affected items**, inspect the count, then choose **Clear playback data** and confirm. Cleanup sets position/count to zero, last-played date to null and watched to false for playable video, audio and book items. It is idempotent and preserves favourite, rating and stream-selection fields.

This operation cannot be undone. It never runs merely because a private policy is enabled.

## Internationalisation and accessibility

The dashboard page includes complete UTF-8 dictionaries for `en-GB`, `es-ES`, `pt-PT`, `fr-FR`, `it-IT`, `zh-TW`, `ja-JP`, `ru-RU` and `ko-KR`. It uses the locale reflected by jellyfin-web on the document, normalises language variants and always falls back to `en-GB` for unknown, empty or failed resources.

Standard Jellyfin controls, associated labels, keyboard-operable buttons, status roles and DOM `textContent` are used. User names are never inserted as HTML.

## Upgrade

This beta has configuration schema version `1`. To upgrade a future release:

1. Stop Jellyfin.
2. Back up the existing plugin configuration and Jellyfin data according to your normal process.
3. Extract the new release into its own versioned plugin directory.
4. Remove or move the old plugin binary directory out of Jellyfin's plugin path.
5. Start Jellyfin and verify the enforcement status before relying on it.

Unknown future configuration schemas fail safely to normal behaviour; valid configuration is never silently downgraded.

## Uninstall

1. Stop Jellyfin.
2. Remove the plugin's versioned binary directory from Jellyfin's plugin path.
3. Start Jellyfin and verify normal playback and UserData behaviour.
4. Optionally archive or remove `Jellyfin.Plugin.PrivatePlayback.xml` from the plugin-configurations directory while Jellyfin is stopped.

Uninstall does not restore data previously cleared by an explicit cleanup and does not delete any data automatically. Jellyfin has no schema dependency on the plugin.

## Build

The repository pins .NET SDK `9.0.316`, Jellyfin packages `10.11.11`, uses lock files, nullable references, deterministic compilation and warnings as errors.

```bash
dotnet restore PrivatePlayback.sln --locked-mode
dotnet build PrivatePlayback.sln --configuration Release --no-restore
dotnet test PrivatePlayback.sln --configuration Release --no-build \
  --collect "XPlat Code Coverage" \
  --settings tests/coverage.runsettings \
  --results-directory artifacts/test-results
./scripts/verify.sh
./scripts/package.sh
```

To run the isolated real-server matrix, extract the official Jellyfin `10.11.11` portable server and run:

```bash
JELLYFIN_BIN_DIR=/path/to/jellyfin \
PRIVATE_PLAYBACK_PLUGIN_ZIP="$PWD/artifacts/private-playback_0.9.0.0.zip" \
./scripts/integration-test.sh
```

The script creates a temporary server/data tree, generated six-minute media, normal/private users and five server boots. It never targets an existing Jellyfin installation and prints the preserved test root for inspection.

## Quality and SonarQube

`scripts/verify.sh` validates JSON, shell and JavaScript syntax, executes web-locale fallback tests, checks unsafe DOM APIs, and scans high-confidence secret patterns. The build enables the .NET SDK analyzers plus Jellyfin's ruleset, StyleCop, Serilog and multithreading analyzers.

`.github/workflows/sonarqube.yml` pins SonarScanner for .NET `11.2.1`, Java 21 and imports OpenCover results. It requires `SONAR_HOST_URL` and `SONAR_TOKEN` repository secrets. A Quality Gate is not claimed unless that workflow completes against a configured server. See `docs/SONARQUBE.md` and `docs/TEST_RESULTS.md` for the actual local result.

## Troubleshooting

**Protection is inactive**  
Verify the server is exactly `10.11.11.0` and that no other plugin has replaced the core `IUserDataManager` registration. The safe fallback is ordinary Jellyfin persistence.

**The watched icon still exists**  
This is expected. There is no supported cross-client button-removal extension. Test the final state through the API or after a refresh/relogin.

**Old progress remains after enabling Fully private**  
Protection preserves pre-existing data by design. Use the separately confirmed cleanup action if permanent removal is intended.

**A reporting or scrobbling plugin still contains history**  
That plugin owns an independent store/event flow. Configure it separately; Private Playback will not tamper with it.

**Configuration is corrupt or from a future schema**  
The plugin logs one technical error and publishes an empty policy snapshot, leaving every user on normal Jellyfin behaviour. Restore a known-good file while Jellyfin is stopped.

## Verified scope and limitations

The included report records actual builds, 42 unit tests, coverage, API/runtime integration, restart, corrupt-config recovery, ZIP installation and uninstall against Jellyfin `10.11.11`. No other Jellyfin version or physical client application is claimed compatible.

The test harness drives the same server APIs used by clients but does not certify every client UI, Direct Stream/transcoding combination, hardware acceleration path, subtitle format or third-party plugin deployment. The enforcement layer does not modify those pipelines; these remain explicit beta validation gaps.

## Documentation

- `docs/ARCHITECTURE.md` — implementation and concurrency model.
- `docs/JELLYFIN_RESEARCH.md` — exact-source call tracing and evidence.
- `docs/DESIGN.md` — requirements, rejected alternatives and threat model.
- `docs/MANUAL_TEST_PLAN.md` — reproducible administrator verification.
- `docs/SONARQUBE.md` — current tooling and honest execution status.
- `docs/TEST_RESULTS.md` — observed build, test, coverage and audit evidence.
- `docs/README.es-ES.md` — operational guide in Spanish (Spain).

## Licence

The project is licensed under `GPL-3.0-only`. Jellyfin.Controller and Jellyfin.Model `10.11.11` declare the same NuGet licence expression. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.
