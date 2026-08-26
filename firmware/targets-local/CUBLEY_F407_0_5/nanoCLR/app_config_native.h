#ifndef CUBLEY_APP_CONFIG_NATIVE_H
#define CUBLEY_APP_CONFIG_NATIVE_H

#include <stddef.h>
#include <stdint.h>

#define CUBLEY_APP_CONFIG_RECORD_SIZE 512u

typedef enum
{
    CUBLEY_APP_CONFIG_OK = 0,
    CUBLEY_APP_CONFIG_INVALID_PARAM = 1,
    CUBLEY_APP_CONFIG_STORAGE_UNAVAILABLE = 2,
    CUBLEY_APP_CONFIG_LAYOUT_CONFLICT = 3,
    CUBLEY_APP_CONFIG_ERASE_FAILED = 4,
    CUBLEY_APP_CONFIG_WRITE_FAILED = 5,
    CUBLEY_APP_CONFIG_VERIFY_FAILED = 6
} cubley_app_config_status_t;

cubley_app_config_status_t cubley_app_config_read(uint8_t *data, size_t count);
cubley_app_config_status_t cubley_app_config_write(const uint8_t *data, size_t count);

#endif