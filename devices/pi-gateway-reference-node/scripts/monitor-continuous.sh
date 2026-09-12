#!/bin/sh
set -eu

BASE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
INTERVAL_SECONDS="${FLUXO_MONITOR_INTERVAL_SECONDS:-900}"

case "$INTERVAL_SECONDS" in
  ''|*[!0-9]*)
    echo "FLUXO_MONITOR_INTERVAL_SECONDS must be an integer." >&2
    exit 1
    ;;
esac

if [ "$INTERVAL_SECONDS" -lt 60 ]; then
  echo "FLUXO_MONITOR_INTERVAL_SECONDS must be at least 60 seconds." >&2
  exit 1
fi

while :; do
  echo "=== Fluxo gateway monitor $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
  "$BASE_DIR/scripts/diagnostics.sh"
  pid="$(systemctl show nodered -p MainPID --value)"
  [ "$pid" -gt 0 ] && ps -o pid=,rss=,%cpu=,etime= -p "$pid" || true
  sleep "$INTERVAL_SECONDS"
done
