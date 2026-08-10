# Dependency and supply-chain audit

Execution date: 2026-08-10 UTC.

## Production project

| Direct package | Version | Role | Distribution |
| --- | --- | --- | --- |
| Jellyfin.Controller | 10.11.11 | Public server/plugin contracts | Runtime assets excluded; supplied by server |
| Jellyfin.Model | 10.11.11 | Public models/configuration contracts | Runtime assets excluded; supplied by server |
| SerilogAnalyzer | 0.15.0 | Build-time logging analyzer | Private build asset only |
| SmartAnalyzers.MultithreadingAnalyzer | 1.1.31 | Build-time concurrency analyzer | Private build asset only |
| StyleCop.Analyzers | 1.2.0-beta.556 | Build-time style analyzer | Private build asset only |

There is no production NuGet library copied into the installable output. Transitive Jellyfin compile contracts resolve from exact 10.11.11 packages and are recorded with SHA-512 content hashes in `src/Jellyfin.Plugin.PrivatePlayback/packages.lock.json`.

## Test/tool project

| Direct package/tool | Version | Role |
| --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.14.1 | VSTest host |
| xunit | 2.9.3 | Unit assertions/runner core |
| xunit.runner.visualstudio | 3.1.5 | VSTest adapter |
| coverlet.collector | 6.0.4 | Cobertura/OpenCover collection |
| dotnet-sonarscanner | 11.2.1 | Optional official Sonar build scanner |

These are excluded from the plugin ZIP. Test package hashes and complete transitives are locked in `tests/Jellyfin.Plugin.PrivatePlayback.Tests/packages.lock.json`; the tool version is pinned in `.config/dotnet-tools.json`.

## Provenance/licensing checks

- Jellyfin package nuspecs identify Jellyfin Contributors, the exact official repository commit and `GPL-3.0-only`.
- Microsoft.NET.Test.Sdk identifies Microsoft/vstest and MIT.
- xUnit packages identify the xUnit upstream repository and Apache-2.0.
- coverlet identifies the coverlet upstream repository and MIT.
- StyleCop identifies DotNetAnalyzers/StyleCopAnalyzers and MIT.
- The Serilog and multithreading analyzers identify their upstream repositories/authors in package metadata; they are analyzers only and not redistributed.
- SonarScanner is the SonarSource-owned NuGet tool and is not bundled.

## Vulnerability query

Executed command:

```bash
dotnet list PrivatePlayback.sln package \
  --vulnerable \
  --include-transitive \
  --source https://www.nuget.org/api/v2/ \
  --format json
```

Exit code was zero and neither project contained a vulnerable-package entry in the returned JSON. This means **zero vulnerabilities reported by that official query at execution time**; it is not a permanent guarantee, a source-code vulnerability scan or a Sonar result.

Package count/version output was also inspected with `dotnet list ... --include-transitive`; no undeclared direct dependency appeared. Updates must preserve exact Jellyfin ABI compatibility rather than chasing unrelated latest versions.
