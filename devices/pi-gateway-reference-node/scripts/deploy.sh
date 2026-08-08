#!/bin/sh
set -eu

SOURCE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
TARGET_DIR="${FLUXO_GATEWAY_INSTALL_DIR:-/home/junior/.node-red/fluxo-gateway}"
ENV_FILE="${FLUXO_GATEWAY_ENV_FILE:-/home/junior/.node-red/environment}"

"$SOURCE_DIR/scripts/validate-environment.sh"
mkdir -p "$TARGET_DIR/scripts" "$TARGET_DIR/docs" "$TARGET_DIR/systemd" "$TARGET_DIR/state/spool"
chmod 700 "$TARGET_DIR" "$TARGET_DIR/state" "$TARGET_DIR/state/spool"
cp "$SOURCE_DIR/flow.json" "$TARGET_DIR/flow.json"
cp "$SOURCE_DIR/scripts/gateway-spool.js" "$SOURCE_DIR/scripts/deploy-flow.js" "$SOURCE_DIR/scripts/diagnostics.sh" "$SOURCE_DIR/scripts/validate-environment.sh" "$SOURCE_DIR/scripts/test-negative-mqtt.js" "$SOURCE_DIR/scripts/test-negative-mqtt.sh" "$SOURCE_DIR/scripts/monitor-24h.sh" "$SOURCE_DIR/scripts/monitor-continuous.sh" "$TARGET_DIR/scripts/"
cp "$SOURCE_DIR/docs/troubleshooting.md" "$TARGET_DIR/docs/"
cp "$SOURCE_DIR/systemd/fluxo-gateway-monitor.service" "$TARGET_DIR/systemd/"
chmod 700 "$TARGET_DIR/scripts/"*.js "$TARGET_DIR/scripts/"*.sh

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
sudo systemctl stop nodered
trap 'sudo systemctl start nodered' EXIT INT TERM
node "$TARGET_DIR/scripts/deploy-flow.js"
sudo systemctl start nodered
trap - EXIT INT TERM
sudo install -m 0644 "$TARGET_DIR/systemd/fluxo-gateway-monitor.service" /etc/systemd/system/fluxo-gateway-monitor.service
sudo systemctl daemon-reload
sudo systemctl enable --now fluxo-gateway-monitor.service
echo "Deployment completed in $TARGET_DIR"
