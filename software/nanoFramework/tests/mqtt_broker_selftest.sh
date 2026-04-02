#!/usr/bin/env bash
set -euo pipefail

BROKER_HOST="${1:-192.168.1.50}"
BROKER_PORT="${2:-1883}"
TOPIC_BASE="${3:-diseqc/test/selfcheck}"
TIMEOUT_SECONDS="${4:-5}"

if ! command -v mosquitto_pub >/dev/null 2>&1 || ! command -v mosquitto_sub >/dev/null 2>&1; then
  echo "ERROR: mosquitto_pub/mosquitto_sub not found in PATH"
  echo "Install mqtt clients, e.g. on Debian/Ubuntu: sudo apt-get install -y mosquitto-clients"
  exit 2
fi

TOKEN="$(date +%s)-$$"
TOPIC="$TOPIC_BASE/$TOKEN"
PAYLOAD="selftest-$TOKEN"
OUTFILE="$(mktemp)"

cleanup() {
  rm -f "$OUTFILE"
}
trap cleanup EXIT

# Subscribe first, then publish so we do not miss the message.
(timeout "$TIMEOUT_SECONDS" mosquitto_sub -h "$BROKER_HOST" -p "$BROKER_PORT" -t "$TOPIC" -C 1 > "$OUTFILE") &
SUB_PID=$!

sleep 0.2
mosquitto_pub -h "$BROKER_HOST" -p "$BROKER_PORT" -t "$TOPIC" -m "$PAYLOAD"

if ! wait "$SUB_PID"; then
  echo "ERROR: did not receive message from broker within ${TIMEOUT_SECONDS}s"
  exit 1
fi

RECEIVED="$(cat "$OUTFILE")"
if [[ "$RECEIVED" != "$PAYLOAD" ]]; then
  echo "ERROR: payload mismatch"
  echo "  expected: $PAYLOAD"
  echo "  received: $RECEIVED"
  exit 1
fi

echo "OK: broker loopback test passed"
echo "  host: $BROKER_HOST"
echo "  port: $BROKER_PORT"
echo "  topic: $TOPIC"
