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
volatile uint32_t g_cubley_diag_mac_probe0;
volatile uint32_t g_cubley_diag_mac_probe1;
volatile uint32_t g_cubley_diag_mac_probe2;
volatile uint32_t g_cubley_diag_mac_probe3;
volatile uint32_t g_cubley_diag_mac_probe4;
volatile uint32_t g_cubley_diag_mac_probe5;

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
    // 0xE2 marks CLR startup diagnostics.
    g_cubley_diag_last_error = ((uint32_t)0xE2 << 24) | ((uint32_t)op << 16) | ((uint32_t)code << 8) | (uint32_t)detail;
}

static inline void SetStartupTrace(uint8_t stage, uint8_t detail)
{
    // Independent startup breadcrumb: 0xA7SS00DD.
    g_startup_trace = ((uint32_t)0xA7 << 24) | ((uint32_t)stage << 16) | (uint32_t)detail;
}

static uint8_t HaltReasonCode(const char *reason)
{
    if (reason == NULL)
    {
        return 0x7F;
    }

    // ChibiOS MACv2 reports PHY autodetect timeout as "MAC failure".
    if ((reason[0] == 'M') && (reason[1] == 'A') && (reason[2] == 'C') && (reason[3] == ' '))
    {
        return 0x31;
    }

    if ((reason[0] == 'D') && (reason[1] == 'M') && (reason[2] == 'A') && (reason[3] == ' '))
    {
        return 0x21;
    }

    return 0x7E;
}

static void CaptureMacFailureContext(void)
{
    enum
    {
        kPhyId1Reg = 2,
        kPhyId2Reg = 3
    };

    // probe0: [31:24]=0xB1 tag, [23:16]=decoded PHY address, [15:0]=last MII data low bits.
    // probe1: raw MII control register at failure (helps identify busy/op/address state).
    uint8_t phyAddr = 0xFF;
    uint32_t miiControl = 0;
    uint32_t miiData = 0;
    uint8_t firstAddr = 0xFF;
    uint16_t firstId1 = 0;
    uint16_t firstId2 = 0;
    uint8_t firstAllZeroAddr = 0xFF;
    uint8_t firstAllFFFFAddr = 0xFF;
    uint8_t allZeroCount = 0;
    uint8_t allFFFFCount = 0;

#if defined(ETH_MACMDIOAR_MB)
    extern MACDriver ETHD1;
    phyAddr = (uint8_t)((ETHD1.phyaddr >> ETH_MACMDIOAR_PA_Pos) & 0x1Fu);
    miiControl = ETH->MACMDIOAR;
    miiData = ETH->MACMDIODR;

    for (uint8_t i = 0; i <= 31u; i++)
    {
        uint32_t id1;
        uint32_t id2;

        ETHD1.phyaddr = ((uint32_t)i << ETH_MACMDIOAR_PA_Pos);
        id1 = mii_read(&ETHD1, kPhyId1Reg);
        id2 = mii_read(&ETHD1, kPhyId2Reg);

        if (((id1 & 0xFFFFu) == 0u) && ((id2 & 0xFFFFu) == 0u))
        {
            if (firstAllZeroAddr == 0xFFu)
            {
                firstAllZeroAddr = i;
            }
            if (allZeroCount < 0xFFu)
            {
                allZeroCount++;
            }
        }

        if (((id1 & 0xFFFFu) == 0xFFFFu) && ((id2 & 0xFFFFu) == 0xFFFFu))
        {
            if (firstAllFFFFAddr == 0xFFu)
            {
                firstAllFFFFAddr = i;
            }
            if (allFFFFCount < 0xFFu)
            {
                allFFFFCount++;
            }
        }

        if (((id1 & 0xFFFFu) != 0u && (id1 & 0xFFFFu) != 0xFFFFu) ||
            ((id2 & 0xFFFFu) != 0u && (id2 & 0xFFFFu) != 0xFFFFu))
        {
            firstAddr = i;
            firstId1 = (uint16_t)(id1 & 0xFFFFu);
            firstId2 = (uint16_t)(id2 & 0xFFFFu);
            break;
        }
    }
#elif defined(ETH_MACMIIAR_MB)
    extern MACDriver ETHD1;
    phyAddr = (uint8_t)((ETHD1.phyaddr >> 11u) & 0x1Fu);
    miiControl = ETH->MACMIIAR;
    miiData = ETH->MACMIIDR;

    for (uint8_t i = 0; i <= 31u; i++)
    {
        uint32_t id1;
        uint32_t id2;

        ETHD1.phyaddr = ((uint32_t)i << 11u);
        id1 = mii_read(&ETHD1, kPhyId1Reg);
        id2 = mii_read(&ETHD1, kPhyId2Reg);

        if (((id1 & 0xFFFFu) == 0u) && ((id2 & 0xFFFFu) == 0u))
        {
            if (firstAllZeroAddr == 0xFFu)
            {
                firstAllZeroAddr = i;
            }
            if (allZeroCount < 0xFFu)
            {
                allZeroCount++;
            }
        }

        if (((id1 & 0xFFFFu) == 0xFFFFu) && ((id2 & 0xFFFFu) == 0xFFFFu))
        {
            if (firstAllFFFFAddr == 0xFFu)
            {
                firstAllFFFFAddr = i;
            }
            if (allFFFFCount < 0xFFu)
            {
                allFFFFCount++;
            }
        }

        if (((id1 & 0xFFFFu) != 0u && (id1 & 0xFFFFu) != 0xFFFFu) ||
            ((id2 & 0xFFFFu) != 0u && (id2 & 0xFFFFu) != 0xFFFFu))
        {
            firstAddr = i;
            firstId1 = (uint16_t)(id1 & 0xFFFFu);
            firstId2 = (uint16_t)(id2 & 0xFFFFu);
            break;
        }
    }
#endif

    g_cubley_diag_mac_probe0 = ((uint32_t)0xB1 << 24) | ((uint32_t)phyAddr << 16) | (miiData & 0xFFFFu);
    g_cubley_diag_mac_probe1 = miiControl;
    g_cubley_diag_mac_probe2 = ((uint32_t)0xB2 << 24) | ((uint32_t)firstAddr << 16) | (uint32_t)firstId1;
    g_cubley_diag_mac_probe3 = ((uint32_t)0xB3 << 24) | ((uint32_t)firstAddr << 16) | (uint32_t)firstId2;
    g_cubley_diag_mac_probe4 = ((uint32_t)0xB4 << 24) | ((uint32_t)firstAllZeroAddr << 16) |
                               ((uint32_t)allZeroCount << 8) | (uint32_t)allFFFFCount;
    g_cubley_diag_mac_probe5 = ((uint32_t)0xB5 << 24) | ((uint32_t)firstAllFFFFAddr << 16);
}

