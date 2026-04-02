#!/usr/bin/env bash
set -euo pipefail

BROKER_HOST="${1:-192.168.1.50}"
BROKER_PORT="${2:-1883}"
TOPIC_PREFIX="${3:-diseqc}"
TIMEOUT_SECONDS="${4:-8}"

if ! command -v mosquitto_pub >/dev/null 2>&1 || ! command -v mosquitto_sub >/dev/null 2>&1; then
  echo "ERROR: mosquitto_pub/mosquitto_sub not found in PATH"
  echo "Install mqtt clients, e.g. on Debian/Ubuntu: sudo apt-get install -y mosquitto-clients"
  exit 2
fi

STATUS_TOPIC="${TOPIC_PREFIX}/status/config/effective/mqtt/broker"
COMMAND_TOPIC="${TOPIC_PREFIX}/command/config/get"
OUTFILE="$(mktemp)"

cleanup() {
  rm -f "$OUTFILE"
}
trap cleanup EXIT

# Listen for a config status value, then trigger config/get command.
(timeout "$TIMEOUT_SECONDS" mosquitto_sub -h "$BROKER_HOST" -p "$BROKER_PORT" -t "$STATUS_TOPIC" -C 1 > "$OUTFILE") &
SUB_PID=$!

sleep 0.2
mosquitto_pub -h "$BROKER_HOST" -p "$BROKER_PORT" -t "$COMMAND_TOPIC" -m ""

if ! wait "$SUB_PID"; then
  echo "ERROR: no config status response received"
  echo "  broker: $BROKER_HOST:$BROKER_PORT"
  echo "  expected topic: $STATUS_TOPIC"
  echo "  Ensure the DiSEqC controller app is connected and subscribed."
  exit 1
fi

VALUE="$(cat "$OUTFILE")"
if [[ -z "$VALUE" ]]; then
  echo "ERROR: empty config value received from $STATUS_TOPIC"
  exit 1
fi

echo "OK: device config smoke test passed"
echo "  broker: $BROKER_HOST:$BROKER_PORT"
echo "  command topic: $COMMAND_TOPIC"
echo "  status topic: $STATUS_TOPIC"
echo "  value: $VALUE"
