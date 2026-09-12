#!/usr/bin/env bash
set -euo pipefail

expected="d1f223342ad1ca326ba716af6e508c78594e1b108958cce2ec4a1efd31a9773a"
if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum openapi.yaml | awk '{print $1}')"
else
  actual="$(shasum -a 256 openapi.yaml | awk '{print $1}')"
fi
test "$actual" = "$expected" || {
  echo "OpenAPI bundle drift: expected $expected, got $actual" >&2
  exit 1
}

if [[ -n "${VIAPOST_OPENAPI_SOURCE:-}" ]]; then
  cmp --silent openapi.yaml "$VIAPOST_OPENAPI_SOURCE" || {
    echo "Bundled OpenAPI differs from $VIAPOST_OPENAPI_SOURCE" >&2
    exit 1
  }
fi

echo "OpenAPI OK: source commit 1daaf57b8c8bb7481b7c8633a68705428de1f90a, SHA-256 $actual"