void CubleySystemHaltHook(const char *reason)
{
    const uint8_t reasonCode = HaltReasonCode(reason);

    if (reasonCode == 0x31)
    {
        CaptureMacFailureContext();
    }

    // 0xE0 stage marks fatal pre-scheduler halts (for example MAC/PHY bring-up).
    SetStartupTrace(0xE0, reasonCode);
    SetStartupDiag(0xE0, 0xEE, reasonCode);
    SetStartupErr(0xE0, 0xEE, reasonCode);
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

#if (CUBLEY_WIRE_PROTOCOL_USB != TRUE)
static void __attribute__((unused)) ForceUsart3PinsOnPd8Pd9(void)
{
    RCC->AHB1ENR |= RCC_AHB1ENR_GPIODEN;
    (void)RCC->AHB1ENR;

    GPIOD->MODER &= ~((3u << (8u * 2u)) | (3u << (9u * 2u)));
    GPIOD->MODER |= ((2u << (8u * 2u)) | (2u << (9u * 2u)));

    GPIOD->OTYPER &= ~((1u << 8u) | (1u << 9u));
    GPIOD->OSPEEDR |= ((3u << (8u * 2u)) | (3u << (9u * 2u)));
    GPIOD->PUPDR &= ~((3u << (8u * 2u)) | (3u << (9u * 2u)));

    GPIOD->AFRH &= ~((0xFu << ((8u - 8u) * 4u)) | (0xFu << ((9u - 8u) * 4u)));
    GPIOD->AFRH |= ((7u << ((8u - 8u) * 4u)) | (7u << ((9u - 8u) * 4u)));
}
#endif

int main(void)
{
    SetStartupTrace(0xC0, 1);
    SetStartupDiag(0xC0, 0, 1);
    SetStartupErr(0xC0, 0, 1);
    SetStartupDiag(0xC0, 2, (CUBLEY_USB_CDC_ACTIVE ? 1 : 0));  // Diagnostic: is USB_CDC active?

#if (HAL_USE_MAC == TRUE)
    SetStartupTrace(0xC0, 0xA1);
    SetStartupDiag(0xC0, 2, 0xA1); // MAC path enabled in this image.
#else
    SetStartupTrace(0xC0, 0xA0);
    SetStartupDiag(0xC0, 2, 0xA0); // MAC path disabled in this image.
#endif

    SetStartupTrace(0xC1, 0x10);
    SetStartupDiag(0xC1, 1, 0x10); // halInit entering.
    halInit();
    SetStartupTrace(0xC1, 0x11);
    SetStartupDiag(0xC1, 1, 0x11); // halInit returned.

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

    palSetLineMode(PAL_LINE(GPIOD, 8U), PAL_MODE_ALTERNATE(7));
    palSetLineMode(PAL_LINE(GPIOD, 9U), PAL_MODE_ALTERNATE(7));
    ForceUsart3PinsOnPd8Pd9();

    sdStart(&SD3, &usart3_cfg);
#endif

    SetStartupDiag(0xC4, 0, 1);
    SetStartupErr(0xC4, 0, 1);

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