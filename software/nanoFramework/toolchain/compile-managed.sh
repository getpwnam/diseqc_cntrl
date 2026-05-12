#!/usr/bin/env bash
set -euo pipefail


SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj"
SOLUTION="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln"
DEFAULT_NANO_PS_PATH=""
NANO_EXT_ROOT="/home/cp/.vscode-server/extensions"
NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="${NF_MDP_MSBUILDTASK_PATH:-}"
NF_MDP_TEMP_DIR=""

cleanup() {
  if [[ -n "$NF_MDP_TEMP_DIR" && -d "$NF_MDP_TEMP_DIR" ]]; then
    rm -rf "$NF_MDP_TEMP_DIR"
  fi
}
trap cleanup EXIT


if [[ -z "${NANO_PS_PATH:-}" ]]; then
  shopt -s nullglob
  candidates=("$NANO_EXT_ROOT"/nanoframework.vscode-nanoframework-*/dist/utils/nanoFramework/v1.0/)
  shopt -u nullglob
  if [[ ${#candidates[@]} -gt 0 ]]; then
    DEFAULT_NANO_PS_PATH="$(printf '%s\n' "${candidates[@]}" | sort -V | tail -n1)"
  fi
fi

NANO_PS_PATH="${NANO_PS_PATH:-$DEFAULT_NANO_PS_PATH}"
CONFIGURATION="${CONFIGURATION:-Release}"

# Linux host workaround for nanoFramework metadata processor dependency.
echo "[stage] Checking for System.Drawing dependencies and preparing metadata processor path..."
if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  DOTNET_SYSTEM_DRAWING_COMMON=""

  # Find System.Drawing.dll (Mono)
  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  else
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  fi

  # Find System.Drawing.Common.dll (.NET Core/Standard)
  DOTNET_SYSTEM_DRAWING_COMMON="$(find /usr -name System.Drawing.Common.dll 2>/dev/null | head -n 1 || true)"

  if [[ -n "$MONO_SYSTEM_DRAWING" || -n "$DOTNET_SYSTEM_DRAWING_COMMON" ]]; then
    NF_MDP_TEMP_DIR="$(mktemp -d /tmp/nf-mdp-managed-XXXXXX)"
    cp -a "$NANO_PS_PATH"/* "$NF_MDP_TEMP_DIR"/
    if [[ -n "$MONO_SYSTEM_DRAWING" ]]; then
      cp "$MONO_SYSTEM_DRAWING" "$NF_MDP_TEMP_DIR/System.Drawing.dll"
    fi
    if [[ -n "$DOTNET_SYSTEM_DRAWING_COMMON" ]]; then
      cp "$DOTNET_SYSTEM_DRAWING_COMMON" "$NF_MDP_TEMP_DIR/System.Drawing.Common.dll"
    fi
    NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="$NF_MDP_TEMP_DIR"
  fi
fi


# Ensure project file exists before using sed
if [[ ! -f "$PROJECT" ]]; then
  echo "[error] Project file not found before sed: $PROJECT" >&2
  exit 2
fi

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Set NANO_PS_PATH to your installed path, e.g.:" >&2
  echo "  export NANO_PS_PATH=/home/<user>/.vscode-server/extensions/nanoframework.vscode-nanoframework-<ver>/dist/utils/nanoFramework/v1.0/" >&2
  exit 1
fi

echo "[info] SCRIPT_DIR: $SCRIPT_DIR"
echo "[info] ROOT_DIR: $ROOT_DIR"
echo "[info] PROJECT: $PROJECT"
echo "[info] SOLUTION: $SOLUTION"
echo "[info] NANO_PS_PATH: $NANO_PS_PATH"
echo "[info] CONFIGURATION: $CONFIGURATION"

echo "[stage] Checking project file exists before msbuild..."
if [[ ! -f "$PROJECT" ]]; then
  echo "[error] Project file not found before msbuild: $PROJECT" >&2
  exit 2
fi

echo "[stage] Running msbuild..."
/usr/local/bin/msbuild "$PROJECT" \
  /t:Build \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
  -verbosity:minimal

# Deterministic bundle creation (like compile-w5500-test.sh)
echo "[stage] Creating deterministic deployment bundle..."
OUTPUT_DIR="$(dirname \"$PROJECT\")/bin/$CONFIGURATION"
TARGET_NAME="$(basename \"$PROJECT\" .nfproj)"
OUTPUT_BIN="$OUTPUT_DIR/$TARGET_NAME.bin"
ASSEMBLY_NAME="$(sed -n 's:.*<AssemblyName>\(.*\)</AssemblyName>.*:\1:p' "$PROJECT" | head -n1)"
if [[ -z "$ASSEMBLY_NAME" ]]; then
  ASSEMBLY_NAME="$TARGET_NAME"
fi
PRIMARY_PE="$OUTPUT_DIR/$ASSEMBLY_NAME.pe"
PRIMARY_BIN="$OUTPUT_DIR/$ASSEMBLY_NAME.bin"
CUBLEY_INTEROP_PE="$OUTPUT_DIR/Cubley.Interop.pe"
RUNTIME_EVENTS_PE=""

if [[ -f "$CUBLEY_INTEROP_PE" ]]; then
  if [[ -x "$SCRIPT_DIR/interop-checksum.sh" ]]; then
    "$SCRIPT_DIR/interop-checksum.sh" --check --pe "$CUBLEY_INTEROP_PE"
  fi
fi

if [[ -f "$OUTPUT_DIR/nanoFramework.Runtime.Events.pe" ]]; then
  RUNTIME_EVENTS_PE="$OUTPUT_DIR/nanoFramework.Runtime.Events.pe"
else
  shopt -s nullglob
  runtime_event_candidates=("$ROOT_DIR"/packages/nanoFramework.Runtime.Events.*/lib/nanoFramework.Runtime.Events.pe)
  shopt -u nullglob
  if [[ ${#runtime_event_candidates[@]} -gt 0 ]]; then
    RUNTIME_EVENTS_PE="$(printf '%s\n' "${runtime_event_candidates[@]}" | sort -V | tail -n1)"
  fi
fi

if [[ -f "$OUTPUT_DIR/mscorlib.pe" && -f "$OUTPUT_DIR/System.Device.Gpio.pe" && -f "$OUTPUT_DIR/System.Threading.pe" && -f "$PRIMARY_PE" ]]; then
  pack_args=(
    --required-marker NFMRK1
    --out-dir "$OUTPUT_DIR"
    --out-base "${TARGET_NAME}_bundle"
    "$PRIMARY_PE"
  )

  if [[ -f "$CUBLEY_INTEROP_PE" ]]; then
    pack_args+=("$CUBLEY_INTEROP_PE")
  fi

  pack_args+=("$OUTPUT_DIR/System.Device.Gpio.pe")

  if [[ -n "$RUNTIME_EVENTS_PE" && -f "$RUNTIME_EVENTS_PE" ]]; then
    pack_args+=("$RUNTIME_EVENTS_PE")
  fi

  pack_args+=(
    "$OUTPUT_DIR/System.Threading.pe"
    "$OUTPUT_DIR/mscorlib.pe"
  )

  if [[ -x "$SCRIPT_DIR/pack-and-validate.sh" ]]; then
    "$SCRIPT_DIR/pack-and-validate.sh" "${pack_args[@]}" >/dev/null
  fi

  if [[ -L "$OUTPUT_DIR/latest.deploy.bin" || -f "$OUTPUT_DIR/latest.deploy.bin" ]]; then
    cp -f "$OUTPUT_DIR/latest.deploy.bin" "$OUTPUT_BIN"
    echo "Created deterministic deployment bundle: $OUTPUT_BIN"
  fi

  # Create a timestamped deployment bundle and symlink
  if [[ -f "$OUTPUT_BIN" ]]; then
    timestamp=$(date +%Y%m%d-%H%M%S)
    bundle_name="${TARGET_NAME}_bundle_${timestamp}.bin"
    bundle_path="$OUTPUT_DIR/$bundle_name"
    cp "$OUTPUT_BIN" "$bundle_path"
    ln -sf "$bundle_name" "$OUTPUT_DIR/latest.deploy.bin"
    echo "Created timestamped bundle: $bundle_path"
    echo "Updated symlink: $OUTPUT_DIR/latest.deploy.bin -> $bundle_name"
  fi
fi

echo "Managed compile succeeded."
