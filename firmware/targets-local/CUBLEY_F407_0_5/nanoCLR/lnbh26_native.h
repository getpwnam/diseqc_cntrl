#ifndef LNBH26_NATIVE_H
#define LNBH26_NATIVE_H

#include <ch.h>
#include <hal.h>
#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define LNBH26_I2C_ADDR 0x08u
#define LNBH26_I2C_TIMEOUT_MS 100

#define LNBH26_DATA1_VSEL_A_MASK 0x0Fu
#define LNBH26_DATA1_A_DISABLED 0x00u
#define LNBH26_DATA1_A_13V 0x01u
#define LNBH26_DATA1_A_18V 0x08u
#define LNBH26_DATA1_VSEL_B_MASK 0xF0u
#define LNBH26_DATA1_B_DISABLED 0x00u
#define LNBH26_DATA1_B_13V 0x10u
#define LNBH26_DATA1_B_18V 0x80u

#define LNBH26_DATA2_TEN_A (1u << 0)
#define LNBH26_DATA2_LPM_A (1u << 1)
#define LNBH26_DATA2_EXTM_A (1u << 2)
#define LNBH26_DATA2_TEN_B (1u << 4)
#define LNBH26_DATA2_LPM_B (1u << 5)
#define LNBH26_DATA2_EXTM_B (1u << 6)

#define LNBH26_DATA3_ISET_A (1u << 0)
#define LNBH26_DATA3_ISW_A (1u << 1)
#define LNBH26_DATA3_ISET_B (1u << 4)
#define LNBH26_DATA3_ISW_B (1u << 5)

#define LNB_NATIVE_CHANNEL_A 0
#define LNB_NATIVE_CHANNEL_B 1

#define LNB_NATIVE_POLARIZATION_VERTICAL 0
#define LNB_NATIVE_POLARIZATION_HORIZONTAL 1

#define LNB_NATIVE_BAND_LOW 0
#define LNB_NATIVE_BAND_HIGH 1

#define LNB_NATIVE_DISEQC_INPUT_DISABLED 0
#define LNB_NATIVE_DISEQC_INPUT_ENABLED 1

typedef enum
{
    LNB_OK = 0,
    LNB_ERROR_INVALID_PARAM = 1,
    LNB_ERROR_NOT_INITIALIZED = 2,
    LNB_ERROR_I2C = 3,
} lnb_status_t;

typedef enum
{
    LNB_CHANNEL_A = 0,
    LNB_CHANNEL_B = 1,
} lnb_channel_t;

typedef enum
{
    LNB_VOLTAGE_13V = 0,
    LNB_VOLTAGE_18V = 1,
} lnb_voltage_t;

typedef enum
{
    LNB_POL_VERTICAL = 0,
    LNB_POL_HORIZONTAL = 1,
} lnb_polarization_t;

typedef enum
{
    LNB_BAND_LOW = 0,
    LNB_BAND_HIGH = 1,
} lnb_band_t;

typedef enum
{
    LNB_DISEQC_INPUT_DISABLED = 0,
    LNB_DISEQC_INPUT_ENABLED = 1,
} lnb_diseqc_input_mode_t;

typedef enum
{
    LNBH26_REGISTER_STATUS1 = 0x00,
    LNBH26_REGISTER_STATUS2 = 0x01,
    LNBH26_REGISTER_DATA1 = 0x02,
    LNBH26_REGISTER_DATA2 = 0x03,
    LNBH26_REGISTER_DATA3 = 0x04,
    LNBH26_REGISTER_DATA4 = 0x05,
} lnbh26_register_t;

typedef struct
{
    I2CDriver *i2c_driver;
    uint8_t i2c_addr;
    lnb_voltage_t voltage[2];
    bool tone_enabled[2];
    bool low_power_enabled[2];
    lnb_diseqc_input_mode_t diseqc_input_mode[2];
    bool enabled[2];
    uint8_t data1_reg;
    uint8_t data2_reg;
    uint8_t data3_reg;
    uint8_t data4_reg;
} lnb_handle_t;

