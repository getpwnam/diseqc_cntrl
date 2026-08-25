/*
    board.h — Cubley v0.5 (STM32F407VGT6, LQFP100)

    Copyright (c) .NET Foundation and Contributors
    See LICENSE.md file in the project root for full license information.

    Structure derived from the ChibiOS ST_STM32F4_DISCOVERY reference board.

    Cubley board pin ownership is documented in
    docs/debug/BOARD_PIN_OWNERSHIP_AUDIT.md. This file names every board-owned
    pin per that audit but starts Phase 0 with all peripheral pins configured
    as safe "input + pull-up". Subsequent phases flip individual pins to their
    alternate functions as each peripheral is brought up:

      Phase 1  PD8/PD9 USART3 (AF7)              wire protocol @ 921600
      Phase 2  PB0 LED_STATUS (output PP low)    boot double-flash
      Phase 3  PB6/PB7 I2C1 (AF4 OD)             FRAM
      Phase 6  PA8/PC9 I2C3 (AF4 OD), PC8 input  LNBH26 + fault
      Phase 7  PA1/PA2/PA7/PB11-13/PC1/PC4/PC5   RMII to LAN8742A (AF11)
      Later    PD12/PD14/PD15 TIM4 (AF2)         DiSEqC carrier, motor
*/

#ifndef BOARD_H
#define BOARD_H

/*===========================================================================*/
/* Driver constants.                                                         */
/*===========================================================================*/

/*
 * Board identifier.
 */
#define BOARD_CUBLEY_F407_0_5
#define BOARD_NAME                  "Cubley v0.5 (STM32F407VG)"
#define BOARD_PHY_ID                MII_LAN8742A_ID
#define BOARD_PHY_RMII

/*
 * Board oscillators-related settings.
 * NOTE: LSE not fitted on Cubley v0.5.
 */
#if !defined(STM32_LSECLK)
#define STM32_LSECLK                0U
#endif

#if !defined(STM32_HSECLK)
#define STM32_HSECLK                8000000U
#endif

/*
 * Board voltages.
 * Required for performance limits calculation.
 */
#define STM32_VDD                   300U

/*
 * MCU type as defined in the ST header.
 */
#undef  STM32F407xx
#define STM32F407xx

/*===========================================================================*/
/* IO pins assignments.                                                      */
/*===========================================================================*/

/* GPIOA: PA1/PA2/PA7 RMII, PA8 I2C3_SCL, PA9 VBUS_SENSE, PA13/PA14 SWD */
#define GPIOA_PIN0                  0U
#define GPIOA_RMII_REF_CLK          1U
#define GPIOA_RMII_MDIO             2U
#define GPIOA_PIN3                  3U
#define GPIOA_PIN4                  4U
#define GPIOA_PIN5                  5U
#define GPIOA_PIN6                  6U
#define GPIOA_RMII_CRS_DV           7U
#define GPIOA_I2C3_SCL              8U
#define GPIOA_PIN9                  9U
#define GPIOA_PIN10                 10U
#define GPIOA_PIN11                 11U
#define GPIOA_PIN12                 12U
#define GPIOA_SWDIO                 13U
#define GPIOA_SWCLK                 14U
#define GPIOA_PIN15                 15U

/* GPIOB: PB0 LED, PB3 SWO, PB6/PB7 I2C1, PB11-PB13 RMII TX */
#define GPIOB_LED_STATUS            0U
#define GPIOB_PIN1                  1U
#define GPIOB_PIN2                  2U
#define GPIOB_SWO                   3U
#define GPIOB_PIN4                  4U
#define GPIOB_PIN5                  5U
#define GPIOB_I2C1_SCL              6U
#define GPIOB_I2C1_SDA              7U
#define GPIOB_PIN8                  8U
#define GPIOB_PIN9                  9U
#define GPIOB_PIN10                 10U
#define GPIOB_RMII_TX_EN            11U
#define GPIOB_RMII_TXD0             12U
#define GPIOB_RMII_TXD1             13U
#define GPIOB_PIN14                 14U
#define GPIOB_PIN15                 15U

