#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj"
SOLUTION="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln"
NANO_PS_PATH="${NANO_PS_PATH:-/home/cp/.vscode-server/extensions/nanoframework.vscode-nanoframework-1.0.189/dist/utils/nanoFramework/v1.0/}"
CONFIGURATION="${CONFIGURATION:-Release}"

if [[ ! -f "$PROJECT" ]]; then
  echo "Project not found: $PROJECT" >&2
  exit 1
fi

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Set NANO_PS_PATH to your installed path, e.g.:" >&2
  echo "  export NANO_PS_PATH=/home/<user>/.vscode-server/extensions/nanoframework.vscode-nanoframework-<ver>/dist/utils/nanoFramework/v1.0/" >&2
  exit 1
fi

echo "[1/2] Restoring packages..."
/usr/bin/nuget restore "$SOLUTION"

echo "[2/2] Compiling managed project ($CONFIGURATION)..."
/usr/local/bin/msbuild "$PROJECT" \
  /t:Compile \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  -verbosity:minimal

echo "Managed compile succeeded."
