#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="${PROJECT:-$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.nfproj}"
SOLUTION="${SOLUTION:-$ROOT_DIR/DiSEqC_Control/DiSEqC_Control.sln}"
NANO_PS_PATH="${NANO_PS_PATH:-/home/cp/.vscode-server/extensions/nanoframework.vscode-nanoframework-1.0.189/dist/utils/nanoFramework/v1.0/}"
CONFIGURATION="${CONFIGURATION:-Release}"
IMAGE_PATH=""
SERIAL_PORT=""
DEPLOY_ADDRESS=""
BAUD="115200"
DO_DEPLOY="false"
DO_RESET="false"

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
  --image <path>                      PE image path for deployment (default: bin/<Configuration>/<Target>.pe)
  --deploy                            Deploy managed image after successful build
  --serialport <port>                 Serial wire-protocol port for deployment
  --address <hex>                     Deployment address, e.g. 0x080C0000
  --baud <rate>                       Serial baud for deploy (default: 115200)
  --reset                             Reset device after deploy
  --help                              Show this help message

Notes:
  - This script performs a full managed Build (/t:Build), producing .pe artifacts.
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

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  exit 2
fi

TARGET_NAME="$(basename "$PROJECT" .nfproj)"
if [[ -z "$IMAGE_PATH" ]]; then
  IMAGE_PATH="$(dirname "$PROJECT")/bin/$CONFIGURATION/$TARGET_NAME.pe"
fi

echo "[1/3] Restoring packages"
/usr/bin/nuget restore "$SOLUTION"

echo "[2/3] Building managed project ($CONFIGURATION)"
/usr/local/bin/msbuild "$PROJECT" \
  /t:Build \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  -verbosity:minimal

if [[ ! -f "$IMAGE_PATH" ]]; then
  echo "Managed image not found after build: $IMAGE_PATH" >&2
  exit 1
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
