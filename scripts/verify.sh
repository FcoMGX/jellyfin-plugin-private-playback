#!/usr/bin/env bash
set -euo pipefail

project_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$project_root"

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        printf 'Required command not found: %s\n' "$1" >&2
        exit 127
    fi
}

for command_name in bash jq node rg; do
    require_command "$command_name"
done

while IFS= read -r json_file; do
    jq empty "$json_file"
done < <(rg --files -g '*.json' -g '!**/bin/**' -g '!**/obj/**' -g '!artifacts/**' | sort)

while IFS= read -r shell_file; do
    bash -n "$shell_file"
done < <(rg --files -g '*.sh' | sort)

node --input-type=module --check < src/Jellyfin.Plugin.PrivatePlayback/Configuration/Web/config.js
node tests/web/config.test.mjs

if rg -n --hidden \
    --glob '!scripts/verify.sh' \
    --glob '!**/bin/**' \
    --glob '!**/obj/**' \
    --glob '!artifacts/**' \
    '(AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9_]{30,}|sk-[A-Za-z0-9_-]{20,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|sonar\.token\s*=\s*[A-Za-z0-9]{20,})' .; then
    printf 'Potential secret detected.\n' >&2
    exit 1
fi

if rg -n --hidden \
    --glob '!scripts/verify.sh' \
    --glob '!**/bin/**' \
    --glob '!**/obj/**' \
    --glob '!artifacts/**' \
    --glob '!**/*.md' \
    'NO''SONAR|sonar\.(coverage|csharp|javascript|html|json|xml|yaml)\.exclusions' .; then
    printf 'Forbidden broad Sonar suppression detected.\n' >&2
    exit 1
fi

printf 'Static resource, syntax and secret-pattern checks passed.\n'
