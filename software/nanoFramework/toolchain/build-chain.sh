#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj"
SOLUTION="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln"
NANO_PS_PATH="${NANO_PS_PATH:-/home/cp/.vscode-server/extensions/nanoframework.vscode-nanoframework-1.0.189/dist/utils/nanoFramework/v1.0/}"
CONFIGURATION="${CONFIGURATION:-Release}"

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  exit 2
fi

echo "[1/3] Restore packages"
/usr/bin/nuget restore "$SOLUTION"

echo "[2/3] Compile managed project"
/usr/local/bin/msbuild "$PROJECT" \
  /t:Compile \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  -verbosity:minimal

echo "[3/3] Full Build (includes metadata processor)"
set +e
BUILD_OUTPUT=$(/usr/local/bin/msbuild "$PROJECT" \
  /t:Build \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  -verbosity:minimal 2>&1)
BUILD_EXIT=$?
set -e

echo "$BUILD_OUTPUT"

if [[ $BUILD_EXIT -ne 0 ]]; then
  if echo "$BUILD_OUTPUT" | grep -q "System.Drawing.Common"; then
    echo
    echo "Build-chain blocker detected: metadata processor failed to load System.Drawing.Common." >&2
    echo "Managed compile is healthy; full PE generation is blocked by host/extension runtime dependency." >&2
  fi
  exit $BUILD_EXIT
fi

echo "Full build succeeded."
