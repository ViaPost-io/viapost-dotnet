#!/usr/bin/env bash
set -euo pipefail

expected="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("snapshot_sha256")')"
source_commit="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_commit")')"
source_repository="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_repository")')"
source_path="$(ruby -rjson -e 'puts JSON.parse(File.read("openapi-source.json")).fetch("source_path")')"
if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum openapi.yaml | awk '{print $1}')"
else
  actual="$(shasum -a 256 openapi.yaml | awk '{print $1}')"
fi
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
if [[ -z "$immutable_source" ]]; then
  github_token="${VIAPOST_CONTRACT_SOURCE_TOKEN:-${GITHUB_TOKEN:-${GH_TOKEN:-}}}"
  [[ -n "$github_token" ]] || {
    echo "A source token is required to retrieve the immutable private source contract (VIAPOST_CONTRACT_SOURCE_TOKEN, GITHUB_TOKEN, or GH_TOKEN)." >&2
    exit 1
  }
  immutable_source="$(mktemp)"
  trap 'rm -f "$immutable_source"' EXIT
  curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 \
    --header "Authorization: Bearer ${github_token}" \
    --header 'Accept: application/vnd.github.raw+json' \
    "https://api.github.com/repos/${source_repository}/contents/${source_path}?ref=${source_commit}" \
    --output "$immutable_source"
fi

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
