/**
 * @file board_cubley.h
 * @brief Custom board configuration for DiSEqC Controller
 * 
 * Board: Custom STM32F407VGT6 DiSEqC Controller
 * Features:
 * - LNBH26 DiSEqC driver
 * - STM32F4 RMII Ethernet MAC + external PHY
 * - 8MHz HSE crystal
 */

#ifndef BOARD_DISEQC_H
#define BOARD_DISEQC_H

/*
 * Board identifier
 */
#define BOARD_M0DMF_CUBLEY_F407
#define BOARD_NAME                  "DiSEqC Controller STM32F407VGT6"

/*
 * USB OTG_FS configuration.
 *
 * The board exposes USB VBUS to PA9 only through R31 (100k pull-up to VBUS,
 * no divider/pull-down). That impedance is too high to reliably trip the
 * STM32F407 OTG_FS A/B-session VBUS comparators, so VBUSASEN/VBUSBSEN never
 * report VBUS valid, the core never asserts the D+ pull-up, and the host
 * sees no enumeration (Windows reports Code 43).
 *
 * Defining BOARD_OTG_NOVBUSSENS makes ChibiOS program GCCFG with NOVBUSSENS
 * set (and VBUSASEN/VBUSBSEN cleared) inside usb_lld_start(), which is the
 * supported way to handle a self-powered device whose USB cable can be
 * plugged in after power-up. Doing it here (before halInit/usbStart) avoids
 * the race that comes from patching GCCFG after the OTG core has already
 * been brought up with VBUS-sense enabled.
 */
#define BOARD_OTG_NOVBUSSENS

/*
 * Ethernet PHY type (required by ChibiOS MAC driver when networking is enabled)
 */
#define BOARD_PHY_ID                MII_LAN8742A_ID
#define BOARD_PHY_RMII

/*
 * Board oscillators-related settings
 */
#if !defined(STM32_LSECLK)
#define STM32_LSECLK                32768U
#endif

#if !defined(STM32_HSECLK)
#define STM32_HSECLK                8000000U  // 8MHz external crystal
#endif

/*
 * Board voltages
 * Required for performance limits calculation
 */
#define STM32_VDD                   330U

/*
 * MCU type as defined in the ST header
 */
#define STM32F407xx

/*
 * Default wire protocol serial channel.
 *
 * board.h may be included before halconf.h is fully processed, so this
 * fallback is unconditional. Translation units that need the USB-CDC
 * driver instead include usbcfg.h after halconf.h, which #undefs and
 * redefines SERIAL_DRIVER to SDU1.
 */
#define SERIAL_DRIVER               SD3

/*
 * DiSEqC Configuration
 */
#define DISEQC_PWM_DRIVER           PWMD4    // TIM4 for DiSEqC carrier
#define DISEQC_GPT_DRIVER           GPTD5    // TIM5 for bit timing
#define DISEQC_OUTPUT_LINE          PAL_LINE(GPIOD, 12U)  // PD12 = TIM4_CH1

// Note: No motor enable pin - LNBH26 handles power control automatically
// DiSEqC commands control rotor movement directly

/*
 * LNB Control Configuration (LNBH26PQR via I2C)
 * 
 * LNBH26PQR is controlled via I2C interface:
 * - I2C3: PA8 (SCL), PC9 (SDA)
 * - I2C Address: 0x08 (7-bit)
 * 
 * The LNBH26PQR controls:
 * - Voltage selection (13V/18V) - Register bit VSEL
 * - 22kHz tone enable/disable - Register bit TONE
 * - DiSEqC mode - Register bit DISEQC
 * - Current limiting and protection
 */
#define LNB_I2C_DRIVER             I2CD3    // I2C3 bus
#define LNB_I2C_ADDRESS            0x08     // LNBH26PQR I2C address (7-bit)

/*
 * RMII Ethernet Configuration (hardware v0.5)
 * PA1  = REF_CLK
 * PA2  = MDIO
 * PA7  = CRS_DV
 * PC1  = MDC
 * PC4  = RXD0
 * PC5  = RXD1
 * PB11 = TX_EN
 * PB12 = TXD0
 * PB13 = TXD1
 */
