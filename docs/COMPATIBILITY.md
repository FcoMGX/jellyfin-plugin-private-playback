# Compatibility matrix

| Component/client | Status | Evidence or boundary |
| --- | --- | --- |
| Jellyfin Server 10.11.11 / ABI 10.11.11.0 | Verified | Official portable server, five real boots, ZIP install, API persistence/restart/uninstall matrix |
| Jellyfin Server earlier/later versions | Unsupported | Exact runtime guard refuses decoration; no compatibility claim |
| .NET target/runtime | Verified for net9.0 / server runtime 9.0.18 | Debug/Release builds and real server execution |
| Jellyfin Web configuration resource | Partially verified | Resource served through official dashboard endpoint; JS syntax/locale tests pass; no visual browser session was executed |
| Jellyfin playback APIs | Verified | Start, progress, stop, generic UserData, watched endpoint and API reads |
| Movies | Verified | Partial progress, automatic completion threshold, manual watched, Continue Watching |
| Episodes, seasons and series | Verified | Completed private episode, normal watched episode, aggregates and Next Up |
| Two concurrent private sessions | Verified | Simultaneous progress/stop against one item |
| Repeated manual API changes | Verified | Eight parallel watched requests across two authenticated sessions |
| Restart persistence | Verified | Private/normal movie and episode states queried after restart |
| Corrupt configuration | Verified | Normal-behaviour fallback and technical log, followed by recovery |
| Installable ZIP | Verified | Exact two-file package extracted and loaded by Jellyfin |
| Uninstall | Verified | Binary directory moved out, server restarted, normal core UserData persisted |
| Android, Android TV, iOS, tvOS, webOS clients | Inferred server enforcement only | They are not physically tested; direct API bypasses are covered server-side |
| Direct Stream/transcoding/hardware acceleration/subtitle formats | Not verified end to end | The decorator does not alter media delivery, but these combinations were not streamed in the harness |
| Playback Reporting/Webhook/Trakt co-installation | Source-researched, not runtime co-installed | Independent events/stores documented; no compatibility success claimed |
| SonarQube Quality Gate | Not verified | No authorized server/credentials/Java 21; workflow prepared |

Only the first row is a declared Jellyfin server compatibility target.