/* GPIOC: PC1/PC4/PC5 RMII, PC8 LNB fault, PC9 I2C3_SDA */
#define GPIOC_PIN0                  0U
#define GPIOC_RMII_MDC              1U
#define GPIOC_PIN2                  2U
#define GPIOC_PIN3                  3U
#define GPIOC_RMII_RXD0             4U
#define GPIOC_RMII_RXD1             5U
#define GPIOC_PIN6                  6U
#define GPIOC_PIN7                  7U
#define GPIOC_LNB_FLT               8U
#define GPIOC_I2C3_SDA              9U
#define GPIOC_PIN10                 10U
#define GPIOC_PIN11                 11U
#define GPIOC_PIN12                 12U
#define GPIOC_PIN13                 13U
#define GPIOC_PIN14                 14U
#define GPIOC_PIN15                 15U

/* GPIOD: PD8/PD9 USART3 (wire protocol), PD12/PD14/PD15 TIM4 */
#define GPIOD_PIN0                  0U
#define GPIOD_PIN1                  1U
#define GPIOD_PIN2                  2U
#define GPIOD_PIN3                  3U
#define GPIOD_PIN4                  4U
#define GPIOD_PIN5                  5U
#define GPIOD_PIN6                  6U
#define GPIOD_PIN7                  7U
#define GPIOD_USART3_TX             8U
#define GPIOD_USART3_RX             9U
#define GPIOD_PIN10                 10U
#define GPIOD_PIN11                 11U
#define GPIOD_TIM4_CH1              12U
#define GPIOD_PIN13                 13U
#define GPIOD_TIM4_CH3              14U
#define GPIOD_TIM4_CH4              15U

/* GPIOE: unused on Cubley v0.5 */
#define GPIOE_PIN0                  0U
#define GPIOE_PIN1                  1U
#define GPIOE_PIN2                  2U
#define GPIOE_PIN3                  3U
#define GPIOE_PIN4                  4U
#define GPIOE_PIN5                  5U
#define GPIOE_PIN6                  6U
#define GPIOE_PIN7                  7U
#define GPIOE_PIN8                  8U
#define GPIOE_PIN9                  9U
#define GPIOE_PIN10                 10U
#define GPIOE_PIN11                 11U
#define GPIOE_PIN12                 12U
#define GPIOE_PIN13                 13U
#define GPIOE_PIN14                 14U
#define GPIOE_PIN15                 15U

/* GPIOH: only PH0/PH1 exist on LQFP100 (HSE crystal) */
#define GPIOH_OSC_IN                0U
#define GPIOH_OSC_OUT               1U
#define GPIOH_PIN2                  2U
#define GPIOH_PIN3                  3U
#define GPIOH_PIN4                  4U
#define GPIOH_PIN5                  5U
#define GPIOH_PIN6                  6U
#define GPIOH_PIN7                  7U
#define GPIOH_PIN8                  8U
#define GPIOH_PIN9                  9U
#define GPIOH_PIN10                 10U
#define GPIOH_PIN11                 11U
#define GPIOH_PIN12                 12U
#define GPIOH_PIN13                 13U
#define GPIOH_PIN14                 14U
#define GPIOH_PIN15                 15U

/*===========================================================================*/
/* IO lines assignments (PAL_LINE for peripheral pins).                      */
/*===========================================================================*/

#define LINE_RMII_REF_CLK           PAL_LINE(GPIOA, 1U)
#define LINE_RMII_MDIO              PAL_LINE(GPIOA, 2U)
#define LINE_RMII_CRS_DV            PAL_LINE(GPIOA, 7U)
#define LINE_I2C3_SCL               PAL_LINE(GPIOA, 8U)
#define LINE_SWDIO                  PAL_LINE(GPIOA, 13U)
#define LINE_SWCLK                  PAL_LINE(GPIOA, 14U)

