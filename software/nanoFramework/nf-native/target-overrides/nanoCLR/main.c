// Minimal nanoCLR entry point for M0DMF_CUBLEY_F407.

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

extern volatile uint32_t g_cubley_diag_current_status;
extern volatile uint32_t g_cubley_diag_last_error;
extern volatile uint32_t g_cubley_diag_clr_status;
volatile uint32_t g_startup_trace;

#if !defined(CUBLEY_WIRE_PROTOCOL_USB)
#define CUBLEY_WIRE_PROTOCOL_USB HAL_USE_SERIAL_USB
#endif

#if (HAL_USE_SERIAL_USB == TRUE) || (defined(CUBLEY_ENABLE_USB_CDC_CONSOLE) && (CUBLEY_ENABLE_USB_CDC_CONSOLE == TRUE))
#define CUBLEY_USB_CDC_ACTIVE TRUE
#else
#define CUBLEY_USB_CDC_ACTIVE FALSE
#endif

static inline void SetStartupDiag(uint8_t stage, uint8_t result, uint8_t detail)
{
    // 0xD5SSRRDD => signature(0xD5), stage, result, detail.
    // Stages used here: C0..C6 (main path), D0 (receiver thread), D1 (CLR thread), CF (unexpected post-osKernelStart path).
    const uint32_t word = ((uint32_t)0xD5 << 24) | ((uint32_t)stage << 16) | ((uint32_t)result << 8) | (uint32_t)detail;
    g_cubley_diag_current_status = word;
    g_cubley_diag_clr_status = word;
}

static inline void SetStartupErr(uint8_t op, uint8_t code, uint8_t detail)
{
    // 0xE2 marks CLR startup diagnostics (distinct from W5500 0xE1 path).
    g_cubley_diag_last_error = ((uint32_t)0xE2 << 24) | ((uint32_t)op << 16) | ((uint32_t)code << 8) | (uint32_t)detail;
}

static inline void SetStartupTrace(uint8_t stage, uint8_t detail)
{
    // Independent startup breadcrumb: 0xA7SS00DD.
    g_startup_trace = ((uint32_t)0xA7 << 24) | ((uint32_t)stage << 16) | (uint32_t)detail;
}

static void ReceiverThreadProbe(void const *arg)
{
    SetStartupDiag(0xD0, 0, 1);
    SetStartupErr(0xD0, 0, 1);

    ReceiverThread(arg);

    // Receiver thread should never return in a healthy runtime.
    SetStartupDiag(0xD0, 14, 0xFE);
    SetStartupErr(0xD0, 0xFE, 0);
    while (true)
    {
        osDelay(100);
    }
}

static void CLRStartupThreadProbe(void const *arg)
{
    SetStartupDiag(0xD1, 0, 1);
    SetStartupErr(0xD1, 0, 1);

    CLRStartupThread(arg);

    // CLR startup thread should not return during normal operation.
    SetStartupDiag(0xD1, 14, 0xFD);
    SetStartupErr(0xD1, 0xFD, 0);
    while (true)
    {
        osDelay(100);
    }
}

osThreadDef(ReceiverThreadProbe, osPriorityHigh, 2048, "ReceiverThread");
osThreadDef(CLRStartupThreadProbe, osPriorityNormal, 4096, "CLRStartupThread");

#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
static THD_WORKING_AREA(waUsbCdcInitThread, 768);
static THD_FUNCTION(UsbCdcInitThread, arg)
{
    (void)arg;
    chRegSetThreadName("USB_CDC_Init");

    SetStartupTrace(0xC3, 1);
    SetStartupDiag(0xC3, 1, 1);  // USB init starting
    sduObjectInit(&SDU1);
    sduStart(&SDU1, &serusbcfg);
    SetStartupTrace(0xC3, 2);
    SetStartupDiag(0xC3, 1, 2);  // sduStart completed

    usbDisconnectBus(serusbcfg.usbp);
    SetStartupTrace(0xC3, 3);
    SetStartupDiag(0xC3, 1, 3);  // usbDisconnectBus completed
    chThdSleepMilliseconds(100);
    usbStart(serusbcfg.usbp, &usbcfg);
    SetStartupTrace(0xC3, 4);
    SetStartupDiag(0xC3, 1, 4);  // usbStart completed
    usbConnectBus(serusbcfg.usbp);
    SetStartupTrace(0xC3, 5);
    SetStartupDiag(0xC3, 1, 5);  // usbConnectBus completed

    chThdExit(MSG_OK);
}
#endif

#if defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE)
extern int cubley_w5500_early_init(void);
extern volatile uint32_t g_cubley_diag_current_status;

/*
 * Helper thread that performs the W5500 hardware bring-up once the
 * ChibiOS scheduler is running.
 *
 * w5500_hw_init() uses chThdSleepMilliseconds() heavily (PHY reset
 * settling, OPMD/OPMDC re-assert windows, RST self-clear poll). Those
 * sleeps deadlock or no-op when called before osKernelStart() because
 * SysTick is not yet armed in this nanoFramework port - the same
 * pre-kernel timing pitfall that affects USB. Running the bring-up
 * after kernel start makes every chThdSleepMilliseconds actually
 * sleep, so the W5500's ~3 ms internal PHY reset window is honoured.
 */
