# Third-party notices

The installable ZIP contains only `Jellyfin.Plugin.PrivatePlayback.dll` and plugin metadata. It does not bundle third-party library binaries. At runtime it uses assemblies supplied by the exact Jellyfin 10.11.11 server and the .NET 9 shared framework.

## Compile-time Jellyfin contracts

| Package | Version | Licence declared by NuGet | Source |
| --- | --- | --- | --- |
| Jellyfin.Controller | 10.11.11 | GPL-3.0-only | <https://github.com/jellyfin/jellyfin/tree/1fbd8739292cce610231be93daf43368733edf63> |
| Jellyfin.Model | 10.11.11 | GPL-3.0-only | <https://github.com/jellyfin/jellyfin/tree/1fbd8739292cce610231be93daf43368733edf63> |

Their runtime assets are excluded from the plugin output. Jellyfin trademarks and project identity belong to their respective owners; this plugin is not endorsed by or part of the Jellyfin project.

## Build and test tooling

The source build uses Microsoft .NET SDK and test infrastructure, xUnit, coverlet.collector, StyleCop.Analyzers, SerilogAnalyzer and SmartAnalyzers.MultithreadingAnalyzer under their respective upstream licences. Exact resolved versions and integrity hashes are recorded in `packages.lock.json`; these tools are not included in the installable plugin ZIP.

SonarScanner for .NET 11.2.1 is pinned as an optional repository tool under SonarSource's declared LGPL-3.0 licence and is not bundled in either plugin binary.

No third-party source code was copied into the plugin implementation.
