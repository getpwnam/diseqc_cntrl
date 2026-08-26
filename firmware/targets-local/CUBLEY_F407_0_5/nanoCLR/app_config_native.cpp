#include "app_config_native.h"

#include <nanoHAL.h>
#include <nanoPAL_BlockStorage.h>
#include <string.h>

static uint8_t *cubley_app_config_address()
{
    return ((uint8_t *)&__nanoConfig_end__) - CUBLEY_APP_CONFIG_RECORD_SIZE;
}

static bool cubley_app_config_region_available(const uint8_t *address)
{
    if (address[0] == 'C' && address[1] == 'C' && address[2] == 'F' && address[3] == 'G')
    {
        return true;
    }

    for (size_t index = 0; index < CUBLEY_APP_CONFIG_RECORD_SIZE; index++)
    {
        if (address[index] != 0xFFu)
        {
            return false;
        }
    }

    return true;
}

cubley_app_config_status_t cubley_app_config_read(uint8_t *data, size_t count)
{
    if (data == NULL || count != CUBLEY_APP_CONFIG_RECORD_SIZE)
    {
        return CUBLEY_APP_CONFIG_INVALID_PARAM;
    }

    uint8_t *storageAddress = cubley_app_config_address();
    if (!cubley_app_config_region_available(storageAddress))
    {
        return CUBLEY_APP_CONFIG_LAYOUT_CONFLICT;
    }

    memcpy(data, storageAddress, CUBLEY_APP_CONFIG_RECORD_SIZE);
    return CUBLEY_APP_CONFIG_OK;
}

cubley_app_config_status_t cubley_app_config_write(const uint8_t *data, size_t count)
{
    if (data == NULL || count != CUBLEY_APP_CONFIG_RECORD_SIZE)
    {
        return CUBLEY_APP_CONFIG_INVALID_PARAM;
    }

    BlockStorageDevice *device = BlockStorageList_GetFirstDevice();
    if (device == NULL)
    {
        return CUBLEY_APP_CONFIG_STORAGE_UNAVAILABLE;
    }

    uint8_t *configStart = (uint8_t *)&__nanoConfig_start__;
    uint8_t *configEnd = (uint8_t *)&__nanoConfig_end__;
    uint8_t *storageAddress = cubley_app_config_address();
    size_t configSize = (size_t)(configEnd - configStart);
    if (configSize < CUBLEY_APP_CONFIG_RECORD_SIZE || !cubley_app_config_region_available(storageAddress))
    {
        return CUBLEY_APP_CONFIG_LAYOUT_CONFLICT;
    }

    uint8_t *sectorCopy = (uint8_t *)platform_malloc(configSize);
    if (sectorCopy == NULL)
    {
        return CUBLEY_APP_CONFIG_STORAGE_UNAVAILABLE;
    }

    memcpy(sectorCopy, configStart, configSize);
    memcpy(sectorCopy + configSize - CUBLEY_APP_CONFIG_RECORD_SIZE, data, CUBLEY_APP_CONFIG_RECORD_SIZE);

    if (!BlockStorageDevice_EraseBlock(device, (ByteAddress)configStart))
    {
        platform_free(sectorCopy);
        return CUBLEY_APP_CONFIG_ERASE_FAILED;
    }

    if (!BlockStorageDevice_Write(device, (ByteAddress)configStart, configSize, sectorCopy, true))
    {
        platform_free(sectorCopy);
        return CUBLEY_APP_CONFIG_WRITE_FAILED;
    }

    platform_free(sectorCopy);
    if (memcmp(storageAddress, data, CUBLEY_APP_CONFIG_RECORD_SIZE) != 0)
    {
        return CUBLEY_APP_CONFIG_VERIFY_FAILED;
    }

    return CUBLEY_APP_CONFIG_OK;
}