#define LINE_LED_STATUS             PAL_LINE(GPIOB, 0U)
#define LINE_SWO                    PAL_LINE(GPIOB, 3U)
#define LINE_I2C1_SCL               PAL_LINE(GPIOB, 6U)
#define LINE_I2C1_SDA               PAL_LINE(GPIOB, 7U)
#define LINE_RMII_TX_EN             PAL_LINE(GPIOB, 11U)
#define LINE_RMII_TXD0              PAL_LINE(GPIOB, 12U)
#define LINE_RMII_TXD1              PAL_LINE(GPIOB, 13U)

#define LINE_RMII_MDC               PAL_LINE(GPIOC, 1U)
#define LINE_RMII_RXD0              PAL_LINE(GPIOC, 4U)
#define LINE_RMII_RXD1              PAL_LINE(GPIOC, 5U)
#define LINE_LNB_FLT                PAL_LINE(GPIOC, 8U)
#define LINE_I2C3_SDA               PAL_LINE(GPIOC, 9U)

#define LINE_USART3_TX              PAL_LINE(GPIOD, 8U)
#define LINE_USART3_RX              PAL_LINE(GPIOD, 9U)
#define LINE_TIM4_CH1               PAL_LINE(GPIOD, 12U)
#define LINE_TIM4_CH3               PAL_LINE(GPIOD, 14U)
#define LINE_TIM4_CH4               PAL_LINE(GPIOD, 15U)

#define LINE_OSC_IN                 PAL_LINE(GPIOH, 0U)
#define LINE_OSC_OUT                PAL_LINE(GPIOH, 1U)

/*===========================================================================*/
/* Driver pre-compile time settings.                                         */
/*===========================================================================*/

/*===========================================================================*/
/* Derived constants and error checks.                                       */
/*===========================================================================*/

/*===========================================================================*/
/* Driver data structures and types.                                         */
/*===========================================================================*/

/*===========================================================================*/
/* Driver macros.                                                            */
/*===========================================================================*/

/*
 * I/O ports initial setup, this configuration is established soon after reset
 * in the initialization code.
 * Please refer to the STM32F407 Reference Manual for MODER/OTYPER/OSPEEDR/
 * PUPDR/ODR/AFRL/AFRH encoding.
 *
 * Phase 0 rule: every board-owned peripheral pin is configured as INPUT with
 * PULL-UP so unowned peripherals cannot drive anything. Only SWD (PA13/PA14),
 * SWO (PB3), and OSC (PH0/PH1) are wired to their standard functions from
 * day one.
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
 * GPIOA setup (Phase 0): all peripheral pins input pull-up; SWD active.
 *
 *  PA0  - PIN0             (input pull-up)
 *  PA1  - RMII_REF_CLK     (alternate 11)
 *  PA2  - RMII_MDIO        (alternate 11, pull-up)
 *  PA3  - PIN3             (input pull-up)
 *  PA4  - PIN4             (input pull-up)
 *  PA5  - PIN5             (input pull-up)
 *  PA6  - PIN6             (input pull-up)
 *  PA7  - RMII_CRS_DV      (alternate 11)
 *  PA8  - I2C3_SCL         (input pull-up, Phase 6 -> AF4 OD)
 *  PA9  - VBUS_SENSE       (input floating, VBUS through 100k series resistor)
 *  PA10 - PIN10            (input floating)
 *  PA11 - USB_FS_DM        (alternate 10)
 *  PA12 - USB_FS_DP        (alternate 10)
 *  PA13 - SWDIO            (alternate 0)
 *  PA14 - SWCLK            (alternate 0)
 *  PA15 - PIN15            (input pull-up)
 */
