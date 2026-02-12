/**
 * @file lnb_control.h
 * @brief LNB (Low Noise Block) Control for LNBH26PQR via I2C
 * 
 * The LNBH26PQR is controlled via I2C interface:
 * - Voltage selection (13V/18V) for polarization
 * - 22kHz tone for band selection
 * - Current limiting and protection
 * 
 * I2C Address: 0x08 (7-bit address)
 * I2C Bus: I2C1 (PB8=SCL, PB9=SDA)
 * 
 * Register Map:
 * - Register 0x00: Control register (VSEL, Tone, Enable, etc.)
 * - Register 0x01: Status register (Overcurrent, Temperature, etc.)
 */

#ifndef LNB_CONTROL_H
#define LNB_CONTROL_H

#include <hal.h>
#include <ch.h>
#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* LNBH26PQR I2C Configuration */
#define LNBH26_I2C_ADDR             0x08        // 7-bit I2C address
#define LNBH26_REG_CONTROL          0x00        // Control register
#define LNBH26_REG_STATUS           0x01        // Status register

/* Control Register Bits */
#define LNBH26_CTRL_EN              (1 << 0)    // Enable LNB power
#define LNBH26_CTRL_VSEL            (1 << 1)    // Voltage select (0=13V, 1=18V)
#define LNBH26_CTRL_TONE            (1 << 2)    // 22kHz tone enable
#define LNBH26_CTRL_DISEQC          (1 << 3)    // DiSEqC mode enable
#define LNBH26_CTRL_ILIM_600MA      (0 << 4)    // Current limit 600mA
#define LNBH26_CTRL_ILIM_400MA      (1 << 4)    // Current limit 400mA

/* Status Register Bits */
#define LNBH26_STAT_OCP             (1 << 0)    // Overcurrent protection triggered
#define LNBH26_STAT_OTP             (1 << 1)    // Over-temperature protection
#define LNBH26_STAT_VMON            (1 << 2)    // Voltage monitor

/* LNB Voltage Selection */
typedef enum {
    LNB_VOLTAGE_13V = 0,    // Vertical polarization
    LNB_VOLTAGE_18V = 1     // Horizontal polarization
} lnb_voltage_t;

/* LNB Polarization (maps to voltage) */
typedef enum {
    LNB_POL_VERTICAL = 0,   // 13V
    LNB_POL_HORIZONTAL = 1  // 18V
} lnb_polarization_t;

/* LNB Band Selection */
typedef enum {
    LNB_BAND_LOW = 0,       // 10.7-11.7 GHz (no 22kHz tone)
    LNB_BAND_HIGH = 1       // 11.7-12.75 GHz (22kHz tone enabled)
} lnb_band_t;

/* LNB Configuration */
typedef struct {
    I2CDriver *i2c_driver;      // I2C driver (I2CD1)
    uint8_t i2c_addr;           // I2C address (0x08)
    lnb_voltage_t voltage;      // Current voltage setting
    bool tone_enabled;          // Current tone state
    bool enabled;               // LNB power enabled
    uint8_t control_reg;        // Shadow of control register
} lnb_handle_t;

/* Status codes */
typedef enum {
    LNB_OK = 0,
    LNB_ERROR_INVALID_PARAM = 1,
    LNB_ERROR_NOT_INITIALIZED = 2,
    LNB_ERROR_I2C = 3
} lnb_status_t;

/**
 * @brief Initialize LNB control
 * @param hlnb LNB handle
 * @param i2c_driver I2C driver (I2CD1)
 * @param i2c_addr I2C address (0x08)
 * @return LNB_OK on success
 */
lnb_status_t lnb_init(lnb_handle_t *hlnb, 
                      I2CDriver *i2c_driver,
                      uint8_t i2c_addr);

/**
 * @brief Set LNB voltage (13V or 18V)
 * @param hlnb LNB handle
 * @param voltage Voltage selection
 * @return LNB_OK on success
 */
lnb_status_t lnb_set_voltage(lnb_handle_t *hlnb, lnb_voltage_t voltage);

/**
 * @brief Set LNB polarization (convenience function)
 * @param hlnb LNB handle
 * @param polarization Polarization selection
 * @return LNB_OK on success
 */
lnb_status_t lnb_set_polarization(lnb_handle_t *hlnb, lnb_polarization_t polarization);

/**
 * @brief Enable/disable 22kHz tone
 * @param hlnb LNB handle
 * @param enable true to enable, false to disable
 * @return LNB_OK on success
 */
lnb_status_t lnb_set_tone(lnb_handle_t *hlnb, bool enable);

/**
 * @brief Set LNB band (convenience function)
 * @param hlnb LNB handle
 * @param band Band selection
 * @return LNB_OK on success
 */
lnb_status_t lnb_set_band(lnb_handle_t *hlnb, lnb_band_t band);

/**
 * @brief Enable/disable LNB power
 * @param hlnb LNB handle
 * @param enable true to enable, false to disable
 * @return LNB_OK on success
 */
lnb_status_t lnb_set_enable(lnb_handle_t *hlnb, bool enable);

/**
 * @brief Get current voltage setting
 * @param hlnb LNB handle
 * @return Current voltage
 */
lnb_voltage_t lnb_get_voltage(lnb_handle_t *hlnb);

/**
 * @brief Get current tone state
 * @param hlnb LNB handle
 * @return true if tone enabled
 */
bool lnb_get_tone(lnb_handle_t *hlnb);

/**
 * @brief Get current polarization
 * @param hlnb LNB handle
 * @return Current polarization
 */
lnb_polarization_t lnb_get_polarization(lnb_handle_t *hlnb);

/**
 * @brief Get current band
 * @param hlnb LNB handle
 * @return Current band
 */
lnb_band_t lnb_get_band(lnb_handle_t *hlnb);

/**
 * @brief Read status register
 * @param hlnb LNB handle
 * @param status Pointer to store status byte
 * @return LNB_OK on success
 */
lnb_status_t lnb_read_status(lnb_handle_t *hlnb, uint8_t *status);

/**
 * @brief Get global LNB handle (for C# interop)
 * @return Pointer to global handle
 */
lnb_handle_t* lnb_get_global_handle(void);

#ifdef __cplusplus
}
#endif

#endif /* LNB_CONTROL_H */
