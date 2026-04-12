/**
 * @file w5500_interop.cpp
 * @brief nanoFramework interop implementation for native W5500 socket transport
 */

#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <nanoCLR_Checks.h>
#include <hal.h>
#include <string.h>
#include <stdlib.h>
#include "board_cubley.h"

extern volatile uint32_t g_w5500_bringup_status;
extern volatile uint32_t g_w5500_last_native_error;

enum w5500_socket_status_t
{
    W5500_SOCKET_OK = 0,
    W5500_SOCKET_INVALID_PARAM = 1,
    W5500_SOCKET_NOT_INITIALIZED = 2,
    W5500_SOCKET_BUSY = 3,
    W5500_SOCKET_TIMEOUT = 4,
    W5500_SOCKET_NOT_SUPPORTED = 5,
    W5500_SOCKET_IO_ERROR = 6
};

static const int32_t kSingleSocketHandle = 1;
static const uint8_t kSocketIndex = 0;

static const uint8_t W5500_MR = 0x0000;
static const uint8_t W5500_GAR = 0x0001;
static const uint8_t W5500_SUBR = 0x0005;
static const uint8_t W5500_SHAR = 0x0009;
static const uint8_t W5500_SIPR = 0x000F;
static const uint8_t W5500_RTR = 0x0019;
static const uint8_t W5500_RCR = 0x001B;
static const uint16_t W5500_PHYCFGR = 0x002E;
static const uint8_t W5500_VERSIONR = 0x0039;

static const uint8_t W5500_PHYCFGR_LNK = 0x01;
static const uint8_t W5500_PHYCFGR_SPD = 0x02;
static const uint8_t W5500_PHYCFGR_DPX = 0x04;
static const uint8_t W5500_PHYCFGR_OPMDC_MASK = 0x38;
static const uint8_t W5500_PHYCFGR_OPMD = 0x40;
static const uint8_t W5500_PHYCFGR_RST = 0x80;
static const uint8_t W5500_PHYCFGR_OPMDC_ALL_AUTO = 0x38; // all-capable auto-negotiation (prefers highest common mode)

static const uint16_t Sn_MR = 0x0000;
static const uint16_t Sn_CR = 0x0001;
static const uint16_t Sn_IR = 0x0002;
static const uint16_t Sn_SR = 0x0003;
static const uint16_t Sn_PORT = 0x0004;
static const uint16_t Sn_DIPR = 0x000C;
static const uint16_t Sn_DPORT = 0x0010;
static const uint16_t Sn_TX_FSR = 0x0020;
static const uint16_t Sn_TX_WR = 0x0024;
static const uint16_t Sn_RX_RSR = 0x0026;
static const uint16_t Sn_RX_RD = 0x0028;
static const uint16_t Sn_RXBUF_SIZE = 0x001E;
static const uint16_t Sn_TXBUF_SIZE = 0x001F;

static const uint8_t W5500_SOCK_MODE_TCP = 0x01;
static const uint8_t W5500_CMD_OPEN = 0x01;
static const uint8_t W5500_CMD_CONNECT = 0x04;
static const uint8_t W5500_CMD_DISCON = 0x08;
static const uint8_t W5500_CMD_CLOSE = 0x10;
static const uint8_t W5500_CMD_SEND = 0x20;
static const uint8_t W5500_CMD_RECV = 0x40;

static const uint8_t W5500_SOCK_CLOSED = 0x00;
static const uint8_t W5500_SOCK_INIT = 0x13;
static const uint8_t W5500_SOCK_ESTABLISHED = 0x17;
static const uint8_t W5500_SOCK_CLOSE_WAIT = 0x1C;

static const uint8_t W5500_IR_CON = 0x01;
static const uint8_t W5500_IR_TIMEOUT = 0x08;
static const uint8_t W5500_IR_SENDOK = 0x10;
static const uint8_t W5500_IR_RECV = 0x04;

static const uint8_t W5500_BSB_COMMON = 0x00;
static const uint8_t W5500_BSB_SOCKET_REG = 0x01;
static const uint8_t W5500_BSB_SOCKET_TX = 0x02;
static const uint8_t W5500_BSB_SOCKET_RX = 0x03;

static const uint16_t kDefaultSourcePort = 50000;
static const uint16_t kDefaultRetryTime = 2000;
static const uint8_t kDefaultRetryCount = 3;

static uint8_t g_networkMac[6] = {0x02, 0x08, 0xDC, 0x00, 0x00, 0x01};
static uint8_t g_networkGateway[4] = {192, 168, 1, 1};
static uint8_t g_networkSubnet[4] = {255, 255, 255, 0};
static uint8_t g_networkIp[4] = {192, 168, 1, 123};

static bool g_initialized = false;
static bool g_socketAllocated = false;
static bool g_socketConnected = false;
static uint16_t g_nextSourcePort = kDefaultSourcePort;

static inline void set_w5500_bringup_status(uint8_t stage, uint8_t result, uint8_t detail)
{
    g_w5500_bringup_status = ((uint32_t)0xD5 << 24) | ((uint32_t)stage << 16) | ((uint32_t)result << 8) | (uint32_t)detail;
}

static inline void set_w5500_last_native_error(uint8_t op, uint8_t code, uint8_t detail)
{
    // 0xE1 marker | op | code | detail (sticky until next update).
    g_w5500_last_native_error = ((uint32_t)0xE1 << 24) | ((uint32_t)op << 16) | ((uint32_t)code << 8) | (uint32_t)detail;
}

// Hardware SPI2 for W5500: PB12=NSS, PB13=SCK, PB14=MISO, PB15=MOSI (all AF5).
// APB1=42 MHz; BR[2:0]=010 -> fPCLK/8 ~5.25 MHz, mode 0 (CPOL=0 CPHA=0).
static SPIConfig g_spi2cfg;
static bool g_spi2cfg_initialized = false;
// SPI DMA on STM32F4 cannot access CCM stack (0x1000xxxx), so keep transfer
// buffers in static SRAM-backed storage.
static uint8_t g_w5500_spi_tx4[4];
static uint8_t g_w5500_spi_rx4[4];
static uint8_t g_w5500_spi_hdr3[3];
static uint8_t g_w5500_spi_word2[2];
static uint8_t g_w5500_last_spi_rx0 = 0;
static uint8_t g_w5500_last_spi_rx1 = 0;
static uint8_t g_w5500_last_spi_ctrl = 0;
static uint8_t g_w5500_last_spi_data = 0;
// CS transition bits sampled around a 4-byte read transaction:
// bit3=before select, bit2=after select, bit1=before unselect, bit0=after unselect.
// Expected for active-low CS is 0b1001 (0x9).
static uint8_t g_w5500_last_cs_bits = 0;
// PB12 high->low->high GPIO sanity code from w5500_hw_init() (expected 0b101 = 0x5).
static uint8_t g_w5500_cs_gpio_code = 0;