#define ETH_RMII_REF_CLK_LINE       PAL_LINE(GPIOA, 1U)
#define ETH_RMII_MDIO_LINE          PAL_LINE(GPIOA, 2U)
#define ETH_RMII_CRS_DV_LINE        PAL_LINE(GPIOA, 7U)
#define ETH_RMII_MDC_LINE           PAL_LINE(GPIOC, 1U)
#define ETH_RMII_RXD0_LINE          PAL_LINE(GPIOC, 4U)
#define ETH_RMII_RXD1_LINE          PAL_LINE(GPIOC, 5U)
#define ETH_RMII_TX_EN_LINE         PAL_LINE(GPIOB, 11U)
#define ETH_RMII_TXD0_LINE          PAL_LINE(GPIOB, 12U)
#define ETH_RMII_TXD1_LINE          PAL_LINE(GPIOB, 13U)

/*
 * Rotator helper signal mapping (feature bring-up)
 */
#define ROTATOR_PWM_A_LINE          PAL_LINE(GPIOD, 14U) // TIM4_CH3
#define ROTATOR_PWM_B_LINE          PAL_LINE(GPIOD, 15U) // TIM4_CH4
#define ROTATOR_ADC_A_LINE          PAL_LINE(GPIOA, 4U)  // ADC1_IN4
#define ROTATOR_ADC_B_LINE          PAL_LINE(GPIOA, 5U)  // ADC1_IN5

/*
 * IO pins assignments
 */
#define GPIOA_PIN0                  0U  // Adjust to your schematic
#define GPIOA_PIN1                  1U
#define GPIOA_PIN2                  2U  // MDIO
#define GPIOA_PIN3                  3U
#define GPIOA_PIN4                  4U
#define GPIOA_PIN5                  5U
#define GPIOA_PIN6                  6U
#define GPIOA_PIN7                  7U
#define GPIOA_PIN8                  8U  // I2C3_SCL (LNBH26)
#define GPIOA_PIN9                  9U
#define GPIOA_PIN10                 10U
#define GPIOA_PIN11                 11U // USB_DM (if used)
#define GPIOA_PIN12                 12U // USB_DP (if used)
#define GPIOA_PIN13                 13U // SWDIO
#define GPIOA_PIN14                 14U // SWCLK
#define GPIOA_PIN15                 15U

#define GPIOB_PIN0                  0U
#define GPIOB_PIN2                  2U
#define GPIOB_PIN3                  3U
#define GPIOB_PIN4                  4U
#define GPIOB_PIN5                  5U
#define GPIOB_PIN6                  6U  // I2C1_SCL
#define GPIOB_PIN7                  7U  // I2C1_SDA
#define GPIOB_PIN8                  8U
#define GPIOB_PIN9                  9U
#define GPIOB_PIN10                 10U
#define GPIOB_PIN11                 11U // RMII_TX_EN
#define GPIOB_PIN12                 12U // RMII_TXD0
#define GPIOB_PIN13                 13U // RMII_TXD1
#define GPIOB_PIN14                 14U
#define GPIOB_PIN15                 15U

#define GPIOC_PIN0                  0U
#define GPIOC_PIN1                  1U
#define GPIOC_PIN2                  2U
#define GPIOC_PIN3                  3U
#define GPIOC_PIN4                  4U  // RMII_RXD0
#define GPIOC_PIN5                  5U  // RMII_RXD1
#define GPIOC_PIN6                  6U
#define GPIOC_PIN7                  7U
#define GPIOC_PIN8                  8U  // LNB_FLT
#define GPIOC_PIN9                  9U  // I2C3_SDA (LNBH26)
#define GPIOC_PIN10                 10U
#define GPIOC_PIN11                 11U
#define GPIOC_PIN12                 12U
#define GPIOC_PIN13                 13U
#define GPIOC_PIN14                 14U
#define GPIOC_PIN15                 15U

