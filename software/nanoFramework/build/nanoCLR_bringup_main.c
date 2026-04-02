// Bring-up smoke entry point: direct preflight blink on PA2/PB10, then
// initialize ChibiOS/HAL and emit a USART3 heartbeat.

#include <ch.h>
#include <hal.h>

#include <serialcfg.h>

#define REG32(addr) (*(volatile uint32_t *)(addr))

#define RCC_AHB1ENR REG32(0x40023830U)
#define GPIOA_MODER REG32(0x40020000U)
#define GPIOA_BSRR  REG32(0x40020018U)
#define GPIOB_MODER REG32(0x40020400U)
#define GPIOB_BSRR  REG32(0x40020418U)

static void busy_delay(volatile uint32_t cycles)
{
    while (cycles-- > 0U)
    {
        __asm__ volatile ("nop");
    }
}

static void preflight_init_gpio(void)
{
    RCC_AHB1ENR |= (1U << 0) | (1U << 1);

    GPIOA_MODER &= ~(3U << (2U * 2U));
    GPIOA_MODER |=  (1U << (2U * 2U));

    GPIOB_MODER &= ~(3U << (10U * 2U));
    GPIOB_MODER |=  (1U << (10U * 2U));
}

static void preflight_blink(void)
{
    preflight_init_gpio();

    // Brief visible proof of execution before HAL/RTOS startup.
    for (uint32_t i = 0; i < 3U; i++)
    {
        GPIOA_BSRR = (1U << 2);
        GPIOB_BSRR = (1U << 10);
        busy_delay(3000000U);

        GPIOA_BSRR = (1U << (2U + 16U));
        GPIOB_BSRR = (1U << (10U + 16U));
        busy_delay(3000000U);
    }
}

static void panic_blink(void)
{
    preflight_init_gpio();

    for (;;)
    {
        GPIOA_BSRR = (1U << 2);
        GPIOB_BSRR = (1U << 10);
        busy_delay(1200000U);

        GPIOA_BSRR = (1U << (2U + 16U));
        GPIOB_BSRR = (1U << (10U + 16U));
        busy_delay(1200000U);
    }
}

void HardFault_Handler(void)
{
    panic_blink();
}

void MemManage_Handler(void)
{
    panic_blink();
}

void BusFault_Handler(void)
{
    panic_blink();
}

void UsageFault_Handler(void)
{
    panic_blink();
}

int main(void)
{
    static const uint8_t banner[] = "\r\n[bringup-smoke] start\r\n";
    static const uint8_t heartbeat[] = "[bringup-smoke] hb\r\n";
    uint32_t heartbeat_divider = 0U;

    // Explicit 115200 8N1 config — do not pass NULL, ChibiOS default may differ.
    static const SerialConfig usart3_cfg = {
        115200,
        0,
        USART_CR2_STOP1_BITS,
        0
    };

    preflight_blink();

    halInit();
    chSysInit();

    // Preflight: drive PB10 as plain GPIO to validate electrical path
    // independently from UART peripheral configuration.
    palSetLineMode(PAL_LINE(GPIOB, 10U), PAL_MODE_OUTPUT_PUSHPULL);
    palSetLine(PAL_LINE(GPIOB, 10U));
    chThdSleepMilliseconds(150);
    palClearLine(PAL_LINE(GPIOB, 10U));
    chThdSleepMilliseconds(150);

    // USART3 wire protocol channel from board config, used here as test UART.
    sdStart(&SERIAL_DRIVER, &usart3_cfg);

    // Explicitly reconfigure PB10/PB11 to USART3 alternate function after GPIO preflight.
    palSetLineMode(PAL_LINE(GPIOB, 10U), PAL_MODE_ALTERNATE(7));  // PB10 = USART3_TX
    palSetLineMode(PAL_LINE(GPIOB, 11U), PAL_MODE_ALTERNATE(7));  // PB11 = USART3_RX

    chnWriteTimeout(&SERIAL_DRIVER, banner, sizeof(banner) - 1, TIME_MS2I(20));

    while (true)
    {
        // PA2 is LED_STATUS in board_diseqc.h.
        palTogglePad(GPIOA, 2U);

        if (heartbeat_divider == 0U)
        {
            chnWriteTimeout(&SERIAL_DRIVER, heartbeat, sizeof(heartbeat) - 1, TIME_MS2I(20));
        }

        heartbeat_divider = (heartbeat_divider + 1U) % 4U;
        chThdSleepMilliseconds(500);
    }
}