static inline void w5500_cs_assert(void)
{
    palClearLine(W5500_CS_LINE);
}

static inline void w5500_cs_release(void)
{
    palSetLine(W5500_CS_LINE);
}

static void w5500_spi_prepare_config(void)
{
    if (g_spi2cfg_initialized)
    {
        return;
    }

    memset(&g_spi2cfg, 0, sizeof(g_spi2cfg));

#if (SPI_SUPPORTS_CIRCULAR == TRUE)
    g_spi2cfg.circular = false;
#endif

#if defined(HAL_LLD_SELECT_SPI_V2)
#if (SPI_SUPPORTS_SLAVE_MODE == TRUE)
    g_spi2cfg.slave = false;
#endif
    g_spi2cfg.data_cb = NULL;
    g_spi2cfg.error_cb = NULL;
#else
    g_spi2cfg.end_cb = NULL;
#endif

#if (SPI_SELECT_MODE == SPI_SELECT_MODE_LINE)
    g_spi2cfg.ssline = W5500_CS_LINE;
#elif (SPI_SELECT_MODE == SPI_SELECT_MODE_PORT)
    g_spi2cfg.ssport = GPIOB;
    g_spi2cfg.ssmask = (ioportmask_t)(1U << 12U);
#elif (SPI_SELECT_MODE == SPI_SELECT_MODE_PAD)
    g_spi2cfg.ssport = GPIOB;
    g_spi2cfg.sspad = 12U;
#else
#error Unsupported SPI_SELECT_MODE for W5500 SPI config
#endif

    g_spi2cfg.cr1 = SPI_CR1_BR_1;
    g_spi2cfg.cr2 = 0U;
    g_spi2cfg_initialized = true;
}

static void w5500_spi_start(void)
{
    w5500_spi_prepare_config();
    // Early init can run before scheduler start; avoid mutex-based bus acquire there.
    spiStart(&SPID2, &g_spi2cfg);
}

static void w5500_spi_set_cr1(uint16_t cr1)
{
    g_spi2cfg.cr1 = cr1;
    spiStop(&SPID2);
    spiStart(&SPID2, &g_spi2cfg);
}

static void w5500_scope_spi_clock_burst()
{
    // Scope-assist for low-end DSO capture: emit repeated, slow SPI bursts with CS low.
    static uint8_t tx[32];
    static uint8_t rx[32];

    for (size_t i = 0; i < sizeof(tx); i++)
    {
        tx[i] = (uint8_t)((i & 1U) != 0U ? 0xAAU : 0x55U);
    }

    // Slowest prescaler gives the widest pulses for easy visual confirmation.
    w5500_spi_set_cr1((uint16_t)(SPI_CR1_BR_2 | SPI_CR1_BR_1 | SPI_CR1_BR_0));

    for (int burst = 0; burst < 12; burst++)
    {
        memset(rx, 0, sizeof(rx));
        w5500_cs_assert();
        spiExchange(&SPID2, sizeof(tx), tx, rx);
        w5500_cs_release();
        chThdSleepMilliseconds(20);
    }

    // Restore default probe baseline before entering normal init sequence.
    w5500_spi_set_cr1(SPI_CR1_BR_1);
}

static void w5500_scope_versionr_stream(uint8_t *lastVersion, uint8_t *nonZeroCount)
{
    static uint8_t tx4[4];
    static uint8_t rx4[4];

    tx4[0] = (uint8_t)(W5500_VERSIONR >> 8);
    tx4[1] = (uint8_t)(W5500_VERSIONR & 0xFF);
    tx4[2] = (uint8_t)((W5500_BSB_COMMON << 3) | 0x00);
    tx4[3] = 0x00;

    uint8_t last = 0;
    uint8_t nonZero = 0;

    // Slow, repeated valid reads to make MISO transitions visible on entry-level scopes.
    w5500_spi_set_cr1((uint16_t)(SPI_CR1_BR_2 | SPI_CR1_BR_1 | SPI_CR1_BR_0));

    for (int i = 0; i < 48; i++)
    {
        memset(rx4, 0, sizeof(rx4));
        w5500_cs_assert();
        spiExchange(&SPID2, 4U, tx4, rx4);
        w5500_cs_release();

        last = rx4[3];
        if (rx4[3] != 0)
        {
            nonZero++;
        }

        chThdSleepMilliseconds(8);
    }

    w5500_spi_set_cr1(SPI_CR1_BR_1);

    if (lastVersion != NULL)
    {
        *lastVersion = last;
    }

    if (nonZeroCount != NULL)
    {
        *nonZeroCount = nonZero;
    }
}


static inline uint8_t socket_reg_bsb(uint8_t socket)
{
    return (uint8_t)(W5500_BSB_SOCKET_REG + (socket * 4));
}

static inline uint8_t socket_tx_bsb(uint8_t socket)
{
    return (uint8_t)(W5500_BSB_SOCKET_TX + (socket * 4));
}

static inline uint8_t socket_rx_bsb(uint8_t socket)
{
    return (uint8_t)(W5500_BSB_SOCKET_RX + (socket * 4));
}

