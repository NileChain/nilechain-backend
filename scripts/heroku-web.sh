#!/usr/bin/env bash
# Start the published API on Heroku (jincod) or a local publish folder.
set -euo pipefail

candidates=(
  "${HOME}/heroku_output"
  "heroku_output"
  "NileChain.API/bin/publish"
  "NileChain.API/bin/Release/net10.0/linux-x64/publish"
  "NileChain.API/bin/Release/net10.0/publish"
)

for dir in "${candidates[@]}"; do
  if [[ -x "${dir}/NileChain.API" ]]; then
    cd "${dir}"
    exec ./NileChain.API
  fi
  if [[ -f "${dir}/NileChain.API.dll" ]]; then
    cd "${dir}"
    exec dotnet NileChain.API.dll
  fi
done

echo "NileChain.API binary not found. Layout:" >&2
find . -name 'NileChain.API*' 2>/dev/null | head -n 40 >&2 || true
ls -la >&2
exit 1
