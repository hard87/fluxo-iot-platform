#!/bin/sh
set -eu

BASE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
ENV_FILE="${FLUXO_GATEWAY_ENV_FILE:-/home/junior/.node-red/environment}"
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

echo "Node-RED: $(systemctl is-active nodered)"
echo "Uptime/load: $(uptime)"
free -h
df -h / /home
if [ -r /sys/class/thermal/thermal_zone0/temp ]; then
  awk '{printf "CPU temperature: %.1f C\n", $1/1000}' /sys/class/thermal/thermal_zone0/temp
fi
node "$BASE_DIR/scripts/gateway-spool.js" status