// Hardware SPI transaction helpers.
// The W5500 frame format is: [ADDR_HI][ADDR_LO][BSB|RW] followed by data bytes.
// spiSelect/spiUnselect toggle PB12 CS via the SPIConfig ssport/sspad.
static uint8_t w5500_read8(uint16_t address, uint8_t bsb)
{
    g_w5500_spi_tx4[0] = (uint8_t)(address >> 8);
    g_w5500_spi_tx4[1] = (uint8_t)(address & 0xFF);
    g_w5500_spi_tx4[2] = (uint8_t)((bsb << 3) | 0x00);
    g_w5500_spi_tx4[3] = 0x00;

    g_w5500_spi_rx4[0] = 0;
    g_w5500_spi_rx4[1] = 0;
    g_w5500_spi_rx4[2] = 0;
    g_w5500_spi_rx4[3] = 0;

    uint8_t csBits = 0;
    if (palReadLine(W5500_CS_LINE) != 0)
    {
        csBits |= 0x08;
    }

    w5500_cs_assert();
    if (palReadLine(W5500_CS_LINE) != 0)
    {
        csBits |= 0x04;
    }

    spiExchange(&SPID2, 4U, g_w5500_spi_tx4, g_w5500_spi_rx4);

    if (palReadLine(W5500_CS_LINE) != 0)
    {
        csBits |= 0x02;
    }

    w5500_cs_release();
    if (palReadLine(W5500_CS_LINE) != 0)
    {
        csBits |= 0x01;
    }

    // Keep latest raw response bytes for SWD diagnostics.
    g_w5500_last_spi_rx0 = g_w5500_spi_rx4[0];
    g_w5500_last_spi_rx1 = g_w5500_spi_rx4[1];
    g_w5500_last_spi_ctrl = g_w5500_spi_rx4[2];
    g_w5500_last_spi_data = g_w5500_spi_rx4[3];
    g_w5500_last_cs_bits = csBits;

    return g_w5500_spi_rx4[3];
}

static void w5500_write8(uint16_t address, uint8_t bsb, uint8_t value)
{
    g_w5500_spi_tx4[0] = (uint8_t)(address >> 8);
    g_w5500_spi_tx4[1] = (uint8_t)(address & 0xFF);
    g_w5500_spi_tx4[2] = (uint8_t)((bsb << 3) | 0x04);
    g_w5500_spi_tx4[3] = value;

    w5500_cs_assert();
    spiSend(&SPID2, 4U, g_w5500_spi_tx4);
    w5500_cs_release();
}

static void w5500_read_buf(uint16_t address, uint8_t bsb, uint8_t* out, uint16_t length)
{
    g_w5500_spi_hdr3[0] = (uint8_t)(address >> 8);
    g_w5500_spi_hdr3[1] = (uint8_t)(address & 0xFF);
    g_w5500_spi_hdr3[2] = (uint8_t)((bsb << 3) | 0x00);

    w5500_cs_assert();
    spiSend(&SPID2, 3U, g_w5500_spi_hdr3);
    spiReceive(&SPID2, (size_t)length, out);
    w5500_cs_release();
}

static void w5500_write_buf(uint16_t address, uint8_t bsb, const uint8_t* data, uint16_t length)
{
    g_w5500_spi_hdr3[0] = (uint8_t)(address >> 8);
    g_w5500_spi_hdr3[1] = (uint8_t)(address & 0xFF);
    g_w5500_spi_hdr3[2] = (uint8_t)((bsb << 3) | 0x04);

    w5500_cs_assert();
    spiSend(&SPID2, 3U, g_w5500_spi_hdr3);
    spiSend(&SPID2, (size_t)length, data);
    w5500_cs_release();
}

static uint16_t w5500_read16(uint16_t address, uint8_t bsb)
{
    w5500_read_buf(address, bsb, g_w5500_spi_word2, 2);
    return (uint16_t)((g_w5500_spi_word2[0] << 8) | g_w5500_spi_word2[1]);
}

static void w5500_write16(uint16_t address, uint8_t bsb, uint16_t value)
{
    g_w5500_spi_word2[0] = (uint8_t)(value >> 8);
    g_w5500_spi_word2[1] = (uint8_t)(value & 0xFF);
    w5500_write_buf(address, bsb, g_w5500_spi_word2, 2);
}

static bool w5500_wait_command_done(uint8_t socket, int32_t timeoutMs)
{
    int32_t elapsed = 0;
    while (w5500_read8(Sn_CR, socket_reg_bsb(socket)) != 0)
    {
        if (elapsed >= timeoutMs)
        {
            return false;
        }

        chThdSleepMilliseconds(1);
        elapsed++;
    }

    return true;
}

static bool w5500_issue_socket_command(uint8_t socket, uint8_t command, int32_t timeoutMs)
{
    w5500_write8(Sn_CR, socket_reg_bsb(socket), command);
    return w5500_wait_command_done(socket, timeoutMs);
}

static void w5500_socket_close(uint8_t socket)
{
    w5500_issue_socket_command(socket, W5500_CMD_CLOSE, 50);
    w5500_write8(Sn_IR, socket_reg_bsb(socket), 0xFF);
}

static bool parse_ipv4(const char* text, uint8_t out[4])
{
    if (text == NULL)
    {
        return false;
    }

    int idx = 0;
    int value = 0;
    bool hasDigit = false;

    for (const char* p = text; ; ++p)
    {
        char c = *p;

        if (c >= '0' && c <= '9')
        {
            value = (value * 10) + (c - '0');
            if (value > 255)
            {
                return false;
            }
            hasDigit = true;
            continue;
        }

        if (c == '.' || c == '\0')
        {
            if (!hasDigit || idx > 3)
            {
                return false;
            }

            out[idx++] = (uint8_t)value;
            value = 0;
            hasDigit = false;

            if (c == '\0')
            {
                break;
            }

            continue;
        }

        return false;
    }

    return idx == 4;
}

static int hex_nibble(char c)
{
    if (c >= '0' && c <= '9')
    {
        return c - '0';
    }

    if (c >= 'A' && c <= 'F')
    {
        return 10 + (c - 'A');
    }

    if (c >= 'a' && c <= 'f')
    {
        return 10 + (c - 'a');
    }

    return -1;
}

static bool parse_mac(const char* text, uint8_t out[6])
{
    if (text == NULL)
    {
        return false;
    }

    for (int i = 0; i < 6; i++)
    {
        int hi = hex_nibble(*text++);
        int lo = hex_nibble(*text++);

        if (hi < 0 || lo < 0)
        {
            return false;
        }

        out[i] = (uint8_t)((hi << 4) | lo);

        if (i < 5)
        {
            if (*text++ != ':')
            {
                return false;
            }
        }
    }

    return *text == '\0';
}