#define VAL_GPIOA_MODER             (PIN_MODE_INPUT(GPIOA_PIN0)          |  \
                                     PIN_MODE_ALTERNATE(GPIOA_RMII_REF_CLK) |  \
                                     PIN_MODE_ALTERNATE(GPIOA_RMII_MDIO) |  \
                                     PIN_MODE_INPUT(GPIOA_PIN3)          |  \
                                     PIN_MODE_INPUT(GPIOA_PIN4)          |  \
                                     PIN_MODE_INPUT(GPIOA_PIN5)          |  \
                                     PIN_MODE_INPUT(GPIOA_PIN6)          |  \
                                     PIN_MODE_ALTERNATE(GPIOA_RMII_CRS_DV) |  \
                                     PIN_MODE_INPUT(GPIOA_I2C3_SCL)      |  \
                                     PIN_MODE_INPUT(GPIOA_PIN9)          |  \
                                     PIN_MODE_INPUT(GPIOA_PIN10)         |  \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN11)     |  \
                                     PIN_MODE_ALTERNATE(GPIOA_PIN12)     |  \
                                     PIN_MODE_ALTERNATE(GPIOA_SWDIO)     |  \
                                     PIN_MODE_ALTERNATE(GPIOA_SWCLK)     |  \
                                     PIN_MODE_INPUT(GPIOA_PIN15))
#define VAL_GPIOA_OTYPER            0x00000000U
#define VAL_GPIOA_OSPEEDR           (PIN_OSPEED_HIGH(GPIOA_SWDIO)        |  \
                                     PIN_OSPEED_HIGH(GPIOA_SWCLK)        |  \
                                     PIN_OSPEED_HIGH(GPIOA_PIN11)        |  \
                                     PIN_OSPEED_HIGH(GPIOA_PIN12)        |  \
                                     PIN_OSPEED_HIGH(GPIOA_RMII_REF_CLK) |  \
                                     PIN_OSPEED_HIGH(GPIOA_RMII_MDIO)    |  \
                                     PIN_OSPEED_HIGH(GPIOA_RMII_CRS_DV))
#define VAL_GPIOA_PUPDR             (PIN_PUPDR_PULLUP(GPIOA_PIN0)        |  \
                                     PIN_PUPDR_FLOATING(GPIOA_RMII_REF_CLK)| \
                                     PIN_PUPDR_PULLUP(GPIOA_RMII_MDIO)   |  \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN3)        |  \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN4)        |  \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN5)        |  \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN6)        |  \
                                     PIN_PUPDR_FLOATING(GPIOA_RMII_CRS_DV) | \
                                     PIN_PUPDR_PULLUP(GPIOA_I2C3_SCL)    |  \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN9)      |  \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN10)     |  \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN11)     |  \
                                     PIN_PUPDR_FLOATING(GPIOA_PIN12)     |  \
                                     PIN_PUPDR_FLOATING(GPIOA_SWDIO)     |  \
                                     PIN_PUPDR_FLOATING(GPIOA_SWCLK)     |  \
                                     PIN_PUPDR_PULLUP(GPIOA_PIN15))
#define VAL_GPIOA_ODR               0xFFFFFFFFU
#define VAL_GPIOA_AFRL              (PIN_AFIO_AF(GPIOA_RMII_REF_CLK, 11U) | \
                                     PIN_AFIO_AF(GPIOA_RMII_MDIO, 11U)   |  \
                                     PIN_AFIO_AF(GPIOA_RMII_CRS_DV, 11U))
#define VAL_GPIOA_AFRH              (PIN_AFIO_AF(GPIOA_PIN11, 10U)       |  \
                                     PIN_AFIO_AF(GPIOA_PIN12, 10U)       |  \
                                     PIN_AFIO_AF(GPIOA_SWDIO, 0U)        |  \
                                     PIN_AFIO_AF(GPIOA_SWCLK, 0U))

/*
 * GPIOB setup (Phase 0): all peripheral pins input pull-up; SWO active.
 *
 *  PB0  - LED_STATUS       (input pull-up, Phase 2 -> output PP low)
 *  PB1  - PIN1             (input pull-up)
 *  PB2  - PIN2             (input pull-up)
 *  PB3  - SWO              (alternate 0)
 *  PB4  - PIN4             (input pull-up)
 *  PB5  - PIN5             (input pull-up)
 *  PB6  - I2C1_SCL         (input pull-up, Phase 3 -> AF4 OD)
 *  PB7  - I2C1_SDA         (input pull-up, Phase 3 -> AF4 OD)
 *  PB8  - PIN8             (input pull-up)
 *  PB9  - PIN9             (input pull-up)
 *  PB10 - PIN10            (input pull-up)
 *  PB11 - RMII_TX_EN       (alternate 11, pull-down)
 *  PB12 - RMII_TXD0        (alternate 11, pull-down)
 *  PB13 - RMII_TXD1        (alternate 11, pull-down)
 *  PB14 - PIN14            (input pull-up)
 *  PB15 - PIN15            (input pull-up)
 */
