#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="${PROJECT:-$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj}"
SOLUTION="${SOLUTION:-$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln}"
CHECKSUM_TOOL="$SCRIPT_DIR/interop-checksum.sh"

resolve_nano_ps_path() {
  local root="/home/cp/.vscode-server/extensions"
  local latest=""

  if [[ -d "$root" ]]; then
    latest="$(ls -1d "$root"/nanoframework.vscode-nanoframework-* 2>/dev/null | sort -V | tail -n 1)"
    if [[ -n "$latest" && -d "$latest/dist/utils/nanoFramework/v1.0" ]]; then
      echo "$latest/dist/utils/nanoFramework/v1.0/"
      return 0
    fi
  fi

  return 1
}

if [[ -n "${NANO_PS_PATH:-}" ]]; then
  NANO_PS_PATH="$NANO_PS_PATH"
else
  NANO_PS_PATH="$(resolve_nano_ps_path || true)"
fi

# If caller/environment provided a stale path, try auto-detect before failing.
if [[ -n "$NANO_PS_PATH" && ! -d "$NANO_PS_PATH" ]]; then
  AUTO_NANO_PS_PATH="$(resolve_nano_ps_path || true)"
  if [[ -n "$AUTO_NANO_PS_PATH" && -d "$AUTO_NANO_PS_PATH" ]]; then
    NANO_PS_PATH="$AUTO_NANO_PS_PATH"
  fi
fi

CONFIGURATION="${CONFIGURATION:-Release}"
IMAGE_PATH=""
IMAGE_ARG_SET="false"
NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="${NF_MDP_MSBUILDTASK_PATH:-}"
NF_MDP_TEMP_DIR=""
SERIAL_PORT=""
DEPLOY_ADDRESS=""
BAUD="115200"
DO_DEPLOY="false"
DO_RESET="false"

cleanup() {
  if [[ -n "$NF_MDP_TEMP_DIR" && -d "$NF_MDP_TEMP_DIR" ]]; then
    rm -rf "$NF_MDP_TEMP_DIR"
  fi
}
trap cleanup EXIT

usage() {
  cat << 'EOF'
Usage:
  ./toolchain/build-managed-cli.sh [options]

Build only (default):
  ./toolchain/build-managed-cli.sh

Build and deploy:
  ./toolchain/build-managed-cli.sh --deploy --serialport /dev/ttyUSB0 --address 0x080C0000

Options:
  --configuration <Debug|Release>     Build configuration (default: Release)
  --project <path>                    Path to .nfproj file
  --solution <path>                   Path to .sln file for restore
  --nano-ps-path <path>               nanoFramework project system path
  --image <path>                      Managed image path for deployment (default: bin/<Configuration>/<Target>.bin, fallback to .pe)
  --deploy                            Deploy managed image after successful build
  --serialport <port>                 Serial wire-protocol port for deployment
  --address <hex>                     Deployment address, e.g. 0x080C0000
  --baud <rate>                       Serial baud for deploy (default: 115200)
  --reset                             Reset device after deploy
  --help                              Show this help message

Notes:
  - This script performs a full managed Build (/t:Build), producing .pe and (for app projects) .bin artifacts.
  - Deployment uses nanoff: --nanodevice --deploy --image --address --serialport.
  - You must provide a valid deployment address for your firmware memory layout.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --project)
      PROJECT="$2"
      shift 2
      ;;
    --solution)
      SOLUTION="$2"
      shift 2
      ;;
    --nano-ps-path)
      NANO_PS_PATH="$2"
      shift 2
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

if [[ ! -f "$PROJECT" ]]; then
  echo "Project not found: $PROJECT" >&2
  exit 1
fi

if [[ ! -f "$SOLUTION" ]]; then
  echo "Solution not found: $SOLUTION" >&2
  exit 1
fi

if [[ -x "$CHECKSUM_TOOL" ]]; then
  echo "[preflight] Validating interop checksum and AssemblyNativeVersion scope"
  "$CHECKSUM_TOOL" --check
else
  echo "[warn] Interop checksum tool not found or not executable: $CHECKSUM_TOOL" >&2
fi

if [[ -z "$NANO_PS_PATH" || ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Install the nanoFramework VS Code extension in this WSL environment, or pass --nano-ps-path explicitly." >&2
  exit 2
fi

# Linux host workaround for nanoFramework metadata processor dependency.
if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  else
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  fi

  if [[ -n "$MONO_SYSTEM_DRAWING" ]]; then
    NF_MDP_TEMP_DIR="$(mktemp -d /tmp/nf-mdp-managed-XXXXXX)"
    cp -a "$NANO_PS_PATH"/* "$NF_MDP_TEMP_DIR"/
    cp "$MONO_SYSTEM_DRAWING" "$NF_MDP_TEMP_DIR/System.Drawing.dll"
    NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="$NF_MDP_TEMP_DIR"
  else
    echo "[warn] Mono System.Drawing.dll not found; build may fail with System.Drawing.Common load error." >&2
  fi
fi

TARGET_NAME="$(basename "$PROJECT" .nfproj)"
IMAGE_DEFAULT_BIN_PATH="$(dirname "$PROJECT")/bin/$CONFIGURATION/$TARGET_NAME.bin"
IMAGE_FALLBACK_PATH="$(dirname "$PROJECT")/bin/$CONFIGURATION/$TARGET_NAME.pe"
IMAGE_NFMRK2_PATH="$(dirname "$PROJECT")/bin/$CONFIGURATION/$TARGET_NAME.nfmrk2.bin"

if [[ -z "$IMAGE_PATH" ]]; then
  if [[ -f "$IMAGE_DEFAULT_BIN_PATH" ]]; then
    IMAGE_PATH="$IMAGE_DEFAULT_BIN_PATH"
  elif [[ -f "$IMAGE_FALLBACK_PATH" ]]; then
    IMAGE_PATH="$IMAGE_FALLBACK_PATH"
  else
    IMAGE_PATH="$IMAGE_NFMRK2_PATH"
  fi
fi

echo "[1/3] Restoring packages"
/usr/bin/nuget restore "$SOLUTION"

echo "[2/3] Building managed project ($CONFIGURATION)"
/usr/local/bin/msbuild "$PROJECT" \
  /t:Build \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
  -verbosity:minimal

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

if [[ -z "$SERIAL_PORT" || -z "$DEPLOY_ADDRESS" ]]; then
  echo "--deploy requires --serialport and --address" >&2
  exit 2
fi

if ! command -v nanoff >/dev/null 2>&1; then
  echo "nanoff not found in PATH. Install with: dotnet tool install -g nanoff" >&2
  exit 2
fi

echo "[3/3] Deploying managed image"
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