static void w5500_apply_network_settings()
{
    w5500_write_buf(W5500_GAR, W5500_BSB_COMMON, g_networkGateway, 4);
    w5500_write_buf(W5500_SUBR, W5500_BSB_COMMON, g_networkSubnet, 4);
    w5500_write_buf(W5500_SHAR, W5500_BSB_COMMON, g_networkMac, 6);
    w5500_write_buf(W5500_SIPR, W5500_BSB_COMMON, g_networkIp, 4);
}

static w5500_socket_status_t w5500_hw_init()
{
    set_w5500_last_native_error(0x40, 0x00, 0x00);

    // Configure W5500 control pins.
    // SPI2 pins (PB12-PB15) are already AF5 via board_cubley.h; no palSetLineMode needed.
    // But explicitly enforce AF5 at runtime to catch any mux config issues.
    palSetLineMode(PAL_LINE(GPIOB, 13U), PAL_MODE_ALTERNATE(5));  // SCK
    palSetLineMode(PAL_LINE(GPIOB, 14U), PAL_MODE_ALTERNATE(5));  // MISO
    palSetLineMode(PAL_LINE(GPIOB, 15U), PAL_MODE_ALTERNATE(5));  // MOSI
    palSetLineMode(W5500_CS_LINE, PAL_MODE_OUTPUT_PUSHPULL);
    w5500_cs_release();
    palSetLineMode(W5500_RESET_LINE, PAL_MODE_OUTPUT_PUSHPULL);
    palSetLineMode(W5500_INT_LINE, PAL_MODE_INPUT_PULLUP);

    // Scope-assist: Drive PB13 and PB15 as slow GPIO pulses for 500 ms so single-channel scope can catch them.
    // This proves the physical probe paths and GPIO control work before enabling SPI peripheral.
    palSetLineMode(PAL_LINE(GPIOB, 13U), PAL_MODE_OUTPUT_PUSHPULL);
    palSetLineMode(PAL_LINE(GPIOB, 15U), PAL_MODE_OUTPUT_PUSHPULL);
    for (int pulse = 0; pulse < 20; pulse++)
    {
        palSetLine(PAL_LINE(GPIOB, 13U));
        palSetLine(PAL_LINE(GPIOB, 15U));
        chThdSleepMilliseconds(12);
        palClearLine(PAL_LINE(GPIOB, 13U));
        palClearLine(PAL_LINE(GPIOB, 15U));
        chThdSleepMilliseconds(12);
    }
    // Now switch back to AF5 for real SPI use.
    palSetLineMode(PAL_LINE(GPIOB, 13U), PAL_MODE_ALTERNATE(5));  // SCK
    palSetLineMode(PAL_LINE(GPIOB, 15U), PAL_MODE_ALTERNATE(5));  // MOSI

    // op 0x49: PB12 software-drive sanity check (high->low->high).
    // code bits: b2=readback after first high, b1=after low, b0=after final high.
    // detail bits: high nibble=ODR snapshots (same phase order), low nibble=IDR snapshots.
    uint8_t cs_hi1 = (palReadLine(W5500_CS_LINE) != 0) ? 1U : 0U;
    w5500_cs_assert();
    uint8_t cs_lo = (palReadLine(W5500_CS_LINE) != 0) ? 1U : 0U;
    w5500_cs_release();
    uint8_t cs_hi2 = (palReadLine(W5500_CS_LINE) != 0) ? 1U : 0U;
    uint8_t odr_hi1 = (uint8_t)((GPIOB->ODR & (1U << 12U)) ? 1U : 0U);
    w5500_cs_assert();
    uint8_t odr_lo = (uint8_t)((GPIOB->ODR & (1U << 12U)) ? 1U : 0U);
    w5500_cs_release();
    uint8_t odr_hi2 = (uint8_t)((GPIOB->ODR & (1U << 12U)) ? 1U : 0U);
    g_w5500_cs_gpio_code = (uint8_t)((cs_hi1 << 2) | (cs_lo << 1) | cs_hi2);
    set_w5500_last_native_error(
        0x49,
        g_w5500_cs_gpio_code,
        (uint8_t)(((odr_hi1 << 6) | (odr_lo << 5) | (odr_hi2 << 4)) | g_w5500_cs_gpio_code));

    // Start hardware SPI2 driver (acquires bus, applies g_spi2cfg).
    w5500_spi_start();

    // op 0x4B: SPI2 register state after start (CR1 high nibble, SR low nibble).
    // If CR1 is 0, SPI2 likely never started.  SR bit 1 (TXE)=1 means TX buffer empty.
    uint8_t cr1_bits = (uint8_t)((SPID2.config->cr1 & 0xF0) >> 4);
    uint8_t sr_bits = (uint8_t)(SPI2->SR & 0x0F);
    set_w5500_last_native_error(0x4B, cr1_bits, sr_bits);

    // op 0x4C: run a long, slow SPI burst to make SCK/MOSI/CS visible on low-cost scopes.
    set_w5500_last_native_error(0x4C, 0x01, 0x00);
    w5500_scope_spi_clock_burst();
    set_w5500_last_native_error(0x4C, 0x02, 0x00);

    // Hardware reset: assert for 20 ms, then release and wait 50 ms for W5500 POR.
    palClearLine(W5500_RESET_LINE);
    chThdSleepMilliseconds(20);
    palSetLine(W5500_RESET_LINE);
    chThdSleepMilliseconds(50);

    // op 0x4D: slow repeated VERSIONR stream summary for scope-limited MISO diagnosis.
    // code=last VERSIONR byte seen, detail=count of non-zero VERSIONR samples across stream.
    uint8_t streamLastVersion = 0;
    uint8_t streamNonZeroCount = 0;
    w5500_scope_versionr_stream(&streamLastVersion, &streamNonZeroCount);
    set_w5500_last_native_error(0x4D, streamLastVersion, streamNonZeroCount);

    // Probe SPI mode/speed combinations to isolate board-level timing/phase issues.
    // code 0x00: mode0 fast  (~5.25 MHz)
    // code 0x01: mode0 slow  (~164 kHz)
    // code 0x02: mode3 slow  (~164 kHz)
    // code 0x03: mode3 fast  (~5.25 MHz)
    static const uint16_t kProbeCr1[] = {
        SPI_CR1_BR_1,
        (uint16_t)(SPI_CR1_BR_2 | SPI_CR1_BR_1 | SPI_CR1_BR_0),
        (uint16_t)(SPI_CR1_BR_2 | SPI_CR1_BR_1 | SPI_CR1_BR_0 | SPI_CR1_CPOL | SPI_CR1_CPHA),
        (uint16_t)(SPI_CR1_BR_1 | SPI_CR1_CPOL | SPI_CR1_CPHA)
    };

    static const uint8_t kProbeCode[] = {0x00, 0x01, 0x02, 0x03};

    uint8_t version = 0;
    uint8_t selectedProbeCode = 0xFF;

    for (size_t p = 0; p < (sizeof(kProbeCr1) / sizeof(kProbeCr1[0])); p++)
    {
        w5500_spi_set_cr1(kProbeCr1[p]);
        chThdSleepMilliseconds(2);

        for (int i = 0; i < 3; i++)
        {
            version = w5500_read8(W5500_VERSIONR, W5500_BSB_COMMON);
            if (version == 0x04)
            {
                selectedProbeCode = kProbeCode[p];
                break;
            }
            chThdSleepMilliseconds(2);
        }

        // op 0x47: per-probe VERSIONR sample.
        // detail high nibble=CS transition bits, low nibble=raw SPI control echo low nibble.
        set_w5500_last_native_error(
            0x47,
            (uint8_t)((kProbeCode[p] << 4) | (version & 0x0F)),
            (uint8_t)((g_w5500_last_cs_bits << 4) | (g_w5500_cs_gpio_code & 0x0F)));

        if (version == 0x04)
        {
            break;
        }
    }

    if (selectedProbeCode != 0xFF)
    {
        // op 0x48: selected working probe code in detail.
        set_w5500_last_native_error(0x48, 0x04, selectedProbeCode);
    }

    // Always capture PHY config register for diagnostic mailbox.
    uint8_t phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);

    if (version != 0x04)
    {
        // Report raw VERSIONR and PHYCFGR for board-level diagnostics.
        set_w5500_bringup_status(0xA0, version, phycfgr);
        // op 0x41: code=VERSIONR read.
        // detail high nibble=CS transition bits, low nibble=raw SPI control echo low nibble.
        set_w5500_last_native_error(
            0x41,
            version,
            (uint8_t)((g_w5500_last_cs_bits << 4) | (g_w5500_cs_gpio_code & 0x0F)));
        // op 0x4A: raw RX bytes from last VERSIONR 4-byte frame.
        // code=rx0, detail=rx1 (rx2/rx3 remain available via existing globals and op 0x41 context).
        set_w5500_last_native_error(0x4A, g_w5500_last_spi_rx0, g_w5500_last_spi_rx1);
        return (w5500_socket_status_t)(0x20 | (version & 0x0F));
    }

    // Capture PHY mode immediately after hardware reset/probe, before MR software reset.
    // Note: when OPMD=0 (HW mode), OPMDC field interpretation is limited; do not infer exact
    // PMODE pin levels from OPMDC alone without physical measurement.
    set_w5500_last_native_error(
        0x44,
        (uint8_t)((phycfgr & W5500_PHYCFGR_OPMDC_MASK) >> 3),
        phycfgr);

    w5500_write8(W5500_MR, W5500_BSB_COMMON, 0x80);
    // Allow enough time for MR software reset to complete before touching PHYCFGR.
    chThdSleepMilliseconds(50);

    // Step 1: Write desired SW configuration with RST deasserted (bit7=1, active-low reset).
    // Keep PHY out of reset while establishing OPMD/OPMDC.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_RST | W5500_PHYCFGR_OPMD | W5500_PHYCFGR_OPMDC_ALL_AUTO));
    chThdSleepMilliseconds(10);

    // op 0x46: readback after step-1 write. If OPMD=0 here, SW-mode write did not stick.
    // Expected high bits: PHYCFGR[7:3]=11111b (RST=1, OPMD=1, OPMDC=7).
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
    set_w5500_last_native_error(0x46, (uint8_t)((phycfgr & W5500_PHYCFGR_OPMD) != 0 ? 0xA1 : 0xA0), phycfgr);

    // Step 2: Trigger PHY reset (active-low): drive RST bit low while preserving SW mode bits.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_OPMD | W5500_PHYCFGR_OPMDC_ALL_AUTO));
    chThdSleepMilliseconds(50);

    // Intermediate readback (op 0x43): may still show RST=0 while reset is in progress.
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
    set_w5500_last_native_error(0x43, (uint8_t)((phycfgr & W5500_PHYCFGR_OPMD) != 0 ? 0xA1 : 0xA0), phycfgr);

    // Poll until RST bit self-clears back to 1 (datasheet: ~3ms; observed to take longer in some runs).
    // Timeout after 3 seconds, then continue. We always re-assert SW mode afterward.
    for (int rst_poll = 0; rst_poll < 300; rst_poll++)
    {
        chThdSleepMilliseconds(10);
        phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
        if ((phycfgr & W5500_PHYCFGR_RST) != 0)
        {
            break;
        }
    }

    // Re-assert SW all-auto mode regardless of poll outcome to avoid depending on previous
    // reset timing behavior.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_RST | W5500_PHYCFGR_OPMD | W5500_PHYCFGR_OPMDC_ALL_AUTO));
    chThdSleepMilliseconds(5);
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);

    // op 0x45: post-reset settled state after explicit SW-mode re-assert.
    set_w5500_last_native_error(0x45, (uint8_t)((phycfgr & W5500_PHYCFGR_OPMD) != 0 ? 0xA1 : 0xA0), phycfgr);

    w5500_apply_network_settings();
    w5500_write16(W5500_RTR, W5500_BSB_COMMON, kDefaultRetryTime);
    w5500_write8(W5500_RCR, W5500_BSB_COMMON, kDefaultRetryCount);

    w5500_socket_close(kSocketIndex);
    w5500_write8(Sn_RXBUF_SIZE, socket_reg_bsb(kSocketIndex), 2);
    w5500_write8(Sn_TXBUF_SIZE, socket_reg_bsb(kSocketIndex), 2);

    return W5500_SOCKET_OK;
}

