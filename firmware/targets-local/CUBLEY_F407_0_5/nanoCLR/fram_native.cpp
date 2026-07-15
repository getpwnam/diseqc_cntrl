#include "fram_native.h"

#include <string.h>

#define FRAM_I2C_TIMEOUT_MS 100
#define FRAM_MAX_TRANSFER_BYTES 32
#define FRAM_TOTAL_BYTES 2048u
#define FRAM_DEFAULT_I2C_ADDR 0x50u

static fram_handle_t g_fram;
static bool g_fram_initialized = false;
static int32_t g_fram_last_i2c_msg = 0;
static fram_last_error_t g_fram_last_error = {0, 0};

// Use static SRAM-backed buffers because STM32F4 I2C DMA cannot use CCM stack.
static uint8_t g_fram_tx_buf[FRAM_MAX_TRANSFER_BYTES + 2];
static uint8_t g_fram_rx_buf[FRAM_MAX_TRANSFER_BYTES];

static const I2CConfig g_fram_i2c_config = {
    OPMODE_I2C,
    100000,
    STD_DUTY_CYCLE
};

static void fram_set_last_error(int32_t status, int32_t detail)
{
    g_fram_last_error.status = status;
    g_fram_last_error.detail = detail;
}

static bool fram_range_is_valid(uint16_t address, uint16_t count)
{
    // 16 kbit FRAM -> 2048-byte address space [0x0000..0x07FF].
    if (count == 0u)
    {
        return false;
    }

    if ((uint32_t)address >= FRAM_TOTAL_BYTES)
    {
        return false;
    }

    // Overflow-safe end check.
    return ((uint32_t)address + (uint32_t)count) <= FRAM_TOTAL_BYTES;
}

static void fram_configure_i2c1_pins(void)
{
    palSetLineMode(PAL_LINE(GPIOB, 6U), PAL_MODE_ALTERNATE(4) | PAL_STM32_OTYPE_OPENDRAIN | PAL_STM32_PUPDR_PULLUP);
    palSetLineMode(PAL_LINE(GPIOB, 7U), PAL_MODE_ALTERNATE(4) | PAL_STM32_OTYPE_OPENDRAIN | PAL_STM32_PUPDR_PULLUP);
}

static void fram_prepare_i2c_bus(I2CDriver *i2c_driver)
{
    if (i2c_driver == &I2CD1)
    {
        fram_configure_i2c1_pins();
    }

    i2cStart(i2c_driver, &g_fram_i2c_config);
}

fram_status_t fram_init(fram_handle_t *hfram, I2CDriver *i2c_driver, uint8_t i2c_addr)
{
    if (hfram == NULL || i2c_driver == NULL)
    {
        g_fram_last_i2c_msg = -127;
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -127);
        return FRAM_ERROR_INVALID_PARAM;
    }

    // FRAM bring-up is intentionally locked to I2C1 + 24C16/24xx-compatible addr.
    if (i2c_driver != &I2CD1 || i2c_addr != FRAM_DEFAULT_I2C_ADDR)
    {
        g_fram_last_i2c_msg = -126;
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -126);
        return FRAM_ERROR_INVALID_PARAM;
    }

    memset(hfram, 0, sizeof(fram_handle_t));
    hfram->i2c_driver = i2c_driver;
    hfram->i2c_addr = i2c_addr;

    fram_prepare_i2c_bus(i2c_driver);

    // Probe by reading one byte from address 0x0000.
    g_fram_tx_buf[0] = 0x00;
    g_fram_tx_buf[1] = 0x00;

    msg_t status = i2cMasterTransmitTimeout(
        hfram->i2c_driver,
        hfram->i2c_addr,
        g_fram_tx_buf,
        2,
        g_fram_rx_buf,
        1,
        TIME_MS2I(FRAM_I2C_TIMEOUT_MS));

    g_fram_last_i2c_msg = (int32_t)status;

    if (status != MSG_OK)
    {
        fram_set_last_error(FRAM_ERROR_I2C, g_fram_last_i2c_msg);
        return FRAM_ERROR_I2C;
    }

    hfram->initialized = true;
    g_fram = *hfram;
    g_fram_initialized = true;
    fram_set_last_error(FRAM_OK, 0);

    return FRAM_OK;
}

