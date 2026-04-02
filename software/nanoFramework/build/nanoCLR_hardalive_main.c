// Hardalive entry point: no RTOS, no CLR, no HAL.
// Proves bare execution by directly enabling GPIO clocks and toggling
// PA2 (LED net) and PB10 (UART3_TX net) at register level.

#include <stdint.h>

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

int main(void)
{
    // Enable GPIOA and GPIOB peripheral clocks.
    RCC_AHB1ENR |= (1U << 0) | (1U << 1);

    // PA2 output mode (01 in MODER[5:4]).
    GPIOA_MODER &= ~(3U << (2U * 2U));
    GPIOA_MODER |=  (1U << (2U * 2U));

    // PB10 output mode (01 in MODER[21:20]).
    GPIOB_MODER &= ~(3U << (10U * 2U));
    GPIOB_MODER |=  (1U << (10U * 2U));

    for (;;)
    {
        GPIOA_BSRR = (1U << 2);
        GPIOB_BSRR = (1U << 10);
        busy_delay(12000000U);

        GPIOA_BSRR = (1U << (2U + 16U));
        GPIOB_BSRR = (1U << (10U + 16U));
        busy_delay(12000000U);
    }
}

// Diagnostic fallback: some current startup/link combinations are observed to
// branch to VectorF8 instead of Reset_Handler before reaching main(). Route
// both entries to main() for this hardalive profile to prove basic execution.
void Reset_Handler(void)
{
    (void)main();
    for (;;)
    {
    }
}

void VectorF8(void)
{
    (void)main();
    for (;;)
    {
    }
}
