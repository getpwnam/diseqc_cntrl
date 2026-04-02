#!/usr/bin/env bash
set -euo pipefail

BROKER_HOST="${1:-mqtt.ebnx.net}"
BROKER_PORT="${2:-1883}"
TOPIC_FILTER="${3:-#}"

if ! command -v mosquitto_sub >/dev/null 2>&1; then
  echo "ERROR: mosquitto_sub not found in PATH"
  echo "Install mqtt clients, e.g. on Debian/Ubuntu: sudo apt-get install -y mosquitto-clients"
  exit 2
fi

echo "Watching MQTT topics"
echo "  host: $BROKER_HOST"
echo "  port: $BROKER_PORT"
echo "  filter: $TOPIC_FILTER"
echo "Press Ctrl+C to stop."

mosquitto_sub -h "$BROKER_HOST" -p "$BROKER_PORT" -t "$TOPIC_FILTER" -v
