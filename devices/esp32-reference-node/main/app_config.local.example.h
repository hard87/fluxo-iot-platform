#ifndef APP_CONFIG_LOCAL_EXAMPLE_H
#define APP_CONFIG_LOCAL_EXAMPLE_H

/*
 * Copy this file to app_config.local.h and update local values.
 * This file is versioned only as reference and contains no real secrets.
 */

#undef APP_WIFI_SSID
#define APP_WIFI_SSID               "MY_LAB_WIFI"

#undef APP_WIFI_PASSWORD
#define APP_WIFI_PASSWORD           "MY_LAB_WIFI_PASSWORD"

#undef APP_MQTT_BROKER_URI
#define APP_MQTT_BROKER_URI         "mqtts://broker.fluxo.local:8883"

#undef APP_MQTT_USERNAME
#define APP_MQTT_USERNAME           "dev-acme-industria-11111111-esp32-lab-01"

#undef APP_MQTT_PASSWORD
#define APP_MQTT_PASSWORD           "PASTE_DEVICE_SECRET_FROM_PROVISIONING"

#undef APP_MQTT_CA_CERT_PEM
#define APP_MQTT_CA_CERT_PEM        "-----BEGIN CERTIFICATE-----\nPASTE_CA_CRT_CONTENT_HERE\n-----END CERTIFICATE-----\n"

#endif
