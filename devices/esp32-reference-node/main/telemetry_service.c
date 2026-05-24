#include <inttypes.h>
#include <stdio.h>
#include <string.h>
#include <time.h>
#include "app_config.h"
#include "telemetry_service.h"
#include "mqtt_client_manager.h"
#include "wifi_manager.h"
#include "esp_err.h"
#include "esp_log.h"
#include "esp_sntp.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static const char *TAG = "telemetry_service";

static TaskHandle_t s_task_handle;
static uint64_t s_sequence;
static bool s_time_warning_logged;

static bool is_time_synchronized(void)
{
    time_t now = 0;
    struct tm time_info = { 0 };
    time(&now);
    gmtime_r(&now, &time_info);
    return time_info.tm_year >= (2024 - 1900);
}

static void sync_time_with_ntp(void)
{
    esp_sntp_setoperatingmode(SNTP_OPMODE_POLL);
    esp_sntp_setservername(0, APP_NTP_SERVER);
    esp_sntp_init();

    TickType_t waited = 0;
    const TickType_t timeout_ticks = pdMS_TO_TICKS(APP_TIME_SYNC_TIMEOUT_MS);
    const TickType_t step_ticks = pdMS_TO_TICKS(1000);

    while (!is_time_synchronized() && waited < timeout_ticks)
    {
        vTaskDelay(step_ticks);
        waited += step_ticks;
    }

    if (is_time_synchronized())
    {
        ESP_LOGI(TAG, "Relogio sincronizado via NTP.");
    }
    else
    {
        ESP_LOGW(
            TAG,
            "Nao foi possivel sincronizar o relogio no tempo limite. "
            "timestampUtc pode ficar impreciso ate o NTP sincronizar.");
    }
}

static void build_topic(char *out_topic, size_t topic_size)
{
    snprintf(
        out_topic,
        topic_size,
        APP_TOPIC_FORMAT,
        APP_TENANT_ID,
        APP_WORKSPACE_ID,
        APP_DEVICE_ID);
}

static void format_timestamp_utc(char *out_timestamp, size_t timestamp_size, bool *out_is_synced)
{
    time_t now = 0;
    struct tm time_info = { 0 };
    time(&now);
    gmtime_r(&now, &time_info);
    strftime(out_timestamp, timestamp_size, "%Y-%m-%dT%H:%M:%SZ", &time_info);

    if (out_is_synced != NULL)
        *out_is_synced = time_info.tm_year >= (2024 - 1900);
}

static float compute_temperature(uint64_t sequence)
{
    return 24.0f + (float)(sequence % 8) * 0.3f;
}

static float compute_humidity(uint64_t sequence)
{
    return 55.0f + (float)(sequence % 10) * 0.5f;
}

static float compute_battery(uint64_t sequence)
{
    const float simulated_drop = (float)(sequence % 1200) * 0.0005f;
    float value = 3.30f - simulated_drop;
    if (value < 3.10f)
        value = 3.10f;
    return value;
}

static void telemetry_task(void *arg)
{
    (void)arg;

    char topic[256];
    build_topic(topic, sizeof(topic));

    sync_time_with_ntp();

    while (true)
    {
        if (!wifi_manager_is_connected())
        {
            ESP_LOGW(TAG, "Wi-Fi offline. Publicacao pulada.");
            vTaskDelay(pdMS_TO_TICKS(APP_TELEMETRY_PERIOD_MS));
            continue;
        }

        if (!mqtt_client_manager_is_connected())
        {
            ESP_LOGW(TAG, "MQTT offline. Publicacao pulada.");
            vTaskDelay(pdMS_TO_TICKS(APP_TELEMETRY_PERIOD_MS));
            continue;
        }

        int rssi = -120;
        esp_err_t rssi_err = wifi_manager_get_rssi(&rssi);
        if (rssi_err != ESP_OK)
            ESP_LOGW(TAG, "Nao foi possivel obter RSSI: %s", esp_err_to_name(rssi_err));

        uint64_t uptime_sec = (uint64_t)(esp_timer_get_time() / 1000000ULL);
        s_sequence++;

        char timestamp_utc[32];
        bool time_synced = false;
        format_timestamp_utc(timestamp_utc, sizeof(timestamp_utc), &time_synced);
        if (!time_synced && !s_time_warning_logged)
        {
            ESP_LOGW(
                TAG,
                "timestampUtc esta em modo provisorio ate sincronizacao de relogio.");
            s_time_warning_logged = true;
        }

        const float temperature = compute_temperature(s_sequence);
        const float humidity = compute_humidity(s_sequence);
        const float battery = compute_battery(s_sequence);

        char payload[768];
        int written = snprintf(
            payload,
            sizeof(payload),
            "{"
            "\"schemaVersion\":\"%s\","
            "\"tenantId\":\"%s\","
            "\"workspaceId\":\"%s\","
            "\"deviceId\":\"%s\","
            "\"messageType\":\"%s\","
            "\"timestampUtc\":\"%s\","
            "\"sequence\":%" PRIu64 ","
            "\"firmwareVersion\":\"%s\","
            "\"metrics\":{"
            "\"temperature\":%.1f,"
            "\"humidity\":%.1f,"
            "\"battery\":%.2f,"
            "\"rssi\":%d,"
            "\"uptimeSec\":%" PRIu64
            "}"
            "}",
            APP_SCHEMA_VERSION,
            APP_TENANT_ID,
            APP_WORKSPACE_ID,
            APP_DEVICE_ID,
            APP_MESSAGE_TYPE,
            timestamp_utc,
            s_sequence,
            APP_FIRMWARE_VERSION,
            temperature,
            humidity,
            battery,
            rssi,
            uptime_sec);

        if (written <= 0 || written >= (int)sizeof(payload))
        {
            ESP_LOGE(TAG, "Falha ao montar payload JSON.");
            vTaskDelay(pdMS_TO_TICKS(APP_TELEMETRY_PERIOD_MS));
            continue;
        }

        esp_err_t publish_err = mqtt_client_manager_publish(
            topic,
            payload,
            APP_MQTT_QOS,
            APP_MQTT_RETAIN);

        if (publish_err == ESP_OK)
        {
            ESP_LOGI(
                TAG,
                "Telemetria publicada. seq=%" PRIu64 ", rssi=%d dBm, topic=%s",
                s_sequence,
                rssi,
                topic);
        }
        else
        {
            ESP_LOGW(TAG, "Falha ao publicar telemetria: %s", esp_err_to_name(publish_err));
        }

        vTaskDelay(pdMS_TO_TICKS(APP_TELEMETRY_PERIOD_MS));
    }
}

esp_err_t telemetry_service_start(void)
{
    if (s_task_handle != NULL)
        return ESP_OK;

    BaseType_t task_result = xTaskCreate(
        telemetry_task,
        "telemetry_task",
        6144,
        NULL,
        5,
        &s_task_handle);

    if (task_result != pdPASS)
        return ESP_FAIL;

    ESP_LOGI(TAG, "Telemetry service iniciado.");
    return ESP_OK;
}
