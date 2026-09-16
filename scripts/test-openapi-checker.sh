#!/usr/bin/env bash
set -euo pipefail

temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

equivalent="$temporary_directory/equivalent.yaml"
drifted="$temporary_directory/drifted.yaml"

ruby -e 'File.write(ARGV[1], "# semantically equivalent, byte-different representation\n\n" + File.read(ARGV[0]))' \
  openapi.yaml "$equivalent"
ruby -e '
  source = File.read(ARGV[0])
  drifted = source.sub("version: 1.0.0", "version: 9.9.9")
  abort "test fixture did not change" if source == drifted
  File.write(ARGV[1], drifted)
' openapi.yaml "$drifted"

VIAPOST_OPENAPI_SOURCE="$equivalent" scripts/check-openapi.sh >/dev/null

if VIAPOST_OPENAPI_SOURCE="$drifted" scripts/check-openapi.sh >/dev/null 2>&1; then
  echo "OpenAPI checker accepted a semantic change" >&2
  exit 1
fi

echo "OpenAPI semantic comparison tests passed"
