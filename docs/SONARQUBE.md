# SonarQube analysis

## Status on 2026-08-10

**NOT EXECUTED against a SonarQube server. No Quality Gate result exists.**

The official SonarScanner for .NET `11.2.1` was restored from NuGet and invoked locally; it identified itself as `SonarScanner for .NET 11.2.1`. The environment did not provide `SONAR_HOST_URL`, `SONAR_TOKEN`, Docker or another reachable SonarQube service. Its installed Java runtime was OpenJDK 17.0.19, while current SonarQube Server 2026.4 requires Java 21 (or 25 for a ZIP server installation). Creating or using an external analysis service without credentials/authorization would have exceeded the task's authority.

Consequently, this report does not claim zero Sonar Bugs, Vulnerabilities, Hotspots, Code Smells, duplication, or a passed Quality Gate. Local compiler/analyzer and security checks below are real but are not a substitute for a server analysis.

## Prepared reproducible analysis

`.config/dotnet-tools.json` pins the official scanner to `11.2.1`. `.github/workflows/sonarqube.yml` pins .NET SDK `9.0.316`, Temurin Java 21, a full Git checkout and these steps:

```bash
dotnet restore PrivatePlayback.sln --locked-mode
dotnet tool restore
dotnet tool run dotnet-sonarscanner begin \
  /k:"private-playback" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.qualitygate.wait=true \
  /d:sonar.cs.vstest.reportsPaths="artifacts/test-results/*.trx" \
  /d:sonar.cs.opencover.reportsPaths="artifacts/test-results/**/coverage.opencover.xml"
dotnet build PrivatePlayback.sln --configuration Release --no-incremental --no-restore
dotnet test PrivatePlayback.sln --configuration Release --no-build \
  --collect "XPlat Code Coverage" \
  --settings tests/coverage.runsettings \
  --results-directory artifacts/test-results \
  --logger "trx;LogFileName=unit-tests.trx"
dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

Secrets are supplied only through repository secrets and are never committed or printed deliberately.

## Languages in the repository

| Language/content | Files | Intended analysis |
| --- | --- | --- |
| C# | plugin and xUnit projects | Sonar C# analyzer via build integration |
| JavaScript | dashboard module and Node tests | Sonar JS analyzer |
| HTML | embedded dashboard page | Sonar HTML analyzer |
| JSON | localisation, metadata, tool manifest, lock files | Sonar JSON analysis where supported; syntax also checked with `jq` |
| YAML | `build.yaml`, GitHub Actions | Sonar YAML analysis where supported |
| XML/MSBuild | projects, props, ruleset, runsettings | Sonar XML analysis where supported |
| Shell | build/package/integration/verification scripts | shell syntax and focused local review; not represented as a claimed Sonar language result |
| Markdown | documentation | documentation review; no claimed Sonar rule result |

Current SonarScanner for .NET multi-language scanning is left at its supported default. No `sonar.*.exclusions`, `NOSONAR`, blanket suppression or fabricated issue baseline is present.

## Profiles and rules

The actual Quality Profile and active rule catalogue are server-side properties, so they cannot truthfully be enumerated without the configured server. The prepared analysis is intended to use the server's current built-in recommended profiles for C#, JavaScript/HTML, JSON, YAML and XML, covering Reliability, Security, Maintainability and Security Hotspots. Before publishing a release, a maintainer must record the server version, analyzer versions, Quality Profile names/revisions, Gate conditions and every issue/hotspot disposition from the completed run.

One local suppression exists in test-only code: CA1852 on the `DispatchProxy` base. It is scoped to one class and justified because .NET dynamically subclasses that type and rejects it when sealed. There are no Sonar suppressions.

## Real local checks

- Debug and Release builds completed with zero compiler/analyzer warnings and `TreatWarningsAsErrors=true`.
- Nullable reference analysis and `AnalysisMode=AllEnabledByDefault` are active.
- Jellyfin's ruleset, StyleCop.Analyzers 1.2.0-beta.556, SerilogAnalyzer 0.15.0 and SmartAnalyzers.MultithreadingAnalyzer 1.1.31 ran during build.
- `dotnet format whitespace --folder . --verify-no-changes` passed.
- Solution-mode `dotnet format` could not run because this sandbox denies the named-pipe socket used by Roslyn's build host; the exact exception is recorded in `docs/TEST_RESULTS.md`.
- 42 unit tests passed; OpenCover/Cobertura measured 77.15% line and 78.47% branch coverage globally. The critical `PolicyUserDataManager` measured 88.73% line and 92.64% branch coverage.
- The official NuGet CLI audit reported no vulnerable direct or transitive packages for either solution project on the configured nuget.org source at execution time.
- Focused secret-pattern, unsafe-DOM, JSON, shell and JavaScript checks passed.

## Required release follow-up

Run the workflow against an authorized SonarQube 2026.4-compatible service, review every hotspot, correct all actionable findings, and attach the server-generated report/Gate result. Until then, Sonar status remains **NOT VERIFIED** rather than passed or failed.

Official references:

- <https://docs.sonarsource.com/sonarqube-server/2026.4/analyzing-source-code/dotnet-environments/getting-started-with-net>
- <https://docs.sonarsource.com/sonarqube-server/2026.4/analyzing-source-code/scanners/scanner-environment/general-requirements>
- <https://docs.sonarsource.com/sonarqube-server/2026.4/server-installation/server-host-requirements>
- <https://github.com/SonarSource/sonar-scanner-msbuild/releases/tag/11.2.1.137242>
- <https://www.nuget.org/packages/dotnet-sonarscanner/11.2.1>
