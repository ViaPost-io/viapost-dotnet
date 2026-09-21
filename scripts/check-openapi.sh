#!/usr/bin/env bash
set -euo pipefail

expected="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("snapshot_sha256")')"
published_expected="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("published_representation_sha256")')"
source_commit="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_commit")')"
source_repository="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_repository")')"
source_path="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_path")')"
published_url="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("published_url")')"

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

[[ "$expected" =~ ^[0-9a-f]{64}$ ]] || {
  echo "OpenAPI snapshot_sha256 must be a lowercase SHA-256 digest." >&2
  exit 1
}
[[ "$published_expected" =~ ^[0-9a-f]{64}$ ]] || {
  echo "OpenAPI published_representation_sha256 must be a lowercase SHA-256 digest." >&2
  exit 1
}
test "$published_expected" = "$expected" || {
  echo "OpenAPI published and bundled snapshot hashes must match." >&2
  exit 1
}

actual="$(sha256_file openapi.yaml)"
test "$actual" = "$expected" || {
  echo "OpenAPI bundle drift: expected $expected, got $actual" >&2
  exit 1
}

[[ "$source_commit" =~ ^[0-9a-f]{40}$ ]] || {
  echo "OpenAPI source_commit must be an immutable full Git SHA." >&2
  exit 1
}
[[ "$source_repository" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || {
  echo "OpenAPI source_repository is invalid." >&2
  exit 1
}
[[ "$source_path" =~ ^[A-Za-z0-9_./-]+$ && "$source_path" != *".."* ]] || {
  echo "OpenAPI source_path is invalid." >&2
  exit 1
}

immutable_source="${VIAPOST_OPENAPI_COMMIT_SOURCE:-}"
temporary_immutable_source=""
cleanup() {
  [[ -z "$temporary_immutable_source" ]] || rm -f "$temporary_immutable_source"
}
trap cleanup EXIT
if [[ -z "$immutable_source" ]]; then
  source_token="${VIAPOST_CONTRACT_SOURCE_TOKEN:-}"
  if [[ -n "$source_token" ]]; then
    immutable_source="$(mktemp)"
    temporary_immutable_source="$immutable_source"
    # Do not follow redirects: this request carries a bearer credential.
    curl --fail --silent --show-error --proto '=https' --tlsv1.2 \
      --connect-timeout 5 --max-time 20 \
      --header "Authorization: Bearer ${source_token}" \
      --header 'Accept: application/vnd.github.raw+json' \
      "https://api.github.com/repos/${source_repository}/contents/${source_path}?ref=${source_commit}" \
      --output "$immutable_source"
  else
    ruby -ruri -e '
      uri = URI(ARGV.fetch(0))
      abort "published_url must be an HTTPS URL on docs.viapost.io" unless uri.is_a?(URI::HTTPS) && uri.host == "docs.viapost.io" && !uri.userinfo && !uri.fragment
    ' "$published_url"
    if [[ -n "${VIAPOST_OPENAPI_PUBLISHED_SOURCE:-}" ]]; then
      immutable_source="$VIAPOST_OPENAPI_PUBLISHED_SOURCE"
    else
      immutable_source="$(mktemp)"
      temporary_immutable_source="$immutable_source"
      ruby scripts/download-openapi.rb "$published_url" "$immutable_source"
    fi
    published_actual="$(sha256_file "$immutable_source")"
    test "$published_actual" = "$published_expected" || {
      echo "Published OpenAPI hash mismatch: expected $published_expected, got $published_actual" >&2
      exit 1
    }
  fi
fi

test -f "$immutable_source" || {
  echo "Immutable OpenAPI source is not a regular file." >&2
  exit 1
}

ruby -ryaml -e \
  'bundled = YAML.safe_load(File.read(ARGV[0]), aliases: false); source = YAML.safe_load(File.read(ARGV[1]), aliases: false); abort unless bundled == source' \
  openapi.yaml "$immutable_source" || {
  echo "Bundled OpenAPI differs semantically from immutable ${source_repository}@${source_commit}:${source_path}" >&2
  exit 1
}

ruby -ryaml -rjson -e '
  document = YAML.safe_load(File.read("openapi.yaml"), aliases: false)
  metadata = JSON.parse(File.read("openapi-source.json"))
  operations = document.fetch("paths").values.flat_map do |path|
    path.values.select { |operation| operation.is_a?(Hash) && operation.key?("operationId") }
  end
  authenticated = operations.select do |operation|
    Array(operation["security"]).any? { |requirement| requirement.key?("bearerApiKey") }
  end
  anonymous = operations.select { |operation| operation["security"] == [] }.map { |operation| operation.fetch("operationId") }.sort
  expected_anonymous = metadata.fetch("anonymous_operations_not_exposed").sort
  abort "authenticated operation count changed" unless authenticated.length == metadata.fetch("authenticated_operation_count")
  abort "anonymous operation policy changed" unless anonymous == expected_anonymous
' || {
  echo "OpenAPI operation policy drifted; review the typed Bearer surface and anonymous exclusions" >&2
  exit 1
}

if [[ -n "${VIAPOST_OPENAPI_SOURCE:-}" ]]; then
  ruby -ryaml -e \
    'bundled = YAML.safe_load(File.read(ARGV[0]), aliases: false); source = YAML.safe_load(File.read(ARGV[1]), aliases: false); abort unless bundled == source' \
    openapi.yaml "$VIAPOST_OPENAPI_SOURCE" || {
    echo "Bundled OpenAPI differs semantically from $VIAPOST_OPENAPI_SOURCE" >&2
    exit 1
  }
fi

echo "OpenAPI OK: immutable source ${source_repository}@${source_commit}:${source_path}, SHA-256 $actual"