extern "C" int cubley_w5500_early_init(void)
{
    if (g_initialized)
    {
        return (int)W5500_SOCKET_OK;
    }

    w5500_socket_status_t initStatus = w5500_hw_init();
    if (initStatus == W5500_SOCKET_OK)
    {
        g_initialized = true;
    }

    return (int)initStatus;
}

static w5500_socket_status_t w5500_connect(uint8_t socket, const uint8_t remoteIp[4], uint16_t remotePort, int32_t timeoutMs)
{
    if (!w5500_issue_socket_command(socket, W5500_CMD_CLOSE, 100))
    {
        return W5500_SOCKET_TIMEOUT;
    }

    w5500_write8(Sn_MR, socket_reg_bsb(socket), W5500_SOCK_MODE_TCP);
    w5500_write16(Sn_PORT, socket_reg_bsb(socket), g_nextSourcePort++);

    if (!w5500_issue_socket_command(socket, W5500_CMD_OPEN, 200))
    {
        return W5500_SOCKET_TIMEOUT;
    }

    if (w5500_read8(Sn_SR, socket_reg_bsb(socket)) != W5500_SOCK_INIT)
    {
        return W5500_SOCKET_IO_ERROR;
    }

    w5500_write_buf(Sn_DIPR, socket_reg_bsb(socket), remoteIp, 4);
    w5500_write16(Sn_DPORT, socket_reg_bsb(socket), remotePort);
    w5500_write8(Sn_IR, socket_reg_bsb(socket), 0xFF);

    if (!w5500_issue_socket_command(socket, W5500_CMD_CONNECT, 200))
    {
        return W5500_SOCKET_TIMEOUT;
    }

    int32_t elapsed = 0;
    while (elapsed < timeoutMs)
    {
        uint8_t status = w5500_read8(Sn_SR, socket_reg_bsb(socket));
        uint8_t ir = w5500_read8(Sn_IR, socket_reg_bsb(socket));

        if (status == W5500_SOCK_ESTABLISHED)
        {
            w5500_write8(Sn_IR, socket_reg_bsb(socket), W5500_IR_CON);
            return W5500_SOCKET_OK;
        }

        if ((ir & W5500_IR_TIMEOUT) != 0 || status == W5500_SOCK_CLOSED)
        {
            w5500_write8(Sn_IR, socket_reg_bsb(socket), W5500_IR_TIMEOUT);
            return W5500_SOCKET_TIMEOUT;
        }

        chThdSleepMilliseconds(1);
        elapsed++;
    }

    return W5500_SOCKET_TIMEOUT;
}

