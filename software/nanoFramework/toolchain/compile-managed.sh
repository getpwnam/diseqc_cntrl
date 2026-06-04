#!/usr/bin/env bash
set -euo pipefail


SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj"
SOLUTION="$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln"
PACKAGES_DIR="$ROOT_DIR/packages"
DEFAULT_NANO_PS_PATH=""
NANO_EXT_ROOT="${HOME:-/home/vscode}/.vscode-server/extensions"
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

resolve_msbuild_cmd() {
  local msbuild_path=""

  if command -v msbuild >/dev/null 2>&1; then
    msbuild_path="$(command -v msbuild)"
    local resolved_msbuild
    resolved_msbuild="$(readlink -f "$msbuild_path" 2>/dev/null || echo "$msbuild_path")"
    if [[ "$(basename "$resolved_msbuild")" == "dotnet" ]]; then
      echo "dotnet msbuild"
      return 0
    fi

    echo "$msbuild_path"
    return 0
  fi

  if command -v dotnet >/dev/null 2>&1; then
    echo "dotnet msbuild"
    return 0
  fi

  return 1
}

restore_packages_from_config() {
  local config_path="$1"

  if [[ ! -f "$config_path" ]]; then
    return 0
  fi

  if ! command -v curl >/dev/null 2>&1; then
    echo "[error] curl is required to bootstrap nanoFramework packages." >&2
    exit 4
  fi

  if ! command -v unzip >/dev/null 2>&1; then
    echo "[error] unzip is required to bootstrap nanoFramework packages." >&2
    exit 4
  fi

  mkdir -p "$PACKAGES_DIR"

  while IFS=$'\t' read -r package_id package_version; do
    [[ -z "$package_id" || -z "$package_version" ]] && continue

    local package_dir="$PACKAGES_DIR/${package_id}.${package_version}"
    if [[ -f "$package_dir/.restored" ]]; then
      continue
    fi

    local tmp_package
    tmp_package="$(mktemp "/tmp/${package_id}.${package_version}.XXXXXX.nupkg")"
    echo "[info] Restoring package ${package_id} ${package_version}"
    curl -fsSL "https://www.nuget.org/api/v2/package/${package_id}/${package_version}" -o "$tmp_package"
    rm -rf "$package_dir"
    mkdir -p "$package_dir"
    unzip -q -o "$tmp_package" -d "$package_dir"
    rm -f "$tmp_package"
    touch "$package_dir/.restored"
  done < <(sed -n 's:.*<package id="\([^"]*\)" version="\([^"]*\)".*:\1\t\2:p' "$config_path")
}

bootstrap_packages() {
  echo "[stage] Ensuring nanoFramework package cache is present..."
  restore_packages_from_config "$ROOT_DIR/Cubley.Interop/packages.config"
  restore_packages_from_config "$ROOT_DIR/DiSEqC_Control/packages.config"
}

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Set NANO_PS_PATH to your installed path, e.g.:" >&2
  echo "  export NANO_PS_PATH=/home/<user>/.vscode-server/extensions/nanoframework.vscode-nanoframework-<ver>/dist/utils/nanoFramework/v1.0/" >&2
  exit 1
fi

# Linux host workaround for nanoFramework metadata processor dependency.
echo "[stage] Checking for System.Drawing dependencies and preparing metadata processor path..."
if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  DOTNET_SYSTEM_DRAWING_COMMON=""

  # Find System.Drawing.dll (Mono)
  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  elif [[ -d "/usr/lib/mono" ]]; then
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  else
    MONO_SYSTEM_DRAWING=""
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

echo "[info] SCRIPT_DIR: $SCRIPT_DIR"
echo "[info] ROOT_DIR: $ROOT_DIR"
echo "[info] PROJECT: $PROJECT"
echo "[info] SOLUTION: $SOLUTION"
echo "[info] NANO_PS_PATH: $NANO_PS_PATH"
echo "[info] CONFIGURATION: $CONFIGURATION"

bootstrap_packages

echo "[stage] Checking project file exists before msbuild..."
if [[ ! -f "$PROJECT" ]]; then
  echo "[error] Project file not found before msbuild: $PROJECT" >&2
  exit 2
fi

echo "[stage] Running msbuild..."
MSBUILD_CMD="$(resolve_msbuild_cmd || true)"
if [[ -z "$MSBUILD_CMD" ]]; then
  echo "[error] Could not find a usable msbuild command. Install MSBuild or .NET SDK." >&2
  exit 3
fi

if [[ "$MSBUILD_CMD" == "dotnet msbuild" ]]; then
  dotnet msbuild "$PROJECT" \
    /t:Build \
    -p:Configuration="$CONFIGURATION" \
    "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
    "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
    -verbosity:minimal
else
  "$MSBUILD_CMD" "$PROJECT" \
    /t:Build \
    -p:Configuration="$CONFIGURATION" \
    "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
    "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
    -verbosity:minimal
fi

# Deterministic bundle creation for DiSEqC_Control deploy artifacts
echo "[stage] Creating deterministic deployment bundle..."
OUTPUT_DIR="$(dirname "$PROJECT")/bin/$CONFIGURATION"
TARGET_NAME="$(basename "$PROJECT" .nfproj)"
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
    if ! "$SCRIPT_DIR/interop-checksum.sh" --check --pe "$CUBLEY_INTEROP_PE"; then
      echo "[warn] Cubley.Interop checksum mismatch; continuing bundle creation." >&2
      echo "[warn] To realign the interop checksum sources, run: $SCRIPT_DIR/interop-checksum.sh --fix --pe $CUBLEY_INTEROP_PE" >&2
    fi
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

if [[ -f "$OUTPUT_DIR/mscorlib.pe" && -f "$OUTPUT_DIR/System.Device.Gpio.pe" && -f "$OUTPUT_DIR/System.Device.I2c.pe" && -f "$OUTPUT_DIR/System.Threading.pe" && -f "$PRIMARY_PE" ]]; then
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
  pack_args+=("$OUTPUT_DIR/System.Device.I2c.pe")

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