typedef struct
{
    int32_t status;
    int32_t detail;
} lnb_last_error_t;

lnb_status_t lnb_init(lnb_handle_t *hlnb, I2CDriver *i2c_driver, uint8_t i2c_addr);
lnb_status_t lnb_set_enable(lnb_handle_t *hlnb, bool enable);
lnb_status_t lnb_read_status(lnb_handle_t *hlnb, uint8_t *status);
lnb_status_t lnb_read_status_pair(lnb_handle_t *hlnb, uint8_t *status1, uint8_t *status2);
lnb_status_t lnb_set_polarization(lnb_handle_t *hlnb, lnb_polarization_t polarization);
lnb_status_t lnb_set_band(lnb_handle_t *hlnb, lnb_band_t band);
lnb_status_t lnb_set_enable_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool enable);
lnb_status_t lnb_set_polarization_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_polarization_t polarization);
lnb_status_t lnb_set_band_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_band_t band);
lnb_status_t lnb_set_low_power_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool enable);
lnb_status_t lnb_set_diseqc_input_mode_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, lnb_diseqc_input_mode_t mode);
lnb_status_t lnb_set_iset_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool lowRange);
lnb_status_t lnb_set_isw_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel, bool lowLimit);
int32_t lnb_get_iset_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel);
int32_t lnb_get_isw_low_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel);
lnb_polarization_t lnb_get_polarization(lnb_handle_t *hlnb);
lnb_band_t lnb_get_band(lnb_handle_t *hlnb);
lnb_polarization_t lnb_get_polarization_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel);
lnb_band_t lnb_get_band_for_channel(lnb_handle_t *hlnb, lnb_channel_t channel);
lnb_status_t lnb_read_register_byte(lnb_handle_t *hlnb, uint8_t reg, uint8_t *value);

lnb_handle_t *lnb_get_global_handle(void);
int32_t lnb_get_last_i2c_msg(void);
lnb_last_error_t lnb_get_last_error(void);

// Interop-shaped wrappers (native only for now).
int32_t lnb_native_init(void);
int32_t lnb_native_set_enable(int32_t enable);
int32_t lnb_native_read_status(int32_t *statusRegister);
int32_t lnb_native_read_status_pair(int32_t *status1Register, int32_t *status2Register);
int32_t lnb_native_set_polarization(int32_t polarizationConstant);
int32_t lnb_native_set_band(int32_t bandConstant);
int32_t lnb_native_set_polarization_for_channel(int32_t channelConstant, int32_t polarizationConstant);
int32_t lnb_native_set_band_for_channel(int32_t channelConstant, int32_t bandConstant);
int32_t lnb_native_set_low_power_for_channel(int32_t channelConstant, int32_t enable);
int32_t lnb_native_set_diseqc_input_mode_for_channel(int32_t channelConstant, int32_t modeConstant);
int32_t lnb_native_set_iset_low_for_channel(int32_t channelConstant, int32_t lowRange);
int32_t lnb_native_set_isw_low_for_channel(int32_t channelConstant, int32_t lowLimit);
int32_t lnb_native_get_polarization(void);
int32_t lnb_native_get_band(void);
int32_t lnb_native_get_polarization_for_channel(int32_t channelConstant);
int32_t lnb_native_get_band_for_channel(int32_t channelConstant);
int32_t lnb_native_get_iset_low_for_channel(int32_t channelConstant);
int32_t lnb_native_get_isw_low_for_channel(int32_t channelConstant);
int32_t lnb_native_read_register(int32_t registerAddress, int32_t *registerValue);
int32_t lnb_native_get_last_error(void);
int32_t lnb_native_get_last_error_detail(void);

#ifdef __cplusplus
}
#endif

#endif // LNBH26_NATIVE_H
