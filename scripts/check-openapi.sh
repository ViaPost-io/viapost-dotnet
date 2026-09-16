#!/usr/bin/env bash
set -euo pipefail

expected="cb61b81b3276679426504eae4161e610eb5520aca2cd71cd267ed62628c518e4"
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

echo "OpenAPI OK: source commit 891adebbe79a26178fb780ec986172c890a5e261, SHA-256 $actual"
