# Verification results

Execution date: 2026-08-10 UTC. Status labels mean exactly **VERIFIED**, **INFERRED** or **NOT VERIFIED**.

## Environment

| Component | Observed value |
| --- | --- |
| OS | Ubuntu 24.04.3 LTS, x86-64 |
| .NET SDK | 9.0.316 |
| .NET runtime used by tests/server | 9.0.18 |
| Jellyfin portable | 10.11.11 / assembly 10.11.11.0 |
| Jellyfin archive SHA-256 | `9f7f194a7e37777cfde0d107c088fc47e81c7904440046ac0ceb7a289546cf79` |
| FFmpeg | 6.1.1 |
| Node.js | 24.14.0 |
| Java | OpenJDK 17.0.19 |
| Docker | unavailable |
| SonarScanner for .NET | 11.2.1 restored and version output observed |

Exact Jellyfin source tag `v10.11.11` resolved to `1fbd8739292cce610231be93daf43368733edf63`.

## Build and unit evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Locked restore | VERIFIED pass | Both project lock files restored from exact versions |
| Debug build | VERIFIED pass | 0 warnings/errors; analyzers and warnings-as-errors active |
| Release build | VERIFIED pass | 0 warnings/errors; `net9.0` plugin DLL produced |
| Unit tests | VERIFIED pass | 42 passed, 0 failed, 0 skipped; VSTest/xUnit |
| Web locale tests | VERIFIED pass | Regional/unknown/empty selection and failed-locale `en-GB` fallback |
| Translation dictionaries | VERIFIED pass | Nine identical non-empty key sets; CJK/Cyrillic resource checks |
| Performance probe | VERIFIED pass | 5,000,000 policy lookups in 200.827 ms (40.2 ns/lookup), 0 thread allocations on this host |

The timing is an environment-specific observation, not a cross-machine SLA.

## Coverage

Coverlet 6.0.4 generated Cobertura and OpenCover reports from the final 42-test run.

| Scope | Line coverage | Branch coverage |
| --- | ---: | ---: |
| Production assembly overall | 77.15% (304/394) | 78.47% (113/144) |
| Critical `PolicyUserDataManager` | 88.73% | 92.64% |
| `ConfigurationValidator` | 96.87% | 92.30% |
| Administrative controller | 100% | 100% |
| Policy model/registry | 100% | 100% |

`PlaybackDataMaintenance` is exercised by the real-server integration suite but appears as 0% in unit-process coverage because no runtime coverage was attached to the external Jellyfin process. Wiring paths also retain lower unit coverage; exact activation is verified by real plugin load/status.

Reports:

- `artifacts/test-results/final/unit-tests-final.trx`
- `artifacts/test-results/final/c142d401-30b8-4385-bdc4-9b45d619489a/coverage.cobertura.xml`
- `artifacts/test-results/final/c142d401-30b8-4385-bdc4-9b45d619489a/coverage.opencover.xml`

## Real Jellyfin 10.11.11 integration

Result: **VERIFIED pass**. The final expanded run installed the generated two-file ZIP into a fresh temporary server tree and ended with `All Private Playback integration tests passed.` Preserved root: `/tmp/tmp.7YUqPJ8cTu` (local execution evidence, not distributed server data).

| Runtime case | Result |
| --- | --- |
| Exact server and plugin package load/status | VERIFIED pass |
| Generated six-minute movie and two-episode TV library scan | VERIFIED pass |
| Non-admin access to custom administrative API | VERIFIED forbidden (403) |
| Dashboard configuration resource served | VERIFIED pass |
| Generic UserData bypass attempt | VERIFIED filtered |
| Manual watched endpoint | VERIFIED filtered for private, persisted for normal |
| Eight parallel watched requests over two private sessions | VERIFIED filtered |
| Two simultaneous private playback sessions | VERIFIED baseline unchanged |
| Private automatic-completion threshold | VERIFIED watched state/history/progress unchanged |
| Normal partial playback | VERIFIED progress/history persisted and Continue Watching changed |
| Private Continue Watching | VERIFIED unchanged; explicit cleanup removed old resume entry |
| Private completed episode | VERIFIED episode, season, series and Next Up unchanged |
| Normal episode watched | VERIFIED aggregates changed and Next Up advanced to episode 2 |
| Existing-data preview/clear | VERIFIED count, unrelated fields, idempotence |
| Restart and new authentication sessions | VERIFIED private and normal states persisted as expected |
| Corrupt XML | VERIFIED normal-behaviour fallback and error log |
| Valid XML restoration | VERIFIED protection resumed without reversing fail-safe writes |
| Uninstall/restart | VERIFIED plugin absent; core UserData and normal-user data operational |

The first shutdown occurred while library work was still active and used the harness's bounded SIGKILL fallback after SIGTERM, exercising abrupt restart. Later shutdown logs show core disposal and restart persistence; a separate stabilized disposable server exited cleanly 200 ms after SIGTERM. The plugin itself has no shutdown queue.

The harness reports session events to the real server and verifies API/persisted state. It does not stream every media delivery variant or drive a physical client UI.

## Static, security and supply-chain checks

| Check | Result | Limit |
| --- | --- | --- |
| JSON parsing with `jq` | VERIFIED pass | Repository JSON files outside build output |
| Shell syntax (`bash -n`) | VERIFIED pass | All repository shell scripts |
| JavaScript module syntax (`node --check`) | VERIFIED pass | Embedded dashboard module |
| Unsafe DOM assertions | VERIFIED pass | No `innerHTML` or `eval`; names use `textContent` |
| High-confidence secret-pattern scan | VERIFIED pass | Custom regex, not a full gitleaks/trufflehog history scan |
| Forbidden Sonar suppression scan | VERIFIED pass | No `NOSONAR` or analysis exclusions |
| `dotnet format whitespace --folder` | VERIFIED pass | Whitespace only |
| Solution-mode `dotnet format` | NOT VERIFIED | Roslyn build-host named pipe failed with sandbox `SocketException (13): Permission denied` |
| NuGet vulnerable-package audit | VERIFIED: zero reported | Official `dotnet list ... --vulnerable --include-transitive`, nuget.org source, execution-time snapshot |
| SonarQube server analysis/Quality Gate | NOT VERIFIED | No server URL/token/Docker; Java 17 below current Java 21 requirement |

No production runtime package beyond the exact Jellyfin contracts is included in the ZIP. Test/analyzer/tool dependencies are locked and not redistributed.

## Packaging

The installable archive contains only `Jellyfin.Plugin.PrivatePlayback.dll` and `meta.json`. The source and verification archives were also generated, and `SHA256SUMS` covers all three. Two consecutive executions with the fixed `SOURCE_DATE_EPOCH` produced identical installable and source ZIP hashes. The final installable ZIP is tested once more in the real-server smoke matrix before delivery.

## Explicitly not verified

- visual rendering or interaction in a real jellyfin-web browser session;
- physical Android/TV/iOS/webOS clients;
- actual Direct Stream, transcode, hardware acceleration and subtitle-format matrix;
- runtime co-installation with Playback Reporting, Webhook or Trakt;
- any Jellyfin server version other than 10.11.11;
- a SonarQube Quality Gate.

These gaps are why the release remains `0.9.0` beta rather than 1.0.0.