fram_status_t fram_write(fram_handle_t *hfram, uint16_t address, const uint8_t *data, uint16_t count)
{
    if (hfram == NULL || data == NULL)
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -125);
        return FRAM_ERROR_INVALID_PARAM;
    }

    if (!g_fram_initialized || !hfram->initialized)
    {
        fram_set_last_error(FRAM_ERROR_NOT_INITIALIZED, 0);
        return FRAM_ERROR_NOT_INITIALIZED;
    }

    if (!fram_range_is_valid(address, count))
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, (int32_t)address);
        return FRAM_ERROR_INVALID_PARAM;
    }

    uint16_t remaining = count;
    const uint8_t *cursor = data;
    uint16_t current = address;

    while (remaining > 0)
    {
        uint16_t chunk = (remaining > FRAM_MAX_TRANSFER_BYTES) ? FRAM_MAX_TRANSFER_BYTES : remaining;

        g_fram_tx_buf[0] = (uint8_t)((current >> 8) & 0xFF);
        g_fram_tx_buf[1] = (uint8_t)(current & 0xFF);
        memcpy(&g_fram_tx_buf[2], cursor, chunk);

        msg_t status = i2cMasterTransmitTimeout(
            hfram->i2c_driver,
            hfram->i2c_addr,
            g_fram_tx_buf,
            (size_t)chunk + 2,
            NULL,
            0,
            TIME_MS2I(FRAM_I2C_TIMEOUT_MS));

        g_fram_last_i2c_msg = (int32_t)status;

        if (status != MSG_OK)
        {
            fram_set_last_error(FRAM_ERROR_I2C, g_fram_last_i2c_msg);
            return FRAM_ERROR_I2C;
        }

        current = (uint16_t)(current + chunk);
        cursor += chunk;
        remaining = (uint16_t)(remaining - chunk);
    }

    fram_set_last_error(FRAM_OK, 0);
    return FRAM_OK;
}

fram_status_t fram_read(fram_handle_t *hfram, uint16_t address, uint8_t *data, uint16_t count)
{
    if (hfram == NULL || data == NULL)
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -124);
        return FRAM_ERROR_INVALID_PARAM;
    }

    if (!g_fram_initialized || !hfram->initialized)
    {
        fram_set_last_error(FRAM_ERROR_NOT_INITIALIZED, 0);
        return FRAM_ERROR_NOT_INITIALIZED;
    }

    if (!fram_range_is_valid(address, count))
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, (int32_t)address);
        return FRAM_ERROR_INVALID_PARAM;
    }

    uint16_t remaining = count;
    uint8_t *cursor = data;
    uint16_t current = address;

    while (remaining > 0)
    {
        uint16_t chunk = (remaining > FRAM_MAX_TRANSFER_BYTES) ? FRAM_MAX_TRANSFER_BYTES : remaining;

        g_fram_tx_buf[0] = (uint8_t)((current >> 8) & 0xFF);
        g_fram_tx_buf[1] = (uint8_t)(current & 0xFF);

        msg_t status = i2cMasterTransmitTimeout(
            hfram->i2c_driver,
            hfram->i2c_addr,
            g_fram_tx_buf,
            2,
            g_fram_rx_buf,
            chunk,
            TIME_MS2I(FRAM_I2C_TIMEOUT_MS));

        g_fram_last_i2c_msg = (int32_t)status;

        if (status != MSG_OK)
        {
            fram_set_last_error(FRAM_ERROR_I2C, g_fram_last_i2c_msg);
            return FRAM_ERROR_I2C;
        }

        memcpy(cursor, g_fram_rx_buf, chunk);
        current = (uint16_t)(current + chunk);
        cursor += chunk;
        remaining = (uint16_t)(remaining - chunk);
    }

    fram_set_last_error(FRAM_OK, 0);
    return FRAM_OK;
}

fram_handle_t *fram_get_global_handle(void)
{
    return &g_fram;
}

int32_t fram_get_last_i2c_msg(void)
{
    return g_fram_last_i2c_msg;
}

int32_t fram_get_capacity(void)
{
    return (int32_t)FRAM_TOTAL_BYTES;
}

fram_last_error_t fram_get_last_error(void)
{
    return g_fram_last_error;
}

int32_t fram_native_init(void)
{
    return (int32_t)fram_init(fram_get_global_handle(), &I2CD1, FRAM_DEFAULT_I2C_ADDR);
}

int32_t fram_native_read(int32_t address, uint8_t *buffer, int32_t bufferLength, int32_t offset, int32_t count)
{
    if (buffer == NULL || address < 0 || address > 0xFFFF || offset < 0 || count <= 0 || bufferLength < 0)
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -123);
        return (int32_t)FRAM_ERROR_INVALID_PARAM;
    }

    if (offset > bufferLength || count > (bufferLength - offset))
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -122);
        return (int32_t)FRAM_ERROR_INVALID_PARAM;
    }

    return (int32_t)fram_read(fram_get_global_handle(), (uint16_t)address, buffer + offset, (uint16_t)count);
}

int32_t fram_native_write(int32_t address, const uint8_t *buffer, int32_t bufferLength, int32_t offset, int32_t count)
{
    if (buffer == NULL || address < 0 || address > 0xFFFF || offset < 0 || count <= 0 || bufferLength < 0)
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -121);
        return (int32_t)FRAM_ERROR_INVALID_PARAM;
    }

    if (offset > bufferLength || count > (bufferLength - offset))
    {
        fram_set_last_error(FRAM_ERROR_INVALID_PARAM, -120);
        return (int32_t)FRAM_ERROR_INVALID_PARAM;
    }

    return (int32_t)fram_write(fram_get_global_handle(), (uint16_t)address, buffer + offset, (uint16_t)count);
}

int32_t fram_native_get_capacity(void)
{
    fram_set_last_error(FRAM_OK, 0);
    return fram_get_capacity();
}

int32_t fram_native_get_last_error(void)
{
    return g_fram_last_error.status;
}
