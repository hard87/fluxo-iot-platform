#!/bin/sh
set -eu

ENV_FILE="${FLUXO_GATEWAY_ENV_FILE:-/home/junior/.node-red/environment}"
[ -r "$ENV_FILE" ] || { echo "Environment file is not readable: $ENV_FILE" >&2; exit 1; }

set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

required="FLUXO_MQTT_HOST FLUXO_MQTT_PORT FLUXO_MQTT_USERNAME FLUXO_MQTT_PASSWORD FLUXO_MQTT_CA_PATH FLUXO_TENANT_ID FLUXO_WORKSPACE_ID FLUXO_DEVICE_ID"
for name in $required; do
  eval "value=\${$name:-}"
  [ -n "$value" ] || { echo "Missing required variable: $name" >&2; exit 1; }
done

[ "$FLUXO_MQTT_PORT" = "8883" ] || { echo "FLUXO_MQTT_PORT must be 8883; insecure fallback is refused." >&2; exit 1; }
[ -r "$FLUXO_MQTT_CA_PATH" ] || { echo "CA file is not readable: $FLUXO_MQTT_CA_PATH" >&2; exit 1; }

mode="$(stat -c '%a' "$ENV_FILE")"
[ "$mode" = "600" ] || { echo "Environment file mode must be 600 (current: $mode)." >&2; exit 1; }

case "$FLUXO_WORKSPACE_ID" in
  ????????-????-????-????-????????????) ;;
  *) echo "FLUXO_WORKSPACE_ID is not a UUID." >&2; exit 1 ;;
esac

echo "Gateway environment is valid (secrets not displayed)."
