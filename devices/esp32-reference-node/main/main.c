#include "app_config.h"
#include "mqtt_client_manager.h"
#include "telemetry_service.h"
#include "wifi_manager.h"
#include "esp_err.h"
#include "esp_log.h"
#include "nvs_flash.h"

static const char *TAG = "fluxo_ref_node";

static esp_err_t initialize_nvs(void)
{
    esp_err_t ret = nvs_flash_init();
    if (ret == ESP_ERR_NVS_NO_FREE_PAGES || ret == ESP_ERR_NVS_NEW_VERSION_FOUND)
    {
        ESP_ERROR_CHECK(nvs_flash_erase());
        ret = nvs_flash_init();
    }

    return ret;
}

void app_main(void)
{
    ESP_ERROR_CHECK(initialize_nvs());
    ESP_LOGI(TAG, "Inicializando firmware de referencia do Fluxo...");

    ESP_ERROR_CHECK(wifi_manager_start());
    ESP_ERROR_CHECK(mqtt_client_manager_start());
    ESP_ERROR_CHECK(telemetry_service_start());

    ESP_LOGI(
        TAG,
        "Node pronto. Publicacao periodica a cada %d ms para %s",
        APP_TELEMETRY_PERIOD_MS,
        APP_MQTT_BROKER_URI);
}