#define GPIOD_PIN0                  0U
#define GPIOD_PIN1                  1U
#define GPIOD_PIN2                  2U
#define GPIOD_PIN3                  3U
#define GPIOD_PIN4                  4U
#define GPIOD_PIN5                  5U
#define GPIOD_PIN6                  6U
#define GPIOD_PIN7                  7U
#define GPIOD_PIN8                  8U  // USART3_TX
#define GPIOD_PIN9                  9U  // USART3_RX
#define GPIOD_PIN10                 10U
#define GPIOD_PIN11                 11U
#define GPIOD_PIN12                 12U  // TIM4_CH1 (DiSEqC output)
#define GPIOD_PIN13                 13U
#define GPIOD_PIN14                 14U // TIM4_CH3
#define GPIOD_PIN15                 15U // TIM4_CH4

/*
 * I/O ports initial setup, this configuration is established soon after reset
 * in the initialization code
 */
#define PIN_MODE_INPUT(n)           (0U << ((n) * 2U))
#define PIN_MODE_OUTPUT(n)          (1U << ((n) * 2U))
#define PIN_MODE_ALTERNATE(n)       (2U << ((n) * 2U))
#define PIN_MODE_ANALOG(n)          (3U << ((n) * 2U))
#define PIN_ODR_LOW(n)              (0U << (n))
#define PIN_ODR_HIGH(n)             (1U << (n))
#define PIN_OTYPE_PUSHPULL(n)       (0U << (n))
#define PIN_OTYPE_OPENDRAIN(n)      (1U << (n))
#define PIN_OSPEED_VERYLOW(n)       (0U << ((n) * 2U))
#define PIN_OSPEED_LOW(n)           (1U << ((n) * 2U))
#define PIN_OSPEED_MEDIUM(n)        (2U << ((n) * 2U))
#define PIN_OSPEED_HIGH(n)          (3U << ((n) * 2U))
#define PIN_PUPDR_FLOATING(n)       (0U << ((n) * 2U))
#define PIN_PUPDR_PULLUP(n)         (1U << ((n) * 2U))
#define PIN_PUPDR_PULLDOWN(n)       (2U << ((n) * 2U))
#define PIN_AFIO_AF(n, v)           ((v) << (((n) % 8U) * 4U))

/*
 * GPIOA setup:
 * PA1  - Alternate RMII_REF_CLK
 * PA2  - Alternate RMII_MDIO
 * PA4  - Analog ADC1_IN4
 * PA5  - Analog ADC1_IN5
 * PA7  - Alternate RMII_CRS_DV
 * PA8  - Alternate I2C3_SCL
 */
#define VAL_GPIOA_MODER             (PIN_MODE_ALTERNATE(GPIOA_PIN1) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN2) |           \
                                     PIN_MODE_ANALOG(GPIOA_PIN4) |              \
                                     PIN_MODE_ANALOG(GPIOA_PIN5) |              \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN7) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN8) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN11) |          \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN12) |          \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN13) |          \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN14))
#define VAL_GPIOA_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOA_PIN1) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN2) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN7) |           \
                                     PIN_OTYPE_OPENDRAIN(GPIOA_PIN8))
#define VAL_GPIOA_OSPEEDR           (PIN_OSPEED_HIGH(GPIOA_PIN1) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN2) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN7) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN11) |             \
                                     PIN_OSPEED_HIGH(GPIOA_PIN12))
#define VAL_GPIOA_PUPDR             (PIN_PUPDR_FLOATING(GPIOA_PIN1) |           \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN2) |           \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN7) |           \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN8))
#define VAL_GPIOA_ODR               (0x00000000)
#define VAL_GPIOA_AFRL              (PIN_AFIO_AF(GPIOA_PIN1, 11U) |             \
                                     PIN_AFIO_AF(GPIOA_PIN2, 11U) |             \
                                     PIN_AFIO_AF(GPIOA_PIN7, 11U))