#define VAL_GPIOB_MODER             (PIN_MODE_INPUT(GPIOB_LED_STATUS)    |  \
                                     PIN_MODE_INPUT(GPIOB_PIN1)          |  \
                                     PIN_MODE_INPUT(GPIOB_PIN2)          |  \
                                     PIN_MODE_ALTERNATE(GPIOB_SWO)       |  \
                                     PIN_MODE_INPUT(GPIOB_PIN4)          |  \
                                     PIN_MODE_INPUT(GPIOB_PIN5)          |  \
                                     PIN_MODE_INPUT(GPIOB_I2C1_SCL)      |  \
                                     PIN_MODE_INPUT(GPIOB_I2C1_SDA)      |  \
                                     PIN_MODE_INPUT(GPIOB_PIN8)          |  \
                                     PIN_MODE_INPUT(GPIOB_PIN9)          |  \
                                     PIN_MODE_INPUT(GPIOB_PIN10)         |  \
                                     PIN_MODE_ALTERNATE(GPIOB_RMII_TX_EN) | \
                                     PIN_MODE_ALTERNATE(GPIOB_RMII_TXD0) |  \
                                     PIN_MODE_ALTERNATE(GPIOB_RMII_TXD1) |  \
                                     PIN_MODE_INPUT(GPIOB_PIN14)         |  \
                                     PIN_MODE_INPUT(GPIOB_PIN15))
#define VAL_GPIOB_OTYPER            0x00000000U
#define VAL_GPIOB_OSPEEDR           (PIN_OSPEED_HIGH(GPIOB_SWO)          |  \
                                     PIN_OSPEED_HIGH(GPIOB_RMII_TX_EN)   |  \
                                     PIN_OSPEED_HIGH(GPIOB_RMII_TXD0)    |  \
                                     PIN_OSPEED_HIGH(GPIOB_RMII_TXD1))
#define VAL_GPIOB_PUPDR             (PIN_PUPDR_PULLUP(GPIOB_LED_STATUS)  |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN1)        |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN2)        |  \
                                     PIN_PUPDR_FLOATING(GPIOB_SWO)       |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN4)        |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN5)        |  \
                                     PIN_PUPDR_PULLUP(GPIOB_I2C1_SCL)    |  \
                                     PIN_PUPDR_PULLUP(GPIOB_I2C1_SDA)    |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN8)        |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN9)        |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN10)       |  \
                                     PIN_PUPDR_PULLDOWN(GPIOB_RMII_TX_EN)|  \
                                     PIN_PUPDR_PULLDOWN(GPIOB_RMII_TXD0) |  \
                                     PIN_PUPDR_PULLDOWN(GPIOB_RMII_TXD1) |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN14)       |  \
                                     PIN_PUPDR_PULLUP(GPIOB_PIN15))
#define VAL_GPIOB_ODR               0xFFFFFFFFU
#define VAL_GPIOB_AFRL              (PIN_AFIO_AF(GPIOB_SWO, 0U))
#define VAL_GPIOB_AFRH              (PIN_AFIO_AF(GPIOB_RMII_TX_EN, 11U) |  \
                                     PIN_AFIO_AF(GPIOB_RMII_TXD0, 11U)  |  \
                                     PIN_AFIO_AF(GPIOB_RMII_TXD1, 11U))

