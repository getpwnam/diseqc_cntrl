// nanoCLR entry point for M0DMF_CUBLEY_F407.
// Grounded in the target-overrides-cubley-base working pattern.
// USART3 on PD8/PD9 at 921600 baud for wire protocol.

#include <ch.h>
#include <hal.h>
#include <hal_nf_community.h>
#include <cmsis_os.h>

#include <serialcfg.h>
#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#include <usbcfg.h>
#endif
#include <swo.h>
#include <CLR_Startup_Thread.h>
#include <WireProtocol_ReceiverThread.h>
#include <nanoCLR_Application.h>
#include <nanoHAL_v2.h>

#ifndef SWO_OUTPUT
#define SWO_OUTPUT 0
#endif

#if !defined(CUBLEY_WIRE_PROTOCOL_USB)
#define CUBLEY_WIRE_PROTOCOL_USB HAL_USE_SERIAL_USB
#endif

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#define CUBLEY_USB_CDC_ACTIVE TRUE
#else
#define CUBLEY_USB_CDC_ACTIVE FALSE
#endif

/* ─── SWD mailbox diagnostics ───────────────────────────────────────────── */
extern volatile uint32_t g_cubley_diag_current_status;
extern volatile uint32_t g_cubley_diag_last_error;
extern volatile uint32_t g_cubley_diag_clr_status;
volatile uint32_t g_startup_trace;
volatile uint32_t g_cubley_diag_mac_probe0;
volatile uint32_t g_cubley_diag_mac_probe1;
volatile uint32_t g_cubley_diag_mac_probe2;
volatile uint32_t g_cubley_diag_mac_probe3;
volatile uint32_t g_cubley_diag_mac_probe4;
volatile uint32_t g_cubley_diag_mac_probe5;

static inline void SetStartupDiag(uint8_t stage, uint8_t result, uint8_t detail)
{
    const uint32_t word = ((uint32_t)0xD5 << 24) | ((uint32_t)stage << 16)
                        | ((uint32_t)result << 8) | (uint32_t)detail;
    g_cubley_diag_current_status = word;
    g_cubley_diag_clr_status     = word;
}

static inline void SetStartupErr(uint8_t op, uint8_t code, uint8_t detail)
{
    g_cubley_diag_last_error = ((uint32_t)0xE2 << 24) | ((uint32_t)op << 16)
                             | ((uint32_t)code << 8)  | (uint32_t)detail;
}

static inline void SetStartupTrace(uint8_t stage, uint8_t detail)
{
    g_startup_trace = ((uint32_t)0xA7 << 24) | ((uint32_t)stage << 16) | (uint32_t)detail;
}

/* Called by the ChibiOS system halt path. */
void CubleySystemHaltHook(const char *reason)
{
    (void)reason;
    SetStartupDiag(0xE0, 0xEE, 0xFF);
    SetStartupErr(0xE0, 0xEE, 0xFF);
}

/* ─── LED pulse ──────────────────────────────────────────────────────────── */
static void BusyDelay(volatile uint32_t cycles)
{
    while (cycles-- > 0)
        __asm("nop");
}

static void __attribute__((unused)) PulseStatusLed(int count)
{
    palSetPadMode(GPIOB, 0, PAL_MODE_OUTPUT_PUSHPULL);
    palClearPad(GPIOB, 0);
    for (int i = 0; i < count; i++)
    {
        palSetPad(GPIOB, 0);
        BusyDelay(2200000U);
        palClearPad(GPIOB, 0);
        BusyDelay(2200000U);
    }
}

/* ─── Thread stubs (with mailbox markers, same 4 KB stack as cubley-base) ─ */
static void ReceiverThreadProbe(void const *arg)
{
    SetStartupDiag(0xD0, 0, 1);
    ReceiverThread(arg);
    SetStartupDiag(0xD0, 14, 0xFE);
    while (true) osDelay(100);
}

static void CLRStartupThreadProbe(void const *arg)
{
    SetStartupDiag(0xD1, 0, 1);
    CLRStartupThread(arg);
    SetStartupDiag(0xD1, 14, 0xFD);
    while (true) osDelay(100);
}

