#!/usr/bin/env bash
set -euo pipefail

# Default deploy settings (override via CLI flags below).
SERIAL_PORT="/dev/ttyUSB0"
BAUD="921600"
ADDRESS="0x08060000"
IMAGE_PATH="build/CubleyControl/latest.deploy.bin"
DO_RESET="false"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

usage() {
  cat <<'USAGE'
Usage: ./toolchain/deploy-CubleyControl.sh [options]

Deploy CubleyControl latest bundle via nanoff.

Options:
  --serialport <path>   Serial port to use (default: /dev/ttyUSB0)
  --baud <rate>         UART baud rate (default: 921600)
  --address <addr>      Flash address (default: 0x08060000, Debug layout)
  --image <path>        Bundle path, relative to software/nanoFramework or absolute
  --reset               Reset device after deploy
  -h, --help            Show this help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --serialport)
      SERIAL_PORT="$2"
      shift 2
      ;;
    --baud)
      BAUD="$2"
      shift 2
      ;;
    --address)
      ADDRESS="$2"
      shift 2
      ;;
    --image)
      IMAGE_PATH="$2"
      shift 2
      ;;
    --reset)
      DO_RESET="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if ! command -v nanoff >/dev/null 2>&1; then
  echo "nanoff not found in PATH. Install with: dotnet tool install -g nanoff" >&2
  exit 2
fi

if [[ "$IMAGE_PATH" != /* ]]; then
  IMAGE_PATH="$ROOT_DIR/$IMAGE_PATH"
fi

if [[ ! -f "$IMAGE_PATH" ]]; then
  echo "Managed image not found: $IMAGE_PATH" >&2
  exit 1
fi

echo "Deploying image: $IMAGE_PATH"
echo "Serial: $SERIAL_PORT @ $BAUD"
echo "Address: $ADDRESS"

DEPLOY_CMD=(
  nanoff
  --nanodevice
  --serialport "$SERIAL_PORT"
  --baud "$BAUD"
  --deploy
  --image "$IMAGE_PATH"
  --address "$ADDRESS"
)

if [[ "$DO_RESET" == "true" ]]; then
  DEPLOY_CMD+=(--reset)
fi

"${DEPLOY_CMD[@]}"
echo "Deploy complete."
