#!/usr/bin/env bash
# Publishes ShardPerformance as a GitHub release.
#
#   ./scripts/release.sh   -> builds Release, stages ShardPerformance.dll + .sha256,
#                             pushes README, and creates release v<version>.
#
# Needs the repo to exist already (see the one-time setup printed by this script if the
# remote is missing). Bump the version in Properties/AssemblyInfo.cs first; a release tag
# can't be reused.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/.*MelonInfo(typeof(ShardPerformanceMod), "[^"]*", "\([^"]*\)".*/\1/p' "$ROOT/Properties/AssemblyInfo.cs")"
REPO="Flonky439/ShardPerformance"
OUT="$ROOT/bin/Release/net48"
STAGE="$ROOT/dist/release-$VERSION"

if ! git -C "$ROOT" remote get-url origin >/dev/null 2>&1; then
  echo "This repo has no 'origin' remote yet. One-time setup (run in your own Terminal):" >&2
  echo "  gh repo create $REPO --public --source \"$ROOT\" --remote origin --push" >&2
  exit 1
fi

echo "==> Building ShardPerformance $VERSION"
dotnet build "$ROOT/ShardPerformance.csproj" -c Release --nologo -v quiet --no-incremental

rm -rf "$STAGE"; mkdir -p "$STAGE"
cp "$OUT/ShardPerformance.dll" "$STAGE/"
(cd "$STAGE" && shasum -a 256 ShardPerformance.dll | awk '{print $1}' > ShardPerformance.dll.sha256)

echo "==> Pushing README and publishing v$VERSION to $REPO"
git -C "$ROOT" add -A
git -C "$ROOT" diff --cached --quiet || git -C "$ROOT" commit -qm "ShardPerformance $VERSION" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git -C "$ROOT" push -q origin HEAD

gh release create "v$VERSION" "$STAGE/ShardPerformance.dll" "$STAGE/ShardPerformance.dll.sha256" \
  --repo "$REPO" --title "ShardPerformance $VERSION" \
  --notes "EXPERIMENTAL / UNTESTED: not yet run on a live server. Server hosts only -- drop ShardPerformance.dll in your server's Mods/ folder and restart. See the README for the perf commands and the off-switches (perf set ForestNearOnly off)."
echo "    https://github.com/$REPO/releases/tag/v$VERSION"
