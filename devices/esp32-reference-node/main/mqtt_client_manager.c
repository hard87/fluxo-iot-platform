#include "app_config.h"
#include "mqtt_client_manager.h"
#include "esp_event.h"
#include "esp_log.h"
#include "mqtt_client.h"

static const char *TAG = "mqtt_manager";
static esp_mqtt_client_handle_t s_mqtt_client;
static bool s_is_connected;

static void mqtt_event_handler(
    void *handler_args,
    esp_event_base_t base,
    int32_t event_id,
    void *event_data)
{
    (void)handler_args;
    (void)base;
    (void)event_data;

    switch ((esp_mqtt_event_id_t)event_id)
    {
        case MQTT_EVENT_CONNECTED:
            s_is_connected = true;
            ESP_LOGI(TAG, "MQTT conectado.");
            break;
        case MQTT_EVENT_DISCONNECTED:
            s_is_connected = false;
            ESP_LOGW(TAG, "MQTT desconectado.");
            break;
        case MQTT_EVENT_ERROR:
            ESP_LOGE(TAG, "MQTT erro recebido.");
            break;
        default:
            break;
    }
}

esp_err_t mqtt_client_manager_start(void)
{
    if (s_mqtt_client != NULL)
        return ESP_OK;

    esp_mqtt_client_config_t mqtt_cfg = {
        .broker.address.uri = APP_MQTT_BROKER_URI,
        .credentials.client_id = APP_DEVICE_ID,
        .network.disable_auto_reconnect = false,
        .session.keepalive = 60
    };

    s_mqtt_client = esp_mqtt_client_init(&mqtt_cfg);
    if (s_mqtt_client == NULL)
        return ESP_FAIL;

    esp_mqtt_client_register_event(
        s_mqtt_client,
        MQTT_EVENT_ANY,
        mqtt_event_handler,
        NULL);

    esp_err_t err = esp_mqtt_client_start(s_mqtt_client);
    if (err != ESP_OK)
    {
        ESP_LOGE(TAG, "Falha ao iniciar MQTT client: %s", esp_err_to_name(err));
        return err;
    }

    ESP_LOGI(TAG, "MQTT manager iniciado. Broker: %s", APP_MQTT_BROKER_URI);
    return ESP_OK;
}

bool mqtt_client_manager_is_connected(void)
{
    return s_is_connected;
}

esp_err_t mqtt_client_manager_publish(
    const char *topic,
    const char *payload,
    int qos,
    int retain)
{
    if (topic == NULL || payload == NULL)
        return ESP_ERR_INVALID_ARG;

    if (s_mqtt_client == NULL || !s_is_connected)
        return ESP_ERR_INVALID_STATE;

    int msg_id = esp_mqtt_client_publish(
        s_mqtt_client,
        topic,
        payload,
        0,
        qos,
        retain);

    if (msg_id < 0)
        return ESP_FAIL;

    return ESP_OK;
}
