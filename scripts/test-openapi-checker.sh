#!/usr/bin/env bash
set -euo pipefail

temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

equivalent="$temporary_directory/equivalent.yaml"
drifted="$temporary_directory/drifted.yaml"
private_curl="$temporary_directory/curl"

ruby -e 'File.write(ARGV[1], "# semantically equivalent, byte-different representation\n\n" + File.read(ARGV[0]))' \
  openapi.yaml "$equivalent"
ruby -e '
  source = File.read(ARGV[0])
  drifted = source.sub("version: 1.0.0", "version: 9.9.9")
  abort "test fixture did not change" if source == drifted
  File.write(ARGV[1], drifted)
' openapi.yaml "$drifted"

env -u VIAPOST_CONTRACT_SOURCE_TOKEN -u GITHUB_TOKEN -u GH_TOKEN \
  VIAPOST_OPENAPI_PUBLISHED_SOURCE="openapi.yaml" \
  scripts/check-openapi.sh >/dev/null

if env -u VIAPOST_CONTRACT_SOURCE_TOKEN -u GITHUB_TOKEN -u GH_TOKEN \
  VIAPOST_OPENAPI_PUBLISHED_SOURCE="$equivalent" \
  scripts/check-openapi.sh >"$temporary_directory/published-hash.out" 2>&1; then
  echo "OpenAPI checker accepted a published representation with the wrong hash" >&2
  exit 1
fi
grep -q 'Published OpenAPI hash mismatch' "$temporary_directory/published-hash.out" || {
  echo "OpenAPI checker did not report the published hash mismatch" >&2
  exit 1
}

cat >"$private_curl" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

output=""
url=""
while (($#)); do
  case "$1" in
    --output)
      output="$2"
      shift 2
      ;;
    https://*)
      url="$1"
      shift
      ;;
    *)
      shift
      ;;
  esac
done

[[ "$url" == "https://api.github.com/repos/ViaPost-io/base-code/contents/docs/openapi/public.yaml?ref=a8d78b4dc2140d3ceee2dbc52eb9ee438b640560" ]]
[[ "${VIAPOST_TEST_CURL_EXIT:-0}" == "0" ]] || exit "$VIAPOST_TEST_CURL_EXIT"
cp "$VIAPOST_TEST_PRIVATE_SOURCE" "$output"
EOF
chmod +x "$private_curl"

PATH="$temporary_directory:$PATH" \
  VIAPOST_CONTRACT_SOURCE_TOKEN='test-token' \
  VIAPOST_TEST_PRIVATE_SOURCE='openapi.yaml' \
  VIAPOST_OPENAPI_PUBLISHED_SOURCE="$drifted" \
  scripts/check-openapi.sh >/dev/null

if PATH="$temporary_directory:$PATH" \
  VIAPOST_CONTRACT_SOURCE_TOKEN='test-token' \
  VIAPOST_TEST_CURL_EXIT=22 \
  VIAPOST_OPENAPI_PUBLISHED_SOURCE='openapi.yaml' \
  scripts/check-openapi.sh >/dev/null 2>&1; then
  echo "OpenAPI checker fell back to the public contract after private source authentication failed" >&2
  exit 1
fi

VIAPOST_OPENAPI_COMMIT_SOURCE="$equivalent" VIAPOST_OPENAPI_SOURCE="$equivalent" scripts/check-openapi.sh >/dev/null

if VIAPOST_OPENAPI_COMMIT_SOURCE="$drifted" scripts/check-openapi.sh >/dev/null 2>&1; then
  echo "OpenAPI checker accepted an immutable source provenance mismatch" >&2
  exit 1
fi

if VIAPOST_OPENAPI_COMMIT_SOURCE="$equivalent" VIAPOST_OPENAPI_SOURCE="$drifted" scripts/check-openapi.sh >/dev/null 2>&1; then
  echo "OpenAPI checker accepted a semantic change" >&2
  exit 1
fi

echo "OpenAPI semantic comparison tests passed"
