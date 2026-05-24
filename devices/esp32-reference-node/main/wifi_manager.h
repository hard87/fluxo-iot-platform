#ifndef WIFI_MANAGER_H
#define WIFI_MANAGER_H

#include <stdbool.h>
#include "freertos/FreeRTOS.h"
#include "esp_err.h"

esp_err_t wifi_manager_start(void);
bool wifi_manager_is_connected(void);
bool wifi_manager_wait_for_connection(TickType_t timeout_ticks);
esp_err_t wifi_manager_get_rssi(int *out_rssi);

#endif