static THD_WORKING_AREA(waW5500InitThread, 1024);
static THD_FUNCTION(W5500InitThread, arg) {
    (void)arg;
    chRegSetThreadName("W5500Init");

    int earlyInitStatus = cubley_w5500_early_init();

    if (earlyInitStatus == 0)
    {
        g_cubley_diag_current_status =
            ((uint32_t)0xD5 << 24) | ((uint32_t)0x90 << 16) | ((uint32_t)1 << 8);
    }
    else
    {
        g_cubley_diag_current_status =
            ((uint32_t)0xD5 << 24) | ((uint32_t)0x90 << 16) |
            ((uint32_t)14 << 8) | ((uint32_t)earlyInitStatus & 0xFFU);
    }

    /*
     * Heartbeat on PA2 (LED_STATUS) so the user can tell at a glance
     * whether firmware is alive and whether early-init passed:
     *   - PASS: short blip every second.
     *   - FAIL: double-blink every second.
     */
    const bool pass = (earlyInitStatus == 0);
    while (true)
    {
        palSetLine(PAL_LINE(GPIOA, 2U));
        chThdSleepMilliseconds(60);
        palClearLine(PAL_LINE(GPIOA, 2U));
        if (!pass)
        {
            chThdSleepMilliseconds(120);
            palSetLine(PAL_LINE(GPIOA, 2U));
            chThdSleepMilliseconds(60);
            palClearLine(PAL_LINE(GPIOA, 2U));
        }
        chThdSleepMilliseconds(900);
    }
}
#endif

#if (CUBLEY_WIRE_PROTOCOL_USB != TRUE)
static void __attribute__((unused)) ForceUsart3PinsOnPb10Pb11(void)
{
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIOBEN;
    (void)RCC->AHB1ENR;

    GPIOB->MODER &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->MODER |= ((2u << (10u * 2u)) | (2u << (11u * 2u)));

    GPIOB->OTYPER &= ~((1u << 10u) | (1u << 11u));
    GPIOB->OSPEEDR |= ((3u << (10u * 2u)) | (3u << (11u * 2u)));
    GPIOB->PUPDR &= ~((3u << (10u * 2u)) | (3u << (11u * 2u)));

    GPIOB->AFRH &= ~((0xFu << ((10u - 8u) * 4u)) | (0xFu << ((11u - 8u) * 4u)));
    GPIOB->AFRH |= ((7u << ((10u - 8u) * 4u)) | (7u << ((11u - 8u) * 4u)));
}
#endif

int main(void)
{
    SetStartupTrace(0xC0, 1);
    SetStartupDiag(0xC0, 0, 1);
    SetStartupErr(0xC0, 0, 1);
    SetStartupDiag(0xC0, 2, (CUBLEY_USB_CDC_ACTIVE ? 1 : 0));  // Diagnostic: is USB_CDC active?

    halInit();

    InitBootClipboard();
    SetStartupDiag(0xC1, 0, 1);
    SetStartupErr(0xC1, 0, 1);

#if (SWO_OUTPUT == TRUE)
    SwoInit();
#endif

    osKernelInitialize();
    SetStartupDiag(0xC2, 0, 1);
    SetStartupErr(0xC2, 0, 1);

#if (HAL_NF_USE_STM32_CRC == TRUE)
    crcStart(NULL);
    SetStartupTrace(0xC3, 0);
    SetStartupDiag(0xC3, 0, 1);
    SetStartupErr(0xC3, 0, 1);
#endif

#if (CUBLEY_WIRE_PROTOCOL_USB != TRUE)
    static const SerialConfig usart3_cfg = {
        115200,
        0,
        USART_CR2_STOP1_BITS,
        0
    };

    palSetLineMode(PAL_LINE(GPIOB, 10U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOB, 11U), PAL_MODE_ALTERNATE(7));
    ForceUsart3PinsOnPb10Pb11();

    sdStart(&SD3, &usart3_cfg);
#endif

    SetStartupDiag(0xC4, 0, 1);
    SetStartupErr(0xC4, 0, 1);

#if defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE)
    /*
     * Configure PA2 as a heartbeat output. The actual W5500 hardware
     * bring-up runs on W5500InitThread once the kernel is up, so that
     * its chThdSleepMilliseconds() calls actually sleep.
     */
    palSetLineMode(PAL_LINE(GPIOA, 2U), PAL_MODE_OUTPUT_PUSHPULL);
    palClearLine(PAL_LINE(GPIOA, 2U));
#endif

#if defined(CUBLEY_W5500_EARLY_INIT) && (CUBLEY_W5500_EARLY_INIT == TRUE)
    chThdCreateStatic(waW5500InitThread, sizeof(waW5500InitThread),
                      NORMALPRIO + 1, W5500InitThread, NULL);
#endif

#if (CUBLEY_USB_CDC_ACTIVE == TRUE)
    chThdCreateStatic(waUsbCdcInitThread, sizeof(waUsbCdcInitThread),
                      NORMALPRIO + 2, UsbCdcInitThread, NULL);
#endif

    osThreadCreate(osThread(ReceiverThreadProbe), NULL);

    CLR_SETTINGS clrSettings;
    (void)memset(&clrSettings, 0, sizeof(CLR_SETTINGS));

    clrSettings.MaxContextSwitches = 50;
    clrSettings.WaitForDebugger = false;
    clrSettings.EnterDebuggerLoopAfterExit = true;

    osThreadCreate(osThread(CLRStartupThreadProbe), &clrSettings);

    SetStartupDiag(0xC5, 0, 1);
    SetStartupErr(0xC5, 0, 1);

    SetStartupDiag(0xC6, 0, 1);
    SetStartupErr(0xC6, 0, 1);
    osKernelStart();

    SetStartupDiag(0xCF, 14, 0xFF);
    SetStartupErr(0xCF, 0xFF, 0);

    while (true)
    {
        osDelay(100);
    }
}