#define VAL_GPIOA_AFRH              (PIN_AFIO_AF(GPIOA_PIN8, 4U) |              \
                                     PIN_AFIO_AF(GPIOA_PIN11, 10U) |            \
                                     PIN_AFIO_AF(GPIOA_PIN12, 10U) |            \
                                     PIN_AFIO_AF(GPIOA_PIN13, 0U) |             \
                                     PIN_AFIO_AF(GPIOA_PIN14, 0U))

/*
 * GPIOB setup:
 * PB0  - Output (LED_STATUS)
 * PB6  - Alternate I2C1_SCL
 * PB7  - Alternate I2C1_SDA
 * PB11 - Alternate RMII_TX_EN
 * PB12 - Alternate RMII_TXD0
 * PB13 - Alternate RMII_TXD1
 */
#define VAL_GPIOB_MODER             (PIN_MODE_OUTPUT(GPIOB_PIN0) |              \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN6) |           \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN7) |           \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN11) |          \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN12) |          \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN13))
#define VAL_GPIOB_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOB_PIN0) |           \
                                     PIN_OTYPE_OPENDRAIN(GPIOB_PIN6) |          \
                                     PIN_OTYPE_OPENDRAIN(GPIOB_PIN7) |          \
                                     PIN_OTYPE_PUSHPULL(GPIOB_PIN11) |          \
                                     PIN_OTYPE_PUSHPULL(GPIOB_PIN12) |          \
                                     PIN_OTYPE_PUSHPULL(GPIOB_PIN13))
#define VAL_GPIOB_OSPEEDR           (PIN_OSPEED_LOW(GPIOB_PIN0) |               \
                                     PIN_OSPEED_HIGH(GPIOB_PIN6) |              \
                                     PIN_OSPEED_HIGH(GPIOB_PIN7) |              \
                                     PIN_OSPEED_HIGH(GPIOB_PIN11) |             \
                                     PIN_OSPEED_HIGH(GPIOB_PIN12) |             \
                                     PIN_OSPEED_HIGH(GPIOB_PIN13))
#define VAL_GPIOB_PUPDR             (PIN_PUPDR_FLOATING(GPIOB_PIN0) |           \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN6) |             \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN7) |             \
                                     PIN_PUPDR_FLOATING(GPIOB_PIN11) |          \
                                     PIN_PUPDR_FLOATING(GPIOB_PIN12) |          \
                                     PIN_PUPDR_FLOATING(GPIOB_PIN13))
#define VAL_GPIOB_ODR               (PIN_ODR_LOW(GPIOB_PIN0))
#define VAL_GPIOB_AFRL              (PIN_AFIO_AF(GPIOB_PIN6, 4U) |              \
                                     PIN_AFIO_AF(GPIOB_PIN7, 4U))
#define VAL_GPIOB_AFRH              (PIN_AFIO_AF(GPIOB_PIN11, 11U) |            \
                                     PIN_AFIO_AF(GPIOB_PIN12, 11U) |            \
                                     PIN_AFIO_AF(GPIOB_PIN13, 11U))

/*
 * GPIOC setup:
 * PC1 - Alternate RMII_MDC
 * PC4 - Alternate RMII_RXD0
 * PC5 - Alternate RMII_RXD1
 * PC8 - Input (LNB_FLT)
 * PC9 - Alternate I2C3_SDA
 */
#define VAL_GPIOC_MODER             (PIN_MODE_ALTERNATE(GPIOC_PIN1) |           \
                                     PIN_MODE_ALTERNATE(GPIOC_PIN4) |           \
                                     PIN_MODE_ALTERNATE(GPIOC_PIN5) |           \
                                     PIN_MODE_INPUT(GPIOC_PIN8) |               \
                                     PIN_MODE_ALTERNATE(GPIOC_PIN9))
#define VAL_GPIOC_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOC_PIN1) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOC_PIN4) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOC_PIN5) |           \
                                     PIN_OTYPE_OPENDRAIN(GPIOC_PIN9))
