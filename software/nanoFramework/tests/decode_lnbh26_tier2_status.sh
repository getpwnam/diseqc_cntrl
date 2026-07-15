#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/phase_a_result_codec.sh"

usage() {
  cat << 'EOF'
Usage:
  ./tests/decode_lnbh26_tier2_status.sh <status_word_hex>
  ./tests/decode_lnbh26_tier2_status.sh --from-swd [path/to/nanoCLR.elf]

Examples:
  ./tests/decode_lnbh26_tier2_status.sh 0xD5C60107
  ./tests/decode_lnbh26_tier2_status.sh --from-swd build/nanoCLR.elf

Format:
  0xD5SSRRDD
  SS = stage, RR = result, DD = detail
EOF
}

stage_label() {
  local stage="$1"
  case "$stage" in
    0xC0) echo "START" ;;
    0xC1) echo "LNB_INIT" ;;
    0xC2) echo "LNB_ENABLE" ;;
    0xC3) echo "LNB_SET_VOLTAGE" ;;
    0xC4) echo "LNB_SET_POLARIZATION" ;;
    0xC5) echo "LNB_SET_BAND" ;;
    0xC6) echo "LNB_READ_STATUS" ;;
    0xCF) echo "FINAL" ;;
    *) echo "UNKNOWN_STAGE" ;;
  esac
}

decode_lnb_status_register() {
  local value_dec="$1"
  local ocp=$(( value_dec & 0x01 ))
  local otp=$(( (value_dec >> 1) & 0x01 ))
  local vmon=$(( (value_dec >> 2) & 0x01 ))

  printf '  LNB status bits: OCP=%s OTP=%s VMON=%s\n' "$ocp" "$otp" "$vmon"
}

decode_final_detail() {
  local detail_hex="$1"
  case "$detail_hex" in
    0xC1) echo "failed at LNB_INIT" ;;
    0xC2) echo "failed at LNB_ENABLE" ;;
    0xC3) echo "failed at LNB_SET_VOLTAGE" ;;
    0xC4) echo "failed at LNB_SET_POLARIZATION" ;;
    0xC5) echo "failed at LNB_SET_BAND" ;;
    0xC6) echo "failed at LNB_READ_STATUS" ;;
    0xFF) echo "all stages passed" ;;
    *) echo "stage marker/detail unknown" ;;
  esac
}

parse_word() {
  local word="$1"

  if [[ ! "$word" =~ ^0[xX][0-9a-fA-F]{8}$ ]]; then
    echo "Invalid status word '$word'. Expected 0x + 8 hex digits." >&2
    exit 2
  fi

  local value_dec=$((word))
  local magic=$(( (value_dec >> 24) & 0xFF ))
  local stage=$(( (value_dec >> 16) & 0xFF ))
  local result=$(( (value_dec >> 8) & 0xFF ))
  local detail=$(( value_dec & 0xFF ))

  local stage_hex
  local detail_hex
  stage_hex=$(printf '0x%02X' "$stage")
  detail_hex=$(printf '0x%02X' "$detail")

  local result_label
  if ! result_label="$(phase_a_result_label "$result")"; then
    result_label="INVALID"
  fi

  printf 'Raw: %s\n' "$word"
  printf '  Magic: 0x%02X\n' "$magic"
  printf '  Stage: %d (%s %s)\n' "$stage" "$stage_hex" "$(stage_label "$stage_hex")"
  printf '  Result: %d (%s)\n' "$result" "$result_label"
  printf '  Detail: %d (%s)\n' "$detail" "$detail_hex"

  if [[ "$magic" -ne $((0xD5)) ]]; then
    echo "  WARN: magic is not 0xD5; word may be from a non-status mailbox source." >&2
    return 0
  fi

  if [[ "$stage_hex" == "0xC6" ]]; then
    decode_lnb_status_register "$detail"
  fi

  if [[ "$stage_hex" == "0xCF" ]]; then
    printf '  Final detail: %s\n' "$(decode_final_detail "$detail_hex")"
  fi
}

read_from_swd() {
  local elf_path="${1:-}"
  local cmd=("$SCRIPT_DIR/swd_read_bringup_status.sh")
  if [[ -n "$elf_path" ]]; then
    cmd+=("$elf_path")
  fi

  local swd_out
  local swd_rc=0
  set +e
  swd_out=$("${cmd[@]}" 2>&1)
  swd_rc=$?
  set -e
  printf '%s\n' "$swd_out"

  if [[ "$swd_rc" -ne 0 ]]; then
    echo "WARN: swd_read_bringup_status.sh exited non-zero ($swd_rc); attempting best-effort decode from captured output." >&2
  fi

  local current_word
  current_word=$(printf '%s\n' "$swd_out" | sed -n 's/^Current status raw: \(0x[0-9A-Fa-f]\+\)$/\1/p' | head -n1)

  if [[ -z "$current_word" ]]; then
    echo "Failed to parse current status from swd_read_bringup_status.sh output." >&2
    exit 1
  fi

  echo
  echo "Decoded current status:"
  parse_word "$current_word"
}

main() {
  if [[ $# -lt 1 ]]; then
    usage
    exit 2
  fi

  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --from-swd)
      read_from_swd "${2:-}"
      ;;
    *)
      parse_word "$1"
      ;;
  esac
}

main "$@"