static w5500_socket_status_t w5500_send(uint8_t socket, const uint8_t* data, uint16_t length)
{
    uint8_t status = w5500_read8(Sn_SR, socket_reg_bsb(socket));
    if (status != W5500_SOCK_ESTABLISHED && status != W5500_SOCK_CLOSE_WAIT)
    {
        return W5500_SOCKET_NOT_INITIALIZED;
    }

    int32_t elapsed = 0;
    while (w5500_read16(Sn_TX_FSR, socket_reg_bsb(socket)) < length)
    {
        if (elapsed >= 2000)
        {
            return W5500_SOCKET_TIMEOUT;
        }
        chThdSleepMilliseconds(1);
        elapsed++;
    }

    uint16_t writePtr = w5500_read16(Sn_TX_WR, socket_reg_bsb(socket));
    w5500_write_buf(writePtr, socket_tx_bsb(socket), data, length);
    w5500_write16(Sn_TX_WR, socket_reg_bsb(socket), (uint16_t)(writePtr + length));

    if (!w5500_issue_socket_command(socket, W5500_CMD_SEND, 200))
    {
        return W5500_SOCKET_TIMEOUT;
    }

    elapsed = 0;
    while (elapsed < 2000)
    {
        uint8_t ir = w5500_read8(Sn_IR, socket_reg_bsb(socket));
        if ((ir & W5500_IR_SENDOK) != 0)
        {
            w5500_write8(Sn_IR, socket_reg_bsb(socket), W5500_IR_SENDOK);
            return W5500_SOCKET_OK;
        }

        if ((ir & W5500_IR_TIMEOUT) != 0)
        {
            w5500_write8(Sn_IR, socket_reg_bsb(socket), W5500_IR_TIMEOUT);
            return W5500_SOCKET_TIMEOUT;
        }

        chThdSleepMilliseconds(1);
        elapsed++;
    }

    return W5500_SOCKET_TIMEOUT;
}

static w5500_socket_status_t w5500_receive(uint8_t socket, uint8_t* buffer, uint16_t maxLength, int32_t timeoutMs, uint16_t* outReceived)
{
    *outReceived = 0;

    int32_t elapsed = 0;
    while (elapsed < timeoutMs)
    {
        uint16_t available = w5500_read16(Sn_RX_RSR, socket_reg_bsb(socket));
        if (available > 0)
        {
            uint16_t toRead = available;
            if (toRead > maxLength)
            {
                toRead = maxLength;
            }

            uint16_t readPtr = w5500_read16(Sn_RX_RD, socket_reg_bsb(socket));
            w5500_read_buf(readPtr, socket_rx_bsb(socket), buffer, toRead);
            w5500_write16(Sn_RX_RD, socket_reg_bsb(socket), (uint16_t)(readPtr + toRead));

            if (!w5500_issue_socket_command(socket, W5500_CMD_RECV, 100))
            {
                return W5500_SOCKET_TIMEOUT;
            }

            *outReceived = toRead;
            return W5500_SOCKET_OK;
        }

        uint8_t status = w5500_read8(Sn_SR, socket_reg_bsb(socket));
        if (status == W5500_SOCK_CLOSED)
        {
            return W5500_SOCKET_NOT_INITIALIZED;
        }

        uint8_t ir = w5500_read8(Sn_IR, socket_reg_bsb(socket));
        if ((ir & W5500_IR_RECV) != 0)
        {
            w5500_write8(Sn_IR, socket_reg_bsb(socket), W5500_IR_RECV);
        }

        chThdSleepMilliseconds(1);
        elapsed++;
    }

    return W5500_SOCKET_TIMEOUT;
}

