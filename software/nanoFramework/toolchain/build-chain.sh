#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj"
SOLUTION="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln"
NANO_PS_PATH="${NANO_PS_PATH:-/home/cp/.vscode-server/extensions/nanoframework.vscode-nanoframework-1.0.189/dist/utils/nanoFramework/v1.0/}"
CONFIGURATION="${CONFIGURATION:-Release}"
NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="${NF_MDP_MSBUILDTASK_PATH:-}"
NF_MDP_TEMP_DIR=""

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  exit 2
fi

cleanup() {
  if [[ -n "$NF_MDP_TEMP_DIR" && -d "$NF_MDP_TEMP_DIR" ]]; then
    rm -rf "$NF_MDP_TEMP_DIR"
  fi
}
trap cleanup EXIT

# Linux host workaround:
# MetadataProcessor task expects desktop System.Drawing when running under .NET MSBuild.
# Build a temporary override folder containing extension task binaries + Mono System.Drawing.
if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  else
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  fi

  if [[ -n "$MONO_SYSTEM_DRAWING" ]]; then
    NF_MDP_TEMP_DIR="$(mktemp -d /tmp/nf-mdp-linux-XXXXXX)"
    cp -a "$NANO_PS_PATH"/* "$NF_MDP_TEMP_DIR"/
    cp "$MONO_SYSTEM_DRAWING" "$NF_MDP_TEMP_DIR/System.Drawing.dll"
    NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="$NF_MDP_TEMP_DIR"
    echo "[info] Linux MDP override enabled: $NF_MDP_MSBUILDTASK_PATH_EFFECTIVE"
    echo "[info] Using Mono System.Drawing: $MONO_SYSTEM_DRAWING"
  else
    echo "[warn] Mono System.Drawing.dll not found; full Build may still fail with System.Drawing.Common load error." >&2
  fi
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
  "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
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
