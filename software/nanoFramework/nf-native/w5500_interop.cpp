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
#include "board_diseqc.h"

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
static const uint8_t W5500_VERSIONR = 0x0039;

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

static const SPIConfig g_w5500SpiConfig = {
    false,
    NULL,
    NULL,
    NULL,
    GPIOB,
    12U,
    SPI_CR1_BR_2 | SPI_CR1_BR_1,
    0
};

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

static uint8_t w5500_read8(uint16_t address, uint8_t bsb)
{
    uint8_t tx[4] = {(uint8_t)(address >> 8), (uint8_t)(address & 0xFF), (uint8_t)((bsb << 3) | 0x00), 0x00};
    uint8_t rx[4] = {0};

    spiSelect(&W5500_SPI_DRIVER);
    spiExchange(&W5500_SPI_DRIVER, sizeof(tx), tx, rx);
    spiUnselect(&W5500_SPI_DRIVER);

    return rx[3];
}

static void w5500_write8(uint16_t address, uint8_t bsb, uint8_t value)
{
    uint8_t tx[4] = {(uint8_t)(address >> 8), (uint8_t)(address & 0xFF), (uint8_t)((bsb << 3) | 0x04), value};

    spiSelect(&W5500_SPI_DRIVER);
    spiSend(&W5500_SPI_DRIVER, sizeof(tx), tx);
    spiUnselect(&W5500_SPI_DRIVER);
}

static void w5500_read_buf(uint16_t address, uint8_t bsb, uint8_t* out, uint16_t length)
{
    uint8_t header[3] = {(uint8_t)(address >> 8), (uint8_t)(address & 0xFF), (uint8_t)((bsb << 3) | 0x00)};

    spiSelect(&W5500_SPI_DRIVER);
    spiSend(&W5500_SPI_DRIVER, sizeof(header), header);
    spiReceive(&W5500_SPI_DRIVER, length, out);
    spiUnselect(&W5500_SPI_DRIVER);
}

static void w5500_write_buf(uint16_t address, uint8_t bsb, const uint8_t* data, uint16_t length)
{
    uint8_t header[3] = {(uint8_t)(address >> 8), (uint8_t)(address & 0xFF), (uint8_t)((bsb << 3) | 0x04)};

    spiSelect(&W5500_SPI_DRIVER);
    spiSend(&W5500_SPI_DRIVER, sizeof(header), header);
    spiSend(&W5500_SPI_DRIVER, length, data);
    spiUnselect(&W5500_SPI_DRIVER);
}

static uint16_t w5500_read16(uint16_t address, uint8_t bsb)
{
    uint8_t tmp[2] = {0};
    w5500_read_buf(address, bsb, tmp, 2);
    return (uint16_t)((tmp[0] << 8) | tmp[1]);
}

static void w5500_write16(uint16_t address, uint8_t bsb, uint16_t value)
{
    uint8_t tmp[2] = {(uint8_t)(value >> 8), (uint8_t)(value & 0xFF)};
    w5500_write_buf(address, bsb, tmp, 2);
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
    spiStart(&W5500_SPI_DRIVER, &g_w5500SpiConfig);

    palClearLine(W5500_RESET_LINE);
    chThdSleepMilliseconds(10);
    palSetLine(W5500_RESET_LINE);
    chThdSleepMilliseconds(120);

    uint8_t version = w5500_read8(W5500_VERSIONR, W5500_BSB_COMMON);
    if (version != 0x04)
    {
        return W5500_SOCKET_IO_ERROR;
    }

    w5500_write8(W5500_MR, W5500_BSB_COMMON, 0x80);
    chThdSleepMilliseconds(5);

    w5500_apply_network_settings();
    w5500_write16(W5500_RTR, W5500_BSB_COMMON, kDefaultRetryTime);
    w5500_write8(W5500_RCR, W5500_BSB_COMMON, kDefaultRetryCount);

    w5500_socket_close(kSocketIndex);
    w5500_write8(Sn_RXBUF_SIZE, socket_reg_bsb(kSocketIndex), 2);
    w5500_write8(Sn_TXBUF_SIZE, socket_reg_bsb(kSocketIndex), 2);

    return W5500_SOCKET_OK;
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

HRESULT Library_diseqc_interop_W5500Socket_NativeOpen___STATIC__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    if (!g_initialized)
    {
        w5500_socket_status_t initStatus = w5500_hw_init();
        if (initStatus != W5500_SOCKET_OK)
        {
            stack.Arg0().NumericByRef().s4 = -1;
            stack.SetResult_I4((int32_t)initStatus);
            NANOCLR_SET_AND_LEAVE(S_OK);
        }

        g_initialized = true;
    }

    if (g_socketAllocated)
    {
        stack.Arg0().NumericByRef().s4 = -1;
        stack.SetResult_I4((int32_t)W5500_SOCKET_BUSY);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    g_socketAllocated = true;
    g_socketConnected = false;
    stack.Arg0().NumericByRef().s4 = kSingleSocketHandle;
    stack.SetResult_I4((int32_t)W5500_SOCKET_OK);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeConfigureNetwork___STATIC__I4__STRING__STRING__STRING__STRING(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

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

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeConnect___STATIC__I4__I4__STRING__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

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
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (port < 1 || port > 65535 || timeoutMs < 0)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (!parse_ipv4(host->StringText(), remoteIp))
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_NOT_SUPPORTED);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    connectStatus = w5500_connect(kSocketIndex, remoteIp, (uint16_t)port, timeoutMs);
    g_socketConnected = (connectStatus == W5500_SOCKET_OK);
    stack.SetResult_I4((int32_t)connectStatus);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeSend___STATIC__I4__I4__SZARRAY_U1__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

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
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || (uint32_t)(offset + count) > dataArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    payload = (uint8_t*)dataArray->GetFirstElement();
    sendStatus = w5500_send(kSocketIndex, payload + offset, (uint16_t)count);
    if (sendStatus == W5500_SOCKET_OK)
    {
        stack.Arg4().NumericByRef().s4 = count;
    }

    stack.SetResult_I4((int32_t)sendStatus);

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeReceive___STATIC__I4__I4__SZARRAY_U1__I4__I4__I4__BYREF_I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

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
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    if (offset < 0 || count < 0 || timeoutMs < 0 || (uint32_t)(offset + count) > bufferArray->m_numOfElements)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
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

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeClose___STATIC__I4__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated)
    {
        stack.SetResult_I4((int32_t)W5500_SOCKET_INVALID_PARAM);
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

    NANOCLR_NOCLEANUP();
}

HRESULT Library_diseqc_interop_W5500Socket_NativeIsConnected___STATIC__BOOLEAN__I4(CLR_RT_StackFrame& stack)
{
    NANOCLR_HEADER();

    int32_t socketHandle = stack.Arg0().NumericByRef().s4;
    uint8_t status = W5500_SOCK_CLOSED;
    bool connected = false;

    if (socketHandle != kSingleSocketHandle || !g_socketAllocated || !g_initialized)
    {
        stack.SetResult_Boolean(false);
        NANOCLR_SET_AND_LEAVE(S_OK);
    }

    status = w5500_read8(Sn_SR, socket_reg_bsb(kSocketIndex));
    connected = (status == W5500_SOCK_ESTABLISHED || status == W5500_SOCK_CLOSE_WAIT);
    g_socketConnected = connected;
    stack.SetResult_Boolean(connected);

    NANOCLR_NOCLEANUP();
}