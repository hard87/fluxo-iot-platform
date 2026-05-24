#ifndef APP_CONFIG_H
#define APP_CONFIG_H

/*
 * Centralized firmware configuration.
 * Note: "localhost" in broker URI will point to ESP32 itself.
 * Use the machine IP where your MQTT broker is running.
 */
#define APP_WIFI_SSID               "CHANGE_ME_WIFI_SSID"
#define APP_WIFI_PASSWORD           "CHANGE_ME_WIFI_PASSWORD"

#define APP_MQTT_BROKER_URI         "mqtt://CHANGE_ME_BROKER_IP:1883"
#define APP_MQTT_QOS                1
#define APP_MQTT_RETAIN             0

#define APP_SCHEMA_VERSION          "1.0"
#define APP_TENANT_ID               "acme-industria"
#define APP_WORKSPACE_ID            "11111111-1111-1111-1111-111111111111"
#define APP_DEVICE_ID               "esp32-lab-01"
#define APP_MESSAGE_TYPE            "telemetry"
#define APP_FIRMWARE_VERSION        "0.1.0"

#define APP_TOPIC_FORMAT "fluxo/tenants/%s/workspaces/%s/devices/%s/telemetry"

#define APP_TELEMETRY_PERIOD_MS     10000

#define APP_NTP_SERVER              "pool.ntp.org"
#define APP_TIME_SYNC_TIMEOUT_MS    30000

#endif