/*
 * GPIOC setup (Phase 0): all peripheral pins input pull-up.
 *
 *  PC0  - PIN0             (input pull-up)
 *  PC1  - RMII_MDC         (alternate 11, pull-up)
 *  PC2  - PIN2             (input pull-up)
 *  PC3  - PIN3             (input pull-up)
 *  PC4  - RMII_RXD0        (alternate 11)
 *  PC5  - RMII_RXD1        (alternate 11)
 *  PC6  - PIN6             (input pull-up)
 *  PC7  - PIN7             (input pull-up)
 *  PC8  - LNB_FLT          (input pull-up)
 *  PC9  - I2C3_SDA         (input pull-up, Phase 6 -> AF4 OD)
 *  PC10 - PIN10            (input pull-up)
 *  PC11 - PIN11            (input pull-up)
 *  PC12 - PIN12            (input pull-up)
 *  PC13 - PIN13            (input pull-up)
 *  PC14 - PIN14            (input pull-up)
 *  PC15 - PIN15            (input pull-up)
 */
#define VAL_GPIOC_MODER             (PIN_MODE_ALTERNATE(GPIOC_RMII_MDC) |  \
                                     PIN_MODE_ALTERNATE(GPIOC_RMII_RXD0) | \
                                     PIN_MODE_ALTERNATE(GPIOC_RMII_RXD1))
#define VAL_GPIOC_OTYPER            0x00000000U
#define VAL_GPIOC_OSPEEDR           (PIN_OSPEED_HIGH(GPIOC_RMII_MDC)    |  \
                                     PIN_OSPEED_HIGH(GPIOC_RMII_RXD0)   |  \
                                     PIN_OSPEED_HIGH(GPIOC_RMII_RXD1))
#define VAL_GPIOC_PUPDR             (PIN_PUPDR_PULLUP(GPIOC_PIN0)        |  \
                                     PIN_PUPDR_PULLUP(GPIOC_RMII_MDC)    |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN2)        |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN3)        |  \
                                     PIN_PUPDR_FLOATING(GPIOC_RMII_RXD0) | \
                                     PIN_PUPDR_FLOATING(GPIOC_RMII_RXD1) | \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN6)        |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN7)        |  \
                                     PIN_PUPDR_PULLUP(GPIOC_LNB_FLT)     |  \
                                     PIN_PUPDR_PULLUP(GPIOC_I2C3_SDA)    |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN10)       |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN11)       |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN12)       |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN13)       |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN14)       |  \
                                     PIN_PUPDR_PULLUP(GPIOC_PIN15))
#define VAL_GPIOC_ODR               0xFFFFFFFFU
#define VAL_GPIOC_AFRL              (PIN_AFIO_AF(GPIOC_RMII_MDC, 11U)   |  \
                                     PIN_AFIO_AF(GPIOC_RMII_RXD0, 11U)  |  \
                                     PIN_AFIO_AF(GPIOC_RMII_RXD1, 11U))
#define VAL_GPIOC_AFRH              0x00000000U

/*
 * GPIOD setup (Phase 0): all peripheral pins input pull-up.
 *
 *  PD0..PD7   PIN0..PIN7   (input pull-up)
 *  PD8  - USART3_TX        (input pull-up, Phase 1 -> AF7)
 *  PD9  - USART3_RX        (input pull-up, Phase 1 -> AF7)
 *  PD10 - PIN10            (input pull-up)
 *  PD11 - PIN11            (input pull-up)
 *  PD12 - TIM4_CH1         (input pull-up, later -> AF2)
 *  PD13 - PIN13            (input pull-up)
 *  PD14 - TIM4_CH3         (input pull-up, later -> AF2)
 *  PD15 - TIM4_CH4         (input pull-up, later -> AF2)
 */
#define VAL_GPIOD_MODER             0x00000000U
#define VAL_GPIOD_OTYPER            0x00000000U
#define VAL_GPIOD_OSPEEDR           0x00000000U
#define VAL_GPIOD_PUPDR             (PIN_PUPDR_PULLUP(GPIOD_PIN0)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN1)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN2)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN3)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN4)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN5)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN6)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN7)        |  \
                                     PIN_PUPDR_PULLUP(GPIOD_USART3_TX)   |  \
                                     PIN_PUPDR_PULLUP(GPIOD_USART3_RX)   |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN10)       |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN11)       |  \
                                     PIN_PUPDR_PULLUP(GPIOD_TIM4_CH1)    |  \
                                     PIN_PUPDR_PULLUP(GPIOD_PIN13)       |  \
                                     PIN_PUPDR_PULLUP(GPIOD_TIM4_CH3)    |  \
                                     PIN_PUPDR_PULLUP(GPIOD_TIM4_CH4))
