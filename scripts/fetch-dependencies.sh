#!/usr/bin/env bash
# Fetches the PC game reference assemblies Modding Tavern vendors (TavernLib and
# TavernAnti), including a publicized Root.Township.dll. No game install needed.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
git clone -q --depth 1 https://github.com/ModdingTavern/TavernLib.git "$WORK/TavernLib"
git clone -q --depth 1 https://github.com/ModdingTavern/TavernAnti.git "$WORK/TavernAnti"
mkdir -p "$ROOT/Dependencies"
cp "$WORK/TavernLib/Dependencies/"*.dll "$WORK/TavernLib/Generated/"*.dll "$ROOT/Dependencies/"
cp "$WORK/TavernAnti/Dependencies/UnityEngine.PhysicsModule.dll" "$ROOT/Dependencies/"
echo "$(ls -1 "$ROOT/Dependencies"/*.dll | wc -l | tr -d ' ') assemblies in Dependencies/"