#define VAL_GPIOC_OSPEEDR           (PIN_OSPEED_HIGH(GPIOC_PIN1) |              \
                                     PIN_OSPEED_HIGH(GPIOC_PIN4) |              \
                                     PIN_OSPEED_HIGH(GPIOC_PIN5) |              \
                                     PIN_OSPEED_HIGH(GPIOC_PIN9))
#define VAL_GPIOC_PUPDR             (PIN_PUPDR_FLOATING(GPIOC_PIN1) |           \
                                     PIN_PUPDR_FLOATING(GPIOC_PIN4) |           \
                                     PIN_PUPDR_FLOATING(GPIOC_PIN5) |           \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN8) |             \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN9))
#define VAL_GPIOC_ODR               (0x00000000)
#define VAL_GPIOC_AFRL              (PIN_AFIO_AF(GPIOC_PIN1, 11U) |             \
                                     PIN_AFIO_AF(GPIOC_PIN4, 11U) |             \
                                     PIN_AFIO_AF(GPIOC_PIN5, 11U))
#define VAL_GPIOC_AFRH              (PIN_AFIO_AF(GPIOC_PIN9, 4U))

/*
 * GPIOD setup:
 * PD8  - Alternate USART3_TX
 * PD9  - Alternate USART3_RX
 * PD12 - Alternate TIM4_CH1 (DiSEqC output)
 * PD14 - Alternate TIM4_CH3 (rotator PWM A)
 * PD15 - Alternate TIM4_CH4 (rotator PWM B)
 */
#define VAL_GPIOD_MODER             (PIN_MODE_ALTERNATE(GPIOD_PIN8) |           \
                                     PIN_MODE_ALTERNATE(GPIOD_PIN9) |           \
                                     PIN_MODE_ALTERNATE(GPIOD_PIN12) |          \
                                     PIN_MODE_ALTERNATE(GPIOD_PIN14) |          \
                                     PIN_MODE_ALTERNATE(GPIOD_PIN15))
#define VAL_GPIOD_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOD_PIN8) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOD_PIN9) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOD_PIN12) |          \
                                     PIN_OTYPE_PUSHPULL(GPIOD_PIN14) |          \
                                     PIN_OTYPE_PUSHPULL(GPIOD_PIN15))
#define VAL_GPIOD_OSPEEDR           (PIN_OSPEED_HIGH(GPIOD_PIN8) |              \
                                     PIN_OSPEED_HIGH(GPIOD_PIN9) |              \
                                     PIN_OSPEED_HIGH(GPIOD_PIN12) |             \
                                     PIN_OSPEED_HIGH(GPIOD_PIN14) |             \
                                     PIN_OSPEED_HIGH(GPIOD_PIN15))
#define VAL_GPIOD_PUPDR             (PIN_PUPDR_FLOATING(GPIOD_PIN8) |           \
                                     PIN_PUPDR_FLOATING(GPIOD_PIN9) |           \
                                     PIN_PUPDR_FLOATING(GPIOD_PIN12) |          \
                                     PIN_PUPDR_FLOATING(GPIOD_PIN14) |          \
                                     PIN_PUPDR_FLOATING(GPIOD_PIN15))
#define VAL_GPIOD_ODR               (0x00000000)
#define VAL_GPIOD_AFRL              (0x00000000)
#define VAL_GPIOD_AFRH              (PIN_AFIO_AF(GPIOD_PIN8, 7U) |              \
                                     PIN_AFIO_AF(GPIOD_PIN9, 7U) |              \
                                     PIN_AFIO_AF(GPIOD_PIN12, 2U) |             \
                                     PIN_AFIO_AF(GPIOD_PIN14, 2U) |             \
                                     PIN_AFIO_AF(GPIOD_PIN15, 2U))

#if !defined(_FROM_ASM_)
#ifdef __cplusplus
extern "C" {
#endif
  void boardInit(void);
#ifdef __cplusplus
}
#endif
#endif /* _FROM_ASM_ */

#endif /* BOARD_DISEQC_H */
