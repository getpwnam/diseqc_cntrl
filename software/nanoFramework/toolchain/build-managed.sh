#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="${PROJECT:-}"
SOLUTION="${SOLUTION:-}"
PACKAGES_DIR="$ROOT_DIR/packages"
CHECKSUM_TOOL="$SCRIPT_DIR/interop-checksum.sh"

DEFAULT_NANO_PS_PATH=""
NANO_EXT_ROOT="${HOME:-/home/vscode}/.vscode-server/extensions"
NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="${NF_MDP_MSBUILDTASK_PATH:-}"
NF_MDP_TEMP_DIR=""

CONFIGURATION="${CONFIGURATION:-Release}"
MODE=""
IMAGE_PATH=""
IMAGE_ARG_SET="false"
PROJECT_ARG_SET="${PROJECT:+true}"
SOLUTION_ARG_SET="${SOLUTION:+true}"
SERIAL_PORT=""
DEPLOY_ADDRESS=""
BAUD="115200"
DO_DEPLOY="false"
DO_RESET="false"
USE_SWD="false"
SKIP_RESTORE="false"

cleanup() {
  if [[ -n "$NF_MDP_TEMP_DIR" && -d "$NF_MDP_TEMP_DIR" ]]; then
    rm -rf "$NF_MDP_TEMP_DIR"
  fi
}
trap cleanup EXIT

usage() {
  cat << 'EOF'
Usage:
  ./toolchain/build-managed.sh (list|compile|build) [options]

Examples:
  ./toolchain/build-managed.sh list
  ./toolchain/build-managed.sh compile
  ./toolchain/build-managed.sh build
  ./toolchain/build-managed.sh build --deploy --serialport /dev/ttyUSB0 --address 0x080C0000
  ./toolchain/build-managed.sh build --deploy --swd --address 0x080C0000

Options:

General options (all modes):
  --configuration <Debug|Release>   Build configuration (default: Release)
  --project <path>                  Path to .nfproj file or project directory
  --solution <path>                 Path to .sln file for restore
  --nano-ps-path <path>             nanoFramework project system path
  --skip-restore                    Skip dotnet restore/bootstrap step
  --help                            Show this help message

Mode-specific options:

build mode:
  --image <path>                    Managed image path for deployment
  --deploy                          Deploy managed image after successful build
  --swd                             Deploy via SWD using st-flash instead of nanoff
  --serialport <port>               Serial wire-protocol port for deployment
  --address <hex>                   Deployment address, e.g. 0x080C0000
  --baud <rate>                     Serial baud for deploy (default: 115200)
  --reset                           Reset device after deploy
EOF
}

list_buildable_projects() {
  local found=0
  local nfproj=""

  echo "Buildable managed projects under: $ROOT_DIR"

  while IFS= read -r nfproj; do
    local project_dir
    local rel_project_dir
    local rel_nfproj
    local sln_count
    local sln_single
    local sln_info

    project_dir="$(dirname "$nfproj")"
    rel_project_dir="${project_dir#"$ROOT_DIR"/}"
    rel_nfproj="${nfproj#"$ROOT_DIR"/}"

    sln_count="$(find "$project_dir" -maxdepth 1 -type f -name "*.sln" | wc -l | tr -d ' ')"
    if [[ "$sln_count" == "1" ]]; then
      sln_single="$(find "$project_dir" -maxdepth 1 -type f -name "*.sln" | head -n 1)"
      sln_info="${sln_single#"$ROOT_DIR"/}"
    elif [[ "$sln_count" == "0" ]]; then
      sln_info="(none)"
    else
      sln_info="(multiple in project dir)"
    fi

    echo "- project: $rel_project_dir"
    echo "  nfproj:  $rel_nfproj"
    echo "  sln:     $sln_info"
    found=1
  done < <(find "$ROOT_DIR" -type f -name "*.nfproj" ! -path "*/bin/*" ! -path "*/obj/*" | sort)

  if [[ "$found" == "0" ]]; then
    echo "No buildable .nfproj files found."
  fi
}