HRESULT Library_cubley_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(2, 0, 0);
    set_w5500_last_native_error(0x10, 0x00, 0x00);

    if (!g_initialized)
    {
        w5500_socket_status_t initStatus = w5500_hw_init();
        if (initStatus != W5500_SOCKET_OK)
        {
            stack.Arg0().NumericByRef().s4 = -1;
            stack.SetResult_I4((int32_t)initStatus);
            set_w5500_bringup_status(2, 14, (uint8_t)initStatus);
            set_w5500_last_native_error(0x11, (uint8_t)initStatus, 0x00);
            NANOCLR_SET_AND_LEAVE(S_OK);
        }

        g_initialized = true;
    }

    if (g_socketAllocated)
    {
        stack.Arg0().NumericByRef().s4 = -1;
        stack.SetResult_I4((int32_t)W5500_SOCKET_BUSY);
        set_w5500_bringup_status(2, 14, (uint8_t)W5500_SOCKET_BUSY);
        set_w5500_last_native_error(0x12, (uint8_t)W5500_SOCKET_BUSY, 0x00);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    g_socketAllocated = true;
    g_socketConnected = false;
    stack.Arg0().NumericByRef().s4 = kSingleSocketHandle;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);
    set_w5500_bringup_status(2, 1, 0);
    set_w5500_last_native_error(0x13, (uint8_t)W5500_SOCKET_OK, 0x00);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(3, 0, 0);

    CLR_RT_HeapBlock* ipArg = &(stack.Arg0());
    CLR_RT_HeapBlock* subnetArg = &(stack.Arg1());
    CLR_RT_HeapBlock* gatewayArg = &(stack.Arg2());
    CLR_RT_HeapBlock* macArg = &(stack.Arg3());

    CLR_RT_HeapBlock_String* ip = NULL;
    CLR_RT_HeapBlock_String* subnet = NULL;
    CLR_RT_HeapBlock_String* gateway = NULL;
    CLR_RT_HeapBlock_String* mac = NULL;

    uint8_t parsedIp[4] = {0};
    uint8_t parsedSubnet[4] = {0};
    uint8_t parsedGateway[4] = {0};
    uint8_t parsedMac[6] = {0};

    ip = ipArg->DereferenceString();
    subnet = subnetArg->DereferenceString();
    gateway = gatewayArg->DereferenceString();
    mac = macArg->DereferenceString();

    FAULT_ON_NULL(ip);
    FAULT_ON_NULL(subnet);
    FAULT_ON_NULL(gateway);
    FAULT_ON_NULL(mac);

    if (!parse_ipv4(ip->StringText(), parsedIp) ||
        !parse_ipv4(subnet->StringText(), parsedSubnet) ||
        !parse_ipv4(gateway->StringText(), parsedGateway) ||
        !parse_mac(mac->StringText(), parsedMac))
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(3, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    memcpy(g_networkIp, parsedIp, sizeof(g_networkIp));
    memcpy(g_networkSubnet, parsedSubnet, sizeof(g_networkSubnet));
    memcpy(g_networkGateway, parsedGateway, sizeof(g_networkGateway));
    memcpy(g_networkMac, parsedMac, sizeof(g_networkMac));

    if (g_initialized)
    {
        w5500_apply_network_settings();
    }

    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);
    set_w5500_bringup_status(3, 1, 0);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(4, 0, 0);

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock* hostArg = &(stack.Arg1());
    int32_t port = stack.Arg2().NumericByRef().s4;
    int32_t timeoutMs = stack.Arg3().NumericByRef().s4;
    CLR_RT_HeapBlock_String* host = NULL;
    uint8_t remoteIp[4] = {0};
    w5500_socket_status_t connectStatus = W5500_SOCKET_IO_ERROR;

    host = hostArg->DereferenceString();
    FAULT_ON_NULL(host);

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_initialized)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(4, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (port < 1 || port > 65535 || timeoutMs < 0)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(4, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (!parse_ipv4(host->StringText(), remoteIp))
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_SUPPORTED);
        set_w5500_bringup_status(4, 14, (uint8_t)W5500_SOCKET_NOT_SUPPORTED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    connectStatus = w5500_connect(kSocketIndex, remoteIp, (uint16_t)port, timeoutMs);
    g_socketConnected = (connectStatus == W5500_SOCKET_OK);
    stack.SetResult_I4((int32_t)connectStatus);
    set_w5500_bringup_status(4, connectStatus == W5500_SOCKET_OK ? 1 : 14, (uint8_t)connectStatus);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(6, 0, 0);

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* dataArray = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;
    uint8_t* payload = NULL;
    w5500_socket_status_t sendStatus = W5500_SOCKET_IO_ERROR;

    FAULT_ON_NULL(dataArray);

    stack.Arg4().NumericByRef().s4 = 0;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_socketConnected || !g_initialized)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_INITIALIZED);
        set_w5500_bringup_status(6, 14, (uint8_t)W5500_SOCKET_NOT_INITIALIZED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || (uint32_t)(offset + count) > dataArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(6, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    payload = (uint8_t*)dataArray->GetFirstElement();
    sendStatus = w5500_send(kSocketIndex, payload + offset, (uint16_t)count);
    if (sendStatus == W5500_SOCKET_OK)
    {
        stack.Arg4().NumericByRef().s4 = count;
    }

    stack.SetResult_I4((int32_t)sendStatus);
    set_w5500_bringup_status(6, sendStatus == W5500_SOCKET_OK ? 1 : 14, (uint8_t)sendStatus);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(7, 0, 0);

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    CLR_RT_HeapBlock_Array* bufferArray = stack.Arg1().DereferenceArray();
    int32_t offset = stack.Arg2().NumericByRef().s4;
    int32_t count = stack.Arg3().NumericByRef().s4;
    int32_t timeoutMs = stack.Arg4().NumericByRef().s4;
    uint8_t* rx = NULL;
    uint16_t received = 0;
    w5500_socket_status_t rxStatus = W5500_SOCKET_IO_ERROR;

    FAULT_ON_NULL(bufferArray);

    stack.Arg5().NumericByRef().s4 = 0;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_socketConnected || !g_initialized)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_INITIALIZED);
        set_w5500_bringup_status(7, 14, (uint8_t)W5500_SOCKET_NOT_INITIALIZED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || timeoutMs < 0 || (uint32_t)(offset + count) > bufferArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(7, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    rx = (uint8_t*)bufferArray->GetFirstElement();
    rxStatus = w5500_receive(kSocketIndex, rx + offset, (uint16_t)count, timeoutMs, &received);
    stack.Arg5().NumericByRef().s4 = received;

    if (rxStatus == W5500_SOCKET_NOT_INITIALIZED)
    {
        g_socketConnected = false;
    }

    stack.SetResult_I4((int32_t)rxStatus);
    if (rxStatus == W5500_SOCKET_TIMEOUT)
    {
        set_w5500_bringup_status(7, 2, (uint8_t)rxStatus);
    }
    else
    {
        set_w5500_bringup_status(7, rxStatus == W5500_SOCKET_OK ? 1 : 14, (uint8_t)rxStatus);
    }

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(8, 0, 0);

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        set_w5500_bringup_status(8, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (g_initialized)
    {
        w5500_issue_socket_command(kSocketIndex, W5500_CMD_DISCON, 100);
        w5500_socket_close(kSocketIndex);
    }

    g_socketConnected = false;
    g_socketAllocated = false;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);
    set_w5500_bringup_status(8, 1, 0);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    set_w5500_bringup_status(5, 0, 0);

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    uint8_t status = W5500_SOCK_CLOSED;
    bool connected = false;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_initialized)
    {
        stack.SetResult_Boolean(false);
        set_w5500_bringup_status(5, 14, (uint8_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    status = w5500_read8(Sn_SR, socket_reg_bsb(kSocketIndex));
    connected = (status == W5500_SOCK_ESTABLISHED || status == W5500_SOCK_CLOSE_WAIT);
    g_socketConnected = connected;
    stack.SetResult_Boolean(connected);
    set_w5500_bringup_status(5, connected ? 1 : 14, connected ? 0 : 1);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeGetPhyStatus___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    uint8_t phycfgr = 0;

    if (!g_initialized)
    {
        stack.SetResult_U4(0);
        set_w5500_last_native_error(0x50, (uint8_t)W5500_SOCKET_NOT_INITIALIZED, 0x00);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
    stack.SetResult_U4((uint32_t)phycfgr);

    // Surface link state snapshots through bringup status for SWD mailbox visibility.
    set_w5500_bringup_status(5, (phycfgr & 0x01) != 0 ? 1 : 14, phycfgr);
    set_w5500_last_native_error(0x51, (phycfgr & 0x01) != 0 ? 0x00 : 0x01, phycfgr);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeGetVersion___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    uint8_t version = 0;
    uint8_t phycfgr = 0;

    if (!g_initialized)
    {
        stack.SetResult_U4(0);
        set_w5500_last_native_error(0x52, (uint8_t)W5500_SOCKET_NOT_INITIALIZED, 0x00);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    version = w5500_read8(W5500_VERSIONR, W5500_BSB_COMMON);
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
    stack.SetResult_U4((uint32_t)version);

    // Surface VERSIONR and PHYCFGR together for SWD-only diagnostics.
    set_w5500_bringup_status(5, version == 0x04 ? 1 : 14, version);
    set_w5500_last_native_error(0x53, version, phycfgr);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeGetVersionPhyStatus___STATIC__U4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    uint8_t version = 0;
    uint8_t phycfgr = 0;
    uint32_t packed = 0;

    if (!g_initialized)
    {
        stack.SetResult_U4(0);
        set_w5500_last_native_error(0x54, (uint8_t)W5500_SOCKET_NOT_INITIALIZED, 0x00);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    version = w5500_read8(W5500_VERSIONR, W5500_BSB_COMMON);
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
    packed = (((uint32_t)version) << 8) | (uint32_t)phycfgr;
    stack.SetResult_U4(packed);

    set_w5500_bringup_status(5, (phycfgr & 0x01) != 0 ? 1 : 14, phycfgr);
    set_w5500_last_native_error(0x54, version, phycfgr);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_cubley_interop_W5500Socket_NativeSetPhyMode___STATIC__U4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t modeCode = stack.Arg0().NumericByRef().s4;
    uint8_t opmdc = 0;
    uint8_t phycfgr = 0;

    if (!g_initialized)
    {
        stack.SetResult_U4(0);
        set_w5500_last_native_error(0x55, (uint8_t)W5500_SOCKET_NOT_INITIALIZED, 0x00);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (modeCode < 0 || modeCode > 7)
    {
        stack.SetResult_U4(0);
        set_w5500_last_native_error(0x55, (uint8_t)W5500_SOCKET_INVALID_PARAM, 0xFF);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    opmdc = (uint8_t)(modeCode & 0x07);

    // Keep PHY in software mode and deassert reset (RST=1) while programming OPMDC.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_RST | W5500_PHYCFGR_OPMD | (uint8_t)(opmdc << 3)));
    chThdSleepMilliseconds(5);

    // Trigger PHY reset (active-low RST=0) to apply mode change.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_OPMD | (uint8_t)(opmdc << 3)));

    for (int rst_poll = 0; rst_poll < 300; rst_poll++)
    {
        chThdSleepMilliseconds(10);
        phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);
        if ((phycfgr & W5500_PHYCFGR_RST) != 0)
        {
            break;
        }
    }

    // Re-assert same SW mode with reset deasserted.
    w5500_write8(
        W5500_PHYCFGR,
        W5500_BSB_COMMON,
        (uint8_t)(W5500_PHYCFGR_RST | W5500_PHYCFGR_OPMD | (uint8_t)(opmdc << 3)));
    chThdSleepMilliseconds(5);
    phycfgr = w5500_read8(W5500_PHYCFGR, W5500_BSB_COMMON);

    stack.SetResult_U4((uint32_t)phycfgr);
    set_w5500_last_native_error(0x55, opmdc, phycfgr);

    NANOCLR_NOCLEANUP();
}