osThreadDef(ReceiverThreadProbe,  osPriorityHigh,   4096, "ReceiverThread");
osThreadDef(CLRStartupThreadProbe, osPriorityNormal, 4096, "CLRStartupThread");

/* ─── USB-CDC init thread (only if USB CDC is active) ───────────────────── */
#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
static THD_WORKING_AREA(waUsbCdcInitThread, 768);
static THD_FUNCTION(UsbCdcInitThread, arg)
{
    (void)arg;
    chRegSetThreadName("USB_CDC_Init");
    sduObjectInit(&SDU1);
    sduStart(&SDU1, &serusbcfg);
    usbDisconnectBus(serusbcfg.usbp);
    chThdSleepMilliseconds(100);
    usbStart(serusbcfg.usbp, &usbcfg);
    usbConnectBus(serusbcfg.usbp);
    chThdExit(MSG_OK);
}
#endif

/* ─── main ───────────────────────────────────────────────────────────────── */
int main(void)
{
    SetStartupDiag(0xC0, 0, 1);

    SetStartupDiag(0xC1, 0, 0x10);
    halInit();
    SetStartupDiag(0xC1, 0, 0x11);

    InitBootClipboard();

#if (SWO_OUTPUT == TRUE)
    SwoInit();
#endif

    osKernelInitialize();
    SetStartupDiag(0xC2, 0, 1);

#if (HAL_NF_USE_STM32_CRC == TRUE)
    crcStart(NULL);
    SetStartupDiag(0xC3, 0, 1);
#endif

#if (CUBLEY_WIRE_PROTOCOL_USB != TRUE)
    // Enable USART3 peripheral clock then start the ChibiOS serial driver.
    // Both palSetLineMode and direct AFRH writes are needed: palSetLineMode
    // updates the ChibiOS PAL state; the raw GPIO write ensures the AF is set
    // even if halInit touched the registers.
    RCC->APB1ENR |= RCC_APB1ENR_USART3EN;
    (void)RCC->APB1ENR;

    palSetLineMode(PAL_LINE(GPIOD, 8U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOD, 9U), PAL_MODE_ALTERNATE(7));

    // Belt-and-suspenders: write GPIOD AFRH directly for PD8 and PD9 (AF7).
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIODEN;
    (void)RCC->AHB1ENR;
    GPIOD->AFRH = (GPIOD->AFRH
                  & ~((0xFu << ((8u - 8u) * 4u)) | (0xFu << ((9u - 8u) * 4u))))
                  |  ((7u   << ((8u - 8u) * 4u)) | (7u   << ((9u - 8u) * 4u)));

    static const SerialConfig usart3_cfg = {
        921600, 0, USART_CR2_STOP1_BITS, 0
    };
    SetStartupDiag(0xC3, 0, 0x20);
    sdStart(&SD3, &usart3_cfg);
    SetStartupDiag(0xC3, 0, 0x22);
    // No LED pulse here: the busy-delay blocks osKernelStart for seconds.
    // Boot visibility is already provided by SWD mailbox markers.
#endif

    SetStartupTrace(0xC4, 1);
    SetStartupDiag(0xC4, 0, 1);

#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
    chThdCreateStatic(waUsbCdcInitThread, sizeof(waUsbCdcInitThread),
                      NORMALPRIO + 2, UsbCdcInitThread, NULL);
#endif

    osThreadCreate(osThread(ReceiverThreadProbe),  NULL);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));
    clrSettings.MaxContextSwitches         = 50;
    clrSettings.WaitForDebugger            = false;
    clrSettings.EnterDebuggerLoopAfterExit = false;
    osThreadCreate(osThread(CLRStartupThreadProbe), &clrSettings);

    SetStartupDiag(0xC5, 0, 1);
    osKernelStart();

    // Never reached.
    SetStartupDiag(0xCF, 14, 0xFF);
    while (true) osDelay(100);
}
