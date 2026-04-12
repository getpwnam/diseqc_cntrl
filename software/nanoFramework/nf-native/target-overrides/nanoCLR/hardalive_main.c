// Hardalive nanoCLR entry point: bare-metal GPIO test using raw CPU delay.
// Uses systick timer loops instead of ChibiOS thread sleep.
// PA2 (LED) heartbeats every ~500 ms.
// PB10/PB11 toggle synchronously with PA2 (reference signals for scope correlation).
// PC6 toggles every ~2 seconds (one edge per 2 sec cycle, slow to verify pull-down capability).

#include <ch.h>
#include <hal.h>
#include <stdbool.h>

// Raw delay using ARM sysTick (STM32F407 clocked at 168 MHz typical)
// Each loop iteration is ~a few cycles. Empirically ~500ms = 168M cycles / 4 ≈ 42M iterations.
static inline void delay_ms(uint32_t ms)
{
    // Rough: 168 MHz / 1000 = 168kHz = ~5.95 us per 1000 cycles
    // So roughly ms * 168000 CPU cycles needed.
    // Using volatile to prevent compiler optimization.
    volatile uint32_t count = ms * 40000U;  // Heuristic: 40k cycles ≈ 0.24 ms on 168 MHz
    while (count--)
    {
        __asm("nop");
    }
}

int main(void)
{
    // Initialize HAL but do NOT start RTOS scheduler
    halInit();

    const ioline_t led = PAL_LINE(GPIOA, 2U);
    const ioline_t ref_tx = PAL_LINE(GPIOB, 10U);
    const ioline_t ref_rx = PAL_LINE(GPIOB, 11U);
    const ioline_t w5500_rst = PAL_LINE(GPIOC, 6U);

    palSetLineMode(led, PAL_MODE_OUTPUT_PUSHPULL);
    palSetLineMode(ref_tx, PAL_MODE_OUTPUT_PUSHPULL);
    palSetLineMode(ref_rx, PAL_MODE_OUTPUT_PUSHPULL);
    palSetLineMode(w5500_rst, PAL_MODE_OUTPUT_PUSHPULL);

    palClearLine(led);
    palClearLine(ref_tx);
    palClearLine(ref_rx);
    palSetLine(w5500_rst);

    bool heartbeat = false;
    uint32_t ticks = 0;

    while (true)
    {
        heartbeat = !heartbeat;

        if (heartbeat)
        {
            palSetLine(led);
            palSetLine(ref_tx);
            palSetLine(ref_rx);
        }
        else
        {
            palClearLine(led);
            palClearLine(ref_tx);
            palClearLine(ref_rx);
        }

        // Toggle PC6 slowly: one transition every 2 seconds (500 ms loop * 4).
        if ((ticks % 4U) == 0U)
        {
            palToggleLine(w5500_rst);
        }

        ticks++;
        
        // Use raw CPU delay instead of ChibiOS thread sleep
        delay_ms(500);
    }
}