#define VAL_GPIOD_ODR               0xFFFFFFFFU
#define VAL_GPIOD_AFRL              0x00000000U
#define VAL_GPIOD_AFRH              0x00000000U

/*
 * GPIOE setup (Phase 0): unused on Cubley v0.5, all input pull-up.
 */
#define VAL_GPIOE_MODER             0x00000000U
#define VAL_GPIOE_OTYPER            0x00000000U
#define VAL_GPIOE_OSPEEDR           0x00000000U
#define VAL_GPIOE_PUPDR             (PIN_PUPDR_PULLUP(GPIOE_PIN0)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN1)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN2)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN3)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN4)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN5)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN6)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN7)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN8)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN9)        |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN10)       |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN11)       |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN12)       |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN13)       |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN14)       |  \
                                     PIN_PUPDR_PULLUP(GPIOE_PIN15))
#define VAL_GPIOE_ODR               0xFFFFFFFFU
#define VAL_GPIOE_AFRL              0x00000000U
#define VAL_GPIOE_AFRH              0x00000000U

/*
 * GPIOF/GPIOG do not exist on STM32F407VG in LQFP100 package.
 * The macros are still required by ChibiOS PAL init; leave them all-zero.
 */
#define VAL_GPIOF_MODER             0x00000000U
#define VAL_GPIOF_OTYPER            0x00000000U
#define VAL_GPIOF_OSPEEDR           0x00000000U
#define VAL_GPIOF_PUPDR             0x00000000U
#define VAL_GPIOF_ODR               0x00000000U
#define VAL_GPIOF_AFRL              0x00000000U
#define VAL_GPIOF_AFRH              0x00000000U

#define VAL_GPIOG_MODER             0x00000000U
#define VAL_GPIOG_OTYPER            0x00000000U
#define VAL_GPIOG_OSPEEDR           0x00000000U
#define VAL_GPIOG_PUPDR             0x00000000U
#define VAL_GPIOG_ODR               0x00000000U
#define VAL_GPIOG_AFRL              0x00000000U
#define VAL_GPIOG_AFRH              0x00000000U

/*
 * GPIOH setup (LQFP100): only PH0 (OSC_IN) and PH1 (OSC_OUT) are pinned out.
 * ChibiOS PAL leaves them as input floating; the stm32_clock_init() code
 * enables the HSE oscillator via RCC->CR (BYP=0, HSEON=1).
 */
#define VAL_GPIOH_MODER             (PIN_MODE_INPUT(GPIOH_OSC_IN)        |  \
                                     PIN_MODE_INPUT(GPIOH_OSC_OUT))
#define VAL_GPIOH_OTYPER            0x00000000U
#define VAL_GPIOH_OSPEEDR           0x00000000U
#define VAL_GPIOH_PUPDR             0x00000000U
#define VAL_GPIOH_ODR               0x00000000U
#define VAL_GPIOH_AFRL              0x00000000U
#define VAL_GPIOH_AFRH              0x00000000U

/*
 * GPIOI does not exist on STM32F407VG in LQFP100 package.
 */
#define VAL_GPIOI_MODER             0x00000000U
#define VAL_GPIOI_OTYPER            0x00000000U
#define VAL_GPIOI_OSPEEDR           0x00000000U
#define VAL_GPIOI_PUPDR             0x00000000U
#define VAL_GPIOI_ODR               0x00000000U
#define VAL_GPIOI_AFRL              0x00000000U
#define VAL_GPIOI_AFRH              0x00000000U

/*===========================================================================*/
/* External declarations.                                                    */
/*===========================================================================*/

#if !defined(_FROM_ASM_)
#ifdef __cplusplus
extern "C" {
#endif
  void boardInit(void);
#ifdef __cplusplus
}
#endif
#endif /* _FROM_ASM_ */

#endif /* BOARD_H */
