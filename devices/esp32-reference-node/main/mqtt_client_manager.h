#ifndef MQTT_CLIENT_MANAGER_H
#define MQTT_CLIENT_MANAGER_H

#include <stdbool.h>
#include "esp_err.h"

esp_err_t mqtt_client_manager_start(void);
bool mqtt_client_manager_is_connected(void);
esp_err_t mqtt_client_manager_publish(
    const char *topic,
    const char *payload,
    int qos,
    int retain);

#endif
