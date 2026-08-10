#!/usr/bin/env bash
set -euo pipefail

project_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
dotnet_bin=${DOTNET_BIN:-dotnet}
configuration=${CONFIGURATION:-Release}

cd "$project_root"
"$dotnet_bin" restore PrivatePlayback.sln --locked-mode
"$dotnet_bin" build PrivatePlayback.sln --configuration "$configuration" --no-restore
"$dotnet_bin" test PrivatePlayback.sln \
    --configuration "$configuration" \
    --no-build \
    --collect "XPlat Code Coverage" \
    --settings tests/coverage.runsettings \
    --results-directory artifacts/test-results
