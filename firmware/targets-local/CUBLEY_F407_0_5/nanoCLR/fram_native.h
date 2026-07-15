#ifndef CUBLEY_FRAM_NATIVE_H
#define CUBLEY_FRAM_NATIVE_H

#include <hal.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum
{
    FRAM_OK = 0,
    FRAM_ERROR_INVALID_PARAM = 1,
    FRAM_ERROR_NOT_INITIALIZED = 2,
    FRAM_ERROR_I2C = 3
} fram_status_t;

typedef struct
{
    I2CDriver *i2c_driver;
    uint8_t i2c_addr;
    bool initialized;
} fram_handle_t;

typedef struct
{
    int32_t status;
    int32_t detail;
} fram_last_error_t;

fram_status_t fram_init(fram_handle_t *hfram, I2CDriver *i2c_driver, uint8_t i2c_addr);
fram_status_t fram_write(fram_handle_t *hfram, uint16_t address, const uint8_t *data, uint16_t count);
fram_status_t fram_read(fram_handle_t *hfram, uint16_t address, uint8_t *data, uint16_t count);

fram_handle_t *fram_get_global_handle(void);
int32_t fram_get_last_i2c_msg(void);
int32_t fram_get_capacity(void);
fram_last_error_t fram_get_last_error(void);

// Interop-ready wrappers (native-only for now):
// 1. NativeInit()
int32_t fram_native_init(void);
// 2. NativeRead(int address, byte[] buffer, int offset, int count)
int32_t fram_native_read(int32_t address, uint8_t *buffer, int32_t bufferLength, int32_t offset, int32_t count);
// 3. NativeWrite(int address, byte[] buffer, int offset, int count)
int32_t fram_native_write(int32_t address, const uint8_t *buffer, int32_t bufferLength, int32_t offset, int32_t count);
// 4. NativeGetCapacity()
int32_t fram_native_get_capacity(void);
// 5. NativeGetLastError()
int32_t fram_native_get_last_error(void);

#ifdef __cplusplus
}
#endif

#endif // CUBLEY_FRAM_NATIVE_H
