#!/usr/bin/env bash
set -euo pipefail

tag="${1:?usage: scripts/check-release-tag.sh vX.Y.Z}"
version="$(sed -n 's|.*<Version>\([^<]*\)</Version>.*|\1|p' src/ViaPost/ViaPost.csproj)"
sdk_version="$(sed -n 's/.*SdkVersion = "\([^"]*\)".*/\1/p' src/ViaPost/ViaPostClient.cs)"
test -n "$version"
test "$version" = "$sdk_version" || {
  echo "Project version $version does not match SDK user-agent version $sdk_version" >&2
  exit 1
}
test "$tag" = "v$version" || {
  echo "Tag $tag does not match package version v$version" >&2
  exit 1
}
echo "Release tag OK: $tag"
