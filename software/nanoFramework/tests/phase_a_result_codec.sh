#!/usr/bin/env bash

# Shared Phase A status-word decode helpers.
# Format: 0xD5SSRRDD

phase_a_result_contract_summary() {
  echo "ENTER=0 [entry/running] PASS=1 WARN=2 [aggregate/skipped] FAIL=14 EXCEPTION=15"
}

phase_a_result_label() {
  local result="$1"
  case "$result" in
    0) echo "ENTER" ;;
    1) echo "PASS" ;;
    2) echo "WARN" ;;
    14) echo "FAIL" ;;
    15) echo "EXCEPTION" ;;
    *) return 1 ;;
  esac
}

phase_a_component_label() {
  local detail="$1"
  case "$detail" in
    1) echo "UART_WIRE_PROTOCOL" ;;
    2) echo "USB_CDC_SERIAL" ;;
    3) echo "LED_GPIO" ;;
    4) echo "FRAM" ;;
    5) echo "LNBH26" ;;
    6) echo "W5500" ;;
    7) echo "DISEQC_TRANSMIT" ;;
    8) echo "DIAGNOSTICS_MAILBOX" ;;
    *) return 1 ;;
  esac
}
