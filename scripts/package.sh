#!/usr/bin/env bash
set -euo pipefail

project_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
version=0.9.0
assembly_version=0.9.0.0
artifact_dir=${ARTIFACT_DIR:-"$project_root/artifacts"}
source_date_epoch=${SOURCE_DATE_EPOCH:-1786320000}
plugin_dll="$project_root/src/Jellyfin.Plugin.PrivatePlayback/bin/Release/net9.0/Jellyfin.Plugin.PrivatePlayback.dll"
install_zip="$artifact_dir/private-playback_${assembly_version}.zip"
source_zip="$artifact_dir/private-playback_${version}_source.zip"
evidence_zip="$artifact_dir/private-playback_${version}_verification.zip"
stage_root=$(mktemp -d)
export TZ=UTC

cleanup() {
    find "$stage_root" -type f -delete
    find "$stage_root" -depth -type d -empty -delete
}
trap cleanup EXIT

[[ -f "$plugin_dll" ]] || { printf 'Release plugin DLL not found: %s\n' "$plugin_dll" >&2; exit 1; }
command -v zip >/dev/null
mkdir -p "$artifact_dir" "$stage_root/install" "$stage_root/source/private-playback-$version"
cp "$plugin_dll" "$stage_root/install/Jellyfin.Plugin.PrivatePlayback.dll"
cp "$project_root/packaging/meta.json" "$stage_root/install/meta.json"

while IFS= read -r -d '' source_file; do
    relative=${source_file#"$project_root/"}
    destination="$stage_root/source/private-playback-$version/$relative"
    mkdir -p "$(dirname "$destination")"
    cp "$source_file" "$destination"
done < <(find "$project_root" -type f \
    ! -path "$project_root/artifacts/*" \
    ! -path '*/bin/*' \
    ! -path '*/obj/*' \
    ! -path '*/.git/*' \
    -print0 | sort -z)

find "$stage_root/install" "$stage_root/source" -type f -exec touch -d "@$source_date_epoch" {} +
(
    cd "$stage_root/install"
    find . -type f -print | LC_ALL=C sort | zip -X -q "$install_zip" -@
)
(
    cd "$stage_root/source"
    find . -type f -print | LC_ALL=C sort | zip -X -q "$source_zip" -@
)

coverage_cobertura=$(find "$artifact_dir/test-results/final" -type f -name coverage.cobertura.xml -print -quit 2>/dev/null || true)
coverage_opencover=$(find "$artifact_dir/test-results/final" -type f -name coverage.opencover.xml -print -quit 2>/dev/null || true)
unit_results="$artifact_dir/test-results/final/unit-tests-final.trx"
hash_targets=("$(basename "$install_zip")" "$(basename "$source_zip")")
if [[ -f "$coverage_cobertura" && -f "$coverage_opencover" && -f "$unit_results" ]]; then
    mkdir -p "$stage_root/evidence"
    cp "$project_root/docs/TEST_RESULTS.md" "$stage_root/evidence/TEST_RESULTS.md"
    cp "$unit_results" "$stage_root/evidence/unit-tests-final.trx"
    cp "$coverage_cobertura" "$stage_root/evidence/coverage.cobertura.xml"
    cp "$coverage_opencover" "$stage_root/evidence/coverage.opencover.xml"
    find "$stage_root/evidence" -type f -exec touch -d "@$source_date_epoch" {} +
    (
        cd "$stage_root/evidence"
        find . -type f -print | LC_ALL=C sort | zip -X -q "$evidence_zip" -@
    )
    hash_targets+=("$(basename "$evidence_zip")")
fi

(
    cd "$artifact_dir"
    sha256sum "${hash_targets[@]}" > SHA256SUMS
)

printf 'Created %s\n' "$install_zip"
printf 'Created %s\n' "$source_zip"
if [[ -f "$evidence_zip" ]]; then
    printf 'Created %s\n' "$evidence_zip"
fi
printf 'Created %s\n' "$artifact_dir/SHA256SUMS"
