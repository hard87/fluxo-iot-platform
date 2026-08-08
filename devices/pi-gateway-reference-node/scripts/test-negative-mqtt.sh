#!/bin/sh
set -eu

ENV_FILE="${FLUXO_GATEWAY_ENV_FILE:-/home/junior/.node-red/environment}"
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

BASE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
node "$BASE_DIR/test-negative-mqtt.js"