resolve_input_path() {
  local input_path="$1"

  if [[ "$input_path" = /* ]]; then
    printf '%s\n' "$input_path"
  else
    printf '%s\n' "$ROOT_DIR/$input_path"
  fi
}

discover_single_file() {
  local dir="$1"
  local pattern="$2"
  local maxdepth="$3"
  local matches=()

  while IFS= read -r file; do
    matches+=("$file")
  done < <(find "$dir" -maxdepth "$maxdepth" -type f -name "$pattern" | sort)

  if [[ ${#matches[@]} -eq 1 ]]; then
    printf '%s\n' "${matches[0]}"
    return 0
  fi

  if [[ ${#matches[@]} -eq 0 ]]; then
    return 1
  fi

  echo "Ambiguous discovery in '$dir': multiple files matching '$pattern'" >&2
  for m in "${matches[@]}"; do
    echo "  - $m" >&2
  done
  return 2
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

  if command -v dotnet >/dev/null 2>&1; then
    local restore_output
    local restore_exit=0

    restore_output="$(dotnet restore "$SOLUTION" 2>&1)" || restore_exit=$?

    # dotnet restore does not fully understand nanoFramework nfproj/sln layouts,
    # so suppress known non-actionable warnings while keeping everything else.
    if [[ -n "$restore_output" ]]; then
      printf '%s\n' "$restore_output" | grep -Ev '(^.+\.nfproj : warning NU1503: Skipping restore for project )|(^/usr/share/dotnet/sdk/.+/NuGet\.targets\([0-9]+,[0-9]+\): warning : Unable to find a project to restore!)'
    fi

    if [[ $restore_exit -ne 0 ]]; then
      return "$restore_exit"
    fi
  fi
}

resolve_nano_ps_path() {
  local latest=""

  if [[ -d "$NANO_EXT_ROOT" ]]; then
    latest="$(ls -1d "$NANO_EXT_ROOT"/nanoframework.vscode-nanoframework-* 2>/dev/null | sort -V | tail -n 1)"
    if [[ -n "$latest" && -d "$latest/dist/utils/nanoFramework/v1.0" ]]; then
      echo "$latest/dist/utils/nanoFramework/v1.0/"
      return 0
    fi
  fi

  return 1
}

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

run_msbuild() {
  local target="$1"
  local msbuild_cmd="$2"

  if [[ "$msbuild_cmd" == "dotnet msbuild" ]]; then
    dotnet msbuild "$PROJECT" \
      "/t:${target}" \
      -p:Configuration="$CONFIGURATION" \
      "-p:OutputPath=$MANAGED_BUILD_DIR/" \
      "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
      "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
      -verbosity:minimal
  else
    "$msbuild_cmd" "$PROJECT" \
      "/t:${target}" \
      -p:Configuration="$CONFIGURATION" \
      "-p:OutputPath=$MANAGED_BUILD_DIR/" \
      "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
      "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
      -verbosity:minimal
  fi
}

while [[ $# -gt 0 ]]; do
  if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    usage
    exit 0
  fi

  MODE="$1"
  shift
  break
done

if [[ -z "$MODE" ]]; then
  echo "Missing mode argument. Use: build-managed.sh (compile|build) [options]" >&2
  usage
  exit 2
fi

if [[ "$MODE" != "list" && "$MODE" != "build" && "$MODE" != "compile" ]]; then
  echo "Invalid mode '$MODE'. Use list, compile, or build." >&2
  exit 2
fi

if [[ "$MODE" == "list" ]]; then
  list_buildable_projects
  exit 0
fi

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --project)
      PROJECT="$2"
      PROJECT_ARG_SET="true"
      shift 2
      ;;
    --solution)
      SOLUTION="$2"
      SOLUTION_ARG_SET="true"
      shift 2
      ;;
    --nano-ps-path)
      NANO_PS_PATH="$2"
      shift 2
      ;;
    --skip-restore)
      SKIP_RESTORE="true"
      shift
      ;;
    --image)
      IMAGE_PATH="$2"
      IMAGE_ARG_SET="true"
      shift 2
      ;;
    --deploy)
      DO_DEPLOY="true"
      shift
      ;;
    --swd)
      USE_SWD="true"
      shift
      ;;
    --serialport)
      SERIAL_PORT="$2"
      shift 2
      ;;
    --address)
      DEPLOY_ADDRESS="$2"
      shift 2
      ;;
    --baud)
      BAUD="$2"
      shift 2
      ;;
    --reset)
      DO_RESET="true"
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$PROJECT" ]]; then
  PROJECT="$ROOT_DIR"
fi

PROJECT="$(resolve_input_path "$PROJECT")"

if [[ -d "$PROJECT" ]]; then
  PROJECT_DIR="$PROJECT"
  discovered_nfproj=""
  if discovered_nfproj="$(discover_single_file "$PROJECT_DIR" "*.nfproj" 1)"; then
    :
  else
    rc=$?
    if [[ $rc -eq 2 ]]; then
      exit 1
    fi
  fi
  if [[ -z "$discovered_nfproj" ]]; then
    if discovered_nfproj="$(discover_single_file "$PROJECT_DIR" "*.nfproj" 2)"; then
      :
    else
      rc=$?
      if [[ $rc -eq 2 ]]; then
        exit 1
      fi
    fi
  fi

  if [[ -z "$discovered_nfproj" ]]; then
    echo "No .nfproj found under project directory: $PROJECT_DIR" >&2
    exit 1
  fi

  PROJECT="$discovered_nfproj"

  if [[ "$SOLUTION_ARG_SET" != "true" ]]; then
    discovered_sln=""
    if discovered_sln="$(discover_single_file "$PROJECT_DIR" "*.sln" 1)"; then
      :
    else
      rc=$?
      if [[ $rc -eq 2 ]]; then
        exit 1
      fi
    fi
    if [[ -n "$discovered_sln" ]]; then
      SOLUTION="$discovered_sln"
    fi
  fi
fi

if [[ "$SOLUTION_ARG_SET" == "true" ]]; then
  SOLUTION="$(resolve_input_path "$SOLUTION")"
fi

if [[ -z "$SOLUTION" ]]; then
  project_parent_dir="$(dirname "$PROJECT")"
  discovered_sln=""
  if discovered_sln="$(discover_single_file "$project_parent_dir" "*.sln" 1)"; then
    :
  else
    rc=$?
    if [[ $rc -eq 2 ]]; then
      exit 1
    fi
  fi

  if [[ -n "$discovered_sln" ]]; then
    SOLUTION="$discovered_sln"
  else
    # Fall back to project-scoped restore when no sibling solution exists.
    SOLUTION="$PROJECT"
  fi
fi

if [[ ! -d "$PROJECT" && "${PROJECT##*.}" != "nfproj" ]]; then
  echo "--project must be a .nfproj file or a directory. Got: $PROJECT" >&2
  exit 1
fi

if [[ ! -f "$PROJECT" ]]; then
  echo "Project not found: $PROJECT" >&2
  exit 1
fi

if [[ ! -f "$SOLUTION" ]]; then
  echo "Solution not found: $SOLUTION" >&2
  exit 1
fi

if [[ -z "${NANO_PS_PATH:-}" ]]; then
  shopt -s nullglob
  candidates=("$NANO_EXT_ROOT"/nanoframework.vscode-nanoframework-*/dist/utils/nanoFramework/v1.0/)
  shopt -u nullglob
  if [[ ${#candidates[@]} -gt 0 ]]; then
    DEFAULT_NANO_PS_PATH="$(printf '%s\n' "${candidates[@]}" | sort -V | tail -n1)"
  fi
  NANO_PS_PATH="${DEFAULT_NANO_PS_PATH:-}"
fi

if [[ -n "$NANO_PS_PATH" && ! -d "$NANO_PS_PATH" ]]; then
  AUTO_NANO_PS_PATH="$(resolve_nano_ps_path || true)"
  if [[ -n "$AUTO_NANO_PS_PATH" && -d "$AUTO_NANO_PS_PATH" ]]; then
    NANO_PS_PATH="$AUTO_NANO_PS_PATH"
  fi
fi

if [[ -z "$NANO_PS_PATH" || ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Set NANO_PS_PATH or pass --nano-ps-path explicitly." >&2
  exit 2
fi

if [[ -x "$CHECKSUM_TOOL" ]]; then
  echo "[preflight] Validating interop checksum and AssemblyNativeVersion scope"
  "$CHECKSUM_TOOL" --check
else
  echo "[warn] Interop checksum tool not found or not executable: $CHECKSUM_TOOL" >&2
fi

if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  DOTNET_SYSTEM_DRAWING_COMMON=""

  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  elif [[ -d "/usr/lib/mono" ]]; then
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  fi

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

TARGET_NAME="$(basename "$PROJECT" .nfproj)"
MANAGED_BUILD_DIR="$ROOT_DIR/build/$TARGET_NAME"
IMAGE_DEFAULT_BIN_PATH="$MANAGED_BUILD_DIR/$TARGET_NAME.bin"
IMAGE_FALLBACK_PATH="$MANAGED_BUILD_DIR/$TARGET_NAME.pe"
IMAGE_NFMRK2_PATH="$MANAGED_BUILD_DIR/$TARGET_NAME.nfmrk2.bin"

mkdir -p "$MANAGED_BUILD_DIR"

if [[ "$SKIP_RESTORE" != "true" ]]; then
  echo "[1/3] Restoring packages"
  bootstrap_packages
else
  echo "[1/3] Restore skipped (--skip-restore)"
fi

MSBUILD_CMD="$(resolve_msbuild_cmd || true)"
if [[ -z "$MSBUILD_CMD" ]]; then
  echo "[error] Could not find a usable msbuild command. Install MSBuild or .NET SDK." >&2
  exit 3
fi

if [[ "$MODE" == "compile" ]]; then
  echo "[2/3] Compiling managed project ($CONFIGURATION)"
  run_msbuild "Compile" "$MSBUILD_CMD"
  if [[ "$DO_DEPLOY" == "true" ]]; then
    echo "--deploy requires 'build' mode (compile mode does not produce deployable bundles)." >&2
    exit 2
  fi
  echo "[3/3] Compile mode complete (no deployable image validation in compile mode)."
  exit 0
else
  echo "[2/3] Building managed project ($CONFIGURATION)"
  run_msbuild "Build" "$MSBUILD_CMD"

  echo "[stage] Creating deterministic deployment bundle..."
  OUTPUT_DIR="$MANAGED_BUILD_DIR"
  OUTPUT_BIN="$OUTPUT_DIR/$TARGET_NAME.bin"
  ASSEMBLY_NAME="$(sed -n 's:.*<AssemblyName>\(.*\)</AssemblyName>.*:\1:p' "$PROJECT" | head -n1)"
  if [[ -z "$ASSEMBLY_NAME" ]]; then
    ASSEMBLY_NAME="$TARGET_NAME"
  fi
  PRIMARY_PE="$OUTPUT_DIR/$ASSEMBLY_NAME.pe"
  CUBLEY_INTEROP_PE="$OUTPUT_DIR/Cubley.Interop.pe"
  RUNTIME_EVENTS_PE=""

  if [[ -f "$CUBLEY_INTEROP_PE" && -x "$CHECKSUM_TOOL" ]]; then
    if ! "$CHECKSUM_TOOL" --check --pe "$CUBLEY_INTEROP_PE"; then
      echo "[warn] Cubley.Interop checksum mismatch; continuing bundle creation." >&2
      echo "[warn] To realign, run: $CHECKSUM_TOOL --fix --pe $CUBLEY_INTEROP_PE" >&2
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

    pack_args+=("$OUTPUT_DIR/System.Threading.pe")
    pack_args+=("$OUTPUT_DIR/mscorlib.pe")

    if [[ -x "$SCRIPT_DIR/pack-and-validate.sh" ]]; then
      "$SCRIPT_DIR/pack-and-validate.sh" "${pack_args[@]}" >/dev/null
    fi

    if [[ -L "$OUTPUT_DIR/latest.deploy.bin" || -f "$OUTPUT_DIR/latest.deploy.bin" ]]; then
      cp -f "$OUTPUT_DIR/latest.deploy.bin" "$OUTPUT_BIN"
      echo "Created deterministic deployment bundle: $OUTPUT_BIN"
    fi

    if [[ -f "$OUTPUT_BIN" ]]; then
      timestamp="$(date +%Y%m%d-%H%M%S)"
      bundle_name="${TARGET_NAME}_bundle_${timestamp}.bin"
      bundle_path="$OUTPUT_DIR/$bundle_name"
      cp "$OUTPUT_BIN" "$bundle_path"
      ln -sf "$bundle_name" "$OUTPUT_DIR/latest.deploy.bin"
      echo "Created timestamped bundle: $bundle_path"
      echo "Updated symlink: $OUTPUT_DIR/latest.deploy.bin -> $bundle_name"
    fi
  fi
fi

if [[ -z "$IMAGE_PATH" ]]; then
  if [[ -f "$IMAGE_DEFAULT_BIN_PATH" ]]; then
    IMAGE_PATH="$IMAGE_DEFAULT_BIN_PATH"
  elif [[ -f "$IMAGE_FALLBACK_PATH" ]]; then
    IMAGE_PATH="$IMAGE_FALLBACK_PATH"
  else
    IMAGE_PATH="$IMAGE_NFMRK2_PATH"
  fi
fi

if [[ ! -f "$IMAGE_PATH" ]]; then
  if [[ "$IMAGE_ARG_SET" == "false" ]]; then
    if [[ -f "$IMAGE_DEFAULT_BIN_PATH" ]]; then
      IMAGE_PATH="$IMAGE_DEFAULT_BIN_PATH"
    elif [[ -f "$IMAGE_FALLBACK_PATH" ]]; then
      IMAGE_PATH="$IMAGE_FALLBACK_PATH"
    elif [[ -f "$IMAGE_NFMRK2_PATH" ]]; then
      IMAGE_PATH="$IMAGE_NFMRK2_PATH"
    else
      echo "Managed image not found after build: $IMAGE_PATH" >&2
      exit 1
    fi
  else
    echo "Managed image not found after build: $IMAGE_PATH" >&2
    exit 1
  fi
fi

echo "Managed image ready: $IMAGE_PATH"

if [[ "$DO_DEPLOY" != "true" ]]; then
  echo "[3/3] Deploy skipped (use --deploy to upload image)"
  exit 0
fi

if [[ -z "$DEPLOY_ADDRESS" ]]; then
  echo "--deploy requires --address" >&2
  exit 2
fi

if [[ "$USE_SWD" == "true" ]]; then
  if ! command -v st-flash >/dev/null 2>&1; then
    echo "st-flash not found in PATH. Install with: sudo apt install stlink-tools" >&2
    exit 2
  fi

  echo "[3/3] Deploying managed image via SWD (st-flash)"
  if [[ "$DO_RESET" == "true" ]]; then
    st-flash --reset write "$IMAGE_PATH" "$DEPLOY_ADDRESS"
  else
    st-flash write "$IMAGE_PATH" "$DEPLOY_ADDRESS"
  fi
  echo "Deploy complete via SWD."
else
  if [[ -z "$SERIAL_PORT" ]]; then
    echo "--deploy with nanoff requires --serialport" >&2
    exit 2
  fi

  if ! command -v nanoff >/dev/null 2>&1; then
    echo "nanoff not found in PATH. Install with: dotnet tool install -g nanoff" >&2
    exit 2
  fi

  echo "[3/3] Deploying managed image via wire protocol (nanoff)"
  DEPLOY_CMD=(
    nanoff
    --nanodevice
    --serialport "$SERIAL_PORT"
    --baud "$BAUD"
    --deploy
    --image "$IMAGE_PATH"
    --address "$DEPLOY_ADDRESS"
  )

  if [[ "$DO_RESET" == "true" ]]; then
    DEPLOY_CMD+=(--reset)
  fi

  "${DEPLOY_CMD[@]}"
  echo "Deploy complete."
fi
