/**
 * @file board_diseqc.h
 * @brief Custom board configuration for DiSEqC Controller
 * 
 * Board: Custom STM32F407VGT6 DiSEqC Controller
 * Features:
 * - LNBH26 DiSEqC driver
 * - W5500 Ethernet
 * - 8MHz HSE crystal
 */

#ifndef BOARD_DISEQC_H
#define BOARD_DISEQC_H

/*
 * Board identifier
 */
#define BOARD_M0DMF_DISEQC_F407
#define BOARD_NAME                  "DiSEqC Controller STM32F407VGT6"

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
 * Default wire protocol serial channel (nanoBooter/nanoCLR)
 */
#define SERIAL_DRIVER               SD2

/*
 * DiSEqC Configuration
 */
#define DISEQC_PWM_DRIVER           PWMD4    // TIM4 for DiSEqC carrier
#define DISEQC_GPT_DRIVER           GPTD5    // TIM5 for bit timing
#define DISEQC_OUTPUT_LINE          PAL_LINE(GPIOD, 12U)  // PD12 = TIM4_CH1
#define MOTOR_ENABLE_LINE           PAL_LINE(GPIOB, 1U)

// Note: No motor enable pin - LNBH26 handles power control automatically
// DiSEqC commands control rotor movement directly

/*
 * LNB Control Configuration (LNBH26PQR via I2C)
 * 
 * LNBH26PQR is controlled via I2C interface:
 * - I2C1: PB8 (SCL), PB9 (SDA)
 * - I2C Address: 0x08 (7-bit)
 * 
 * The LNBH26PQR controls:
 * - Voltage selection (13V/18V) - Register bit VSEL
 * - 22kHz tone enable/disable - Register bit TONE
 * - DiSEqC mode - Register bit DISEQC
 * - Current limiting and protection
 */
#define LNB_I2C_DRIVER             I2CD1    // I2C1 bus
#define LNB_I2C_ADDRESS            0x08     // LNBH26PQR I2C address (7-bit)

/*
 * W5500 Ethernet Configuration
 * Based on diseqc_cntrl schematic
 * SPI1: PA5 (SCK), PA6 (MISO), PA7 (MOSI)
 * Control: PA4 (CS/SCSN), PC4 (RST), PC5 (INT)
 */
#define W5500_SPI_DRIVER            SPID1               // SPI1
#define W5500_CS_LINE               PAL_LINE(GPIOA, 4U) // PA4 = SCSN (Chip Select)
#define W5500_RESET_LINE            PAL_LINE(GPIOC, 4U) // PC4 = W5500_RST
#define W5500_INT_LINE              PAL_LINE(GPIOC, 5U) // PC5 = W5500_INT

/*
 * IO pins assignments
 */
#define GPIOA_PIN0                  0U  // Adjust to your schematic
#define GPIOA_PIN1                  1U
#define GPIOA_PIN2                  2U  // USART2_TX (debug)
#define GPIOA_PIN3                  3U  // USART2_RX (debug)
#define GPIOA_PIN4                  4U  // W5500 CS
#define GPIOA_PIN5                  5U  // SPI1_SCK
#define GPIOA_PIN6                  6U  // SPI1_MISO
#define GPIOA_PIN7                  7U  // SPI1_MOSI
#define GPIOA_PIN8                  8U
#define GPIOA_PIN9                  9U
#define GPIOA_PIN10                 10U
#define GPIOA_PIN11                 11U // USB_DM (if used)
#define GPIOA_PIN12                 12U // USB_DP (if used)
#define GPIOA_PIN13                 13U // SWDIO
#define GPIOA_PIN14                 14U // SWCLK
#define GPIOA_PIN15                 15U

#define GPIOB_PIN0                  0U
#define GPIOB_PIN1                  1U  // NC (Not Connected)
#define GPIOB_PIN2                  2U
#define GPIOB_PIN3                  3U
#define GPIOB_PIN4                  4U
#define GPIOB_PIN5                  5U
#define GPIOB_PIN6                  6U
#define GPIOB_PIN7                  7U
#define GPIOB_PIN8                  8U  // I2C1_SCL
#define GPIOB_PIN9                  9U  // I2C1_SDA
#define GPIOB_PIN10                 10U
#define GPIOB_PIN11                 11U
#define GPIOB_PIN12                 12U
#define GPIOB_PIN13                 13U
#define GPIOB_PIN14                 14U
#define GPIOB_PIN15                 15U

#define GPIOC_PIN0                  0U  // W5500 Reset
#define GPIOC_PIN1                  1U  // W5500 Int
#define GPIOC_PIN2                  2U
#define GPIOC_PIN3                  3U
#define GPIOC_PIN4                  4U
#define GPIOC_PIN5                  5U
#define GPIOC_PIN6                  6U
#define GPIOC_PIN7                  7U
#define GPIOC_PIN8                  8U
#define GPIOC_PIN9                  9U
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
#define GPIOD_PIN8                  8U
#define GPIOD_PIN9                  9U
#define GPIOD_PIN10                 10U
#define GPIOD_PIN11                 11U
#define GPIOD_PIN12                 12U  // TIM4_CH1 (DiSEqC output)
#define GPIOD_PIN13                 13U
#define GPIOD_PIN14                 14U
#define GPIOD_PIN15                 15U

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
 * PA4  - Output (W5500 CS)
 * PA5  - Alternate SPI1_SCK
 * PA6  - Alternate SPI1_MISO
 * PA7  - Alternate SPI1_MOSI
 * PA2  - Alternate USART2_TX (debug)
 * PA3  - Alternate USART2_RX (debug)
 */
