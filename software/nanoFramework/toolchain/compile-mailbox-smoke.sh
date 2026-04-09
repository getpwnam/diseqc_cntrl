#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT_DIR/tests/MailboxSmoke/MailboxSmoke.nfproj"
INTEROP_PROJECT="$ROOT_DIR/DiSEqC_Control.Interop/DiSEqC_Control.Interop.nfproj"
NANO_EXT_ROOT="/home/cp/.vscode-server/extensions"
DEFAULT_NANO_PS_PATH=""
CONFIGURATION="${CONFIGURATION:-Release}"
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

if [[ ! -f "$PROJECT" ]]; then
  echo "Project not found: $PROJECT" >&2
  exit 1
fi

if [[ ! -d "$NANO_PS_PATH" ]]; then
  echo "NanoFrameworkProjectSystemPath not found: $NANO_PS_PATH" >&2
  echo "Set NANO_PS_PATH to your installed path." >&2
  exit 1
fi

if [[ -z "$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" ]]; then
  MONO_SYSTEM_DRAWING=""
  if [[ -f "/usr/lib/mono/4.5/System.Drawing.dll" ]]; then
    MONO_SYSTEM_DRAWING="/usr/lib/mono/4.5/System.Drawing.dll"
  else
    MONO_SYSTEM_DRAWING="$(find /usr/lib/mono -name System.Drawing.dll | head -n 1 || true)"
  fi

  if [[ -n "$MONO_SYSTEM_DRAWING" ]]; then
    NF_MDP_TEMP_DIR="$(mktemp -d /tmp/nf-mdp-mailbox-XXXXXX)"
    cp -a "$NANO_PS_PATH"/* "$NF_MDP_TEMP_DIR"/
    cp "$MONO_SYSTEM_DRAWING" "$NF_MDP_TEMP_DIR/System.Drawing.dll"
    NF_MDP_MSBUILDTASK_PATH_EFFECTIVE="$NF_MDP_TEMP_DIR"
  fi
fi

echo "[1/2] Restoring packages for mailbox smoke app..."
/usr/bin/nuget restore "$PROJECT" -PackagesDirectory "$ROOT_DIR/packages"

if [[ -f "$INTEROP_PROJECT" ]]; then
  /usr/bin/nuget restore "$INTEROP_PROJECT" -PackagesDirectory "$ROOT_DIR/packages"
fi

required_refs=(
  "$ROOT_DIR/packages/nanoFramework.CoreLibrary.1.17.11/lib/mscorlib.dll"
)

missing_refs=0
for ref in "${required_refs[@]}"; do
  if [[ ! -f "$ref" ]]; then
    missing_refs=1
    break
  fi
done

if [[ "$missing_refs" -eq 1 ]]; then
  echo "[info] Restored packages incomplete; installing MailboxSmoke dependencies..."
  /usr/bin/nuget install nanoFramework.CoreLibrary -Version 1.17.11 -OutputDirectory "$ROOT_DIR/packages"
fi

echo "[2/2] Building mailbox smoke app ($CONFIGURATION)..."
/usr/local/bin/msbuild "$PROJECT" \
  /t:Build \
  -p:Configuration="$CONFIGURATION" \
  "-p:NanoFrameworkProjectSystemPath=$NANO_PS_PATH" \
  "-p:NF_MDP_MSBUILDTASK_PATH=$NF_MDP_MSBUILDTASK_PATH_EFFECTIVE" \
  -verbosity:minimal

OUTPUT_DIR="$(dirname "$PROJECT")/bin/$CONFIGURATION"
TARGET_NAME="$(basename "$PROJECT" .nfproj)"
OUTPUT_BIN="$OUTPUT_DIR/$TARGET_NAME.bin"
PRIMARY_BIN="$OUTPUT_DIR/$TARGET_NAME.bin"
PRIMARY_PE="$OUTPUT_DIR/$TARGET_NAME.pe"
RUNTIME_EVENTS_PE=""

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

if [[ ! -f "$OUTPUT_BIN" && -f "$PRIMARY_BIN" ]]; then
  cp -f "$PRIMARY_BIN" "$OUTPUT_BIN"
fi

if [[ ! -f "$OUTPUT_BIN" ]]; then
  if [[ -f "$OUTPUT_DIR/mscorlib.pe" && -f "$PRIMARY_PE" ]]; then
    pack_args=(
      --required-marker NFMRK1
      --out-dir "$OUTPUT_DIR"
      --out-base "${TARGET_NAME}_bundle"
      "$PRIMARY_PE"
    )

    if [[ -f "$OUTPUT_DIR/DiSEqC_Control.Interop.pe" ]]; then
      pack_args+=("$OUTPUT_DIR/DiSEqC_Control.Interop.pe")
    fi

    if [[ -n "$RUNTIME_EVENTS_PE" && -f "$RUNTIME_EVENTS_PE" ]]; then
      pack_args+=("$RUNTIME_EVENTS_PE")
    fi

    pack_args+=("$OUTPUT_DIR/mscorlib.pe")

    "$SCRIPT_DIR/pack-and-validate.sh" "${pack_args[@]}" >/dev/null

    if [[ -L "$OUTPUT_DIR/latest.deploy.bin" || -f "$OUTPUT_DIR/latest.deploy.bin" ]]; then
      cp -f "$OUTPUT_DIR/latest.deploy.bin" "$OUTPUT_BIN"
      echo "Created fallback deployment bundle: $OUTPUT_BIN"
    fi
  fi
fi

echo "Mailbox smoke app build succeeded."
