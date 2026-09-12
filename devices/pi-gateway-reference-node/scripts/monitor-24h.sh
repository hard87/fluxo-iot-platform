#!/bin/sh
set -eu

BASE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
INTERVAL_SECONDS="${FLUXO_MONITOR_INTERVAL_SECONDS:-300}"
DURATION_SECONDS="${FLUXO_MONITOR_DURATION_SECONDS:-86400}"
LOG_DIR="${FLUXO_MONITOR_LOG_DIR:-/home/junior/.node-red/fluxo-gateway/monitoring}"
started="$(date +%s)"
deadline="$((started + DURATION_SECONDS))"
mkdir -p "$LOG_DIR"
chmod 700 "$LOG_DIR"
log="$LOG_DIR/gateway-24h-$(date -u +%Y%m%dT%H%M%SZ).log"

while [ "$(date +%s)" -lt "$deadline" ]; do
  {
    echo "=== $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
    "$BASE_DIR/scripts/diagnostics.sh"
    pid="$(systemctl show nodered -p MainPID --value)"
    [ "$pid" -gt 0 ] && ps -o pid=,rss=,%cpu=,etime= -p "$pid" || true
  } >> "$log" 2>&1
  sleep "$INTERVAL_SECONDS"
done

echo "24-hour local monitoring completed: $log"