#define VAL_GPIOA_MODER             (PIN_MODE_OUTPUT(GPIOA_PIN4) |              \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN5) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN6) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN7) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN2) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN3) |           \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN13) |          \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN14))
#define VAL_GPIOA_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOA_PIN4) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN2) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN3) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN5) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN6) |           \
                                     PIN_OTYPE_PUSHPULL(GPIOA_PIN7))
#define VAL_GPIOA_OSPEEDR           (PIN_OSPEED_HIGH(GPIOA_PIN4) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN5) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN6) |              \
                                     PIN_OSPEED_HIGH(GPIOA_PIN7))
#define VAL_GPIOA_PUPDR             (PIN_PUPDR_FLOATING(GPIOA_PIN4) |           \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN5) |           \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN6) |           \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN7))
#define VAL_GPIOA_ODR               (PIN_ODR_HIGH(GPIOA_PIN4))
#define VAL_GPIOA_AFRL              (PIN_AFIO_AF(GPIOA_PIN2, 7U) |              \
                                     PIN_AFIO_AF(GPIOA_PIN3, 7U) |              \
                                     PIN_AFIO_AF(GPIOA_PIN5, 5U) |              \
                                     PIN_AFIO_AF(GPIOA_PIN6, 5U) |              \
                                     PIN_AFIO_AF(GPIOA_PIN7, 5U))
#define VAL_GPIOA_AFRH              (PIN_AFIO_AF(GPIOA_PIN13, 0U) |             \
                                     PIN_AFIO_AF(GPIOA_PIN14, 0U))

/*
 * GPIOB setup:
 * PB8 - Alternate I2C1_SCL
 * PB9 - Alternate I2C1_SDA
 */
#define VAL_GPIOB_MODER             (PIN_MODE_ALTERNATE(GPIOB_PIN8) |           \
                                     PIN_MODE_ALTERNATE(GPIOB_PIN9))
#define VAL_GPIOB_OTYPER            (PIN_OTYPE_OPENDRAIN(GPIOB_PIN8) |          \
                                     PIN_OTYPE_OPENDRAIN(GPIOB_PIN9))
#define VAL_GPIOB_OSPEEDR           (PIN_OSPEED_HIGH(GPIOB_PIN8) |              \
                                     PIN_OSPEED_HIGH(GPIOB_PIN9))
#define VAL_GPIOB_PUPDR             (PIN_PUPDR_PULLUP(GPIOB_PIN8) |             \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN9))
#define VAL_GPIOB_ODR               0x00000000
#define VAL_GPIOB_AFRL              0x00000000
#define VAL_GPIOB_AFRH              (PIN_AFIO_AF(GPIOB_PIN8, 4U) |              \
                                     PIN_AFIO_AF(GPIOB_PIN9, 4U))

/*
 * GPIOC setup:
 * PC4 - Output (W5500 Reset)
 * PC5 - Input (W5500 Interrupt)
 */
#define VAL_GPIOC_MODER             (PIN_MODE_OUTPUT(GPIOC_PIN4) |              \
                                     PIN_MODE_INPUT(GPIOC_PIN5))
#define VAL_GPIOC_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOC_PIN4))
#define VAL_GPIOC_OSPEEDR           (PIN_OSPEED_LOW(GPIOC_PIN4))
#define VAL_GPIOC_PUPDR             (PIN_PUPDR_FLOATING(GPIOC_PIN4) |           \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN5))
#define VAL_GPIOC_ODR               (PIN_ODR_LOW(GPIOC_PIN4))
#define VAL_GPIOC_AFRL              (0x00000000)
#define VAL_GPIOC_AFRH              (0x00000000)

/*
 * GPIOD setup:
 * PD12 - Alternate TIM4_CH1 (DiSEqC output)
 */
#define VAL_GPIOD_MODER             (PIN_MODE_ALTERNATE(GPIOD_PIN12))
#define VAL_GPIOD_OTYPER            (PIN_OTYPE_PUSHPULL(GPIOD_PIN12))
#define VAL_GPIOD_OSPEEDR           (PIN_OSPEED_HIGH(GPIOD_PIN12))
#define VAL_GPIOD_PUPDR             (PIN_PUPDR_FLOATING(GPIOD_PIN12))
#define VAL_GPIOD_ODR               (PIN_ODR_LOW(GPIOD_PIN12))
#define VAL_GPIOD_AFRL              (0x00000000)
#define VAL_GPIOD_AFRH              (PIN_AFIO_AF(GPIOD_PIN12, 2U))

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
