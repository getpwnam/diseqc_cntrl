//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include "hal.h"

SerialUSBDriver SDU1;

#define USBD1_DATA_REQUEST_EP      1
#define USBD1_DATA_AVAILABLE_EP    1
#define USBD1_INTERRUPT_REQUEST_EP 2

// STM32F4 unique ID registers.
#define DEVICE_ID1 (0x1FFF7A10U)
#define DEVICE_ID2 (0x1FFF7A14U)
#define DEVICE_ID3 (0x1FFF7A18U)

#define USB_STRING_VENDOR L"STMicroelectronics"
#define USB_STRING_DEVICE_DESCRIPTION L"nanoFramework Virtual COM Port"
#define USB_STRING_SERIAL_NUMBER L"NANO_xxxxxxxxxxxx"
#define INDEX_OF_WCHAR_FOR_UNIQUE_ID 5

typedef struct usb_string_vendor
{
    uint8_t bLength;
    uint8_t bDescriptorType;
    wchar_t bPropertyData[sizeof(USB_STRING_VENDOR) / sizeof(wchar_t)];
} usb_string_vendor;

typedef struct usb_string_device_description
{
    uint8_t bLength;
    uint8_t bDescriptorType;
    wchar_t bPropertyData[sizeof(USB_STRING_DEVICE_DESCRIPTION) / sizeof(wchar_t)];
} usb_string_device_description;

typedef struct usb_string_serial_number
{
    uint8_t bLength;
    uint8_t bDescriptorType;
    wchar_t bPropertyData[sizeof(USB_STRING_SERIAL_NUMBER) / sizeof(wchar_t)];
} usb_string_serial_number;

static const uint8_t vcom_device_descriptor_data[18] = {
    USB_DESC_DEVICE(
        0x0200,
        0x02,
        0x00,
        0x00,
        0x40,
        0x0483,
        0x5740,
        0x0200,
        1,
        2,
        3,
        1)};

static const USBDescriptor vcom_device_descriptor = {sizeof vcom_device_descriptor_data, vcom_device_descriptor_data};

static const uint8_t vcom_configuration_descriptor_data[67] = {
    USB_DESC_CONFIGURATION(67, 0x02, 0x01, 0, 0xC0, 50),
    USB_DESC_INTERFACE(0x00, 0x00, 0x01, 0x02, 0x02, 0x01, 0),
    USB_DESC_BYTE(5),
    USB_DESC_BYTE(0x24),
    USB_DESC_BYTE(0x00),
    USB_DESC_BCD(0x0110),
    USB_DESC_BYTE(5),
    USB_DESC_BYTE(0x24),
    USB_DESC_BYTE(0x01),
    USB_DESC_BYTE(0x00),
    USB_DESC_BYTE(0x01),
    USB_DESC_BYTE(4),
    USB_DESC_BYTE(0x24),
    USB_DESC_BYTE(0x02),
    USB_DESC_BYTE(0x02),
    USB_DESC_BYTE(5),
    USB_DESC_BYTE(0x24),
    USB_DESC_BYTE(0x06),
    USB_DESC_BYTE(0x00),
    USB_DESC_BYTE(0x01),
    USB_DESC_ENDPOINT(USBD1_INTERRUPT_REQUEST_EP | 0x80, 0x03, 0x0008, 0xFF),
    USB_DESC_INTERFACE(0x01, 0x00, 0x02, 0x0A, 0x00, 0x00, 0x00),
    USB_DESC_ENDPOINT(USBD1_DATA_AVAILABLE_EP, 0x02, 0x0040, 0x00),
    USB_DESC_ENDPOINT(USBD1_DATA_REQUEST_EP | 0x80, 0x02, 0x0040, 0x00)};

static const USBDescriptor vcom_configuration_descriptor = {
    sizeof vcom_configuration_descriptor_data,
    vcom_configuration_descriptor_data};

static const uint8_t vcom_string0[] = {
    USB_DESC_BYTE(4),
    USB_DESC_BYTE(USB_DESCRIPTOR_STRING),
    USB_DESC_WORD(0x0409)};

static const usb_string_vendor usb_vendor = {
    sizeof(usb_vendor) - sizeof(wchar_t),
    USB_DESC_BYTE(USB_DESCRIPTOR_STRING),
    USB_STRING_VENDOR};

static const usb_string_device_description usb_device_description = {
    sizeof(usb_device_description) - sizeof(wchar_t),
    USB_DESC_BYTE(USB_DESCRIPTOR_STRING),
    USB_STRING_DEVICE_DESCRIPTION};

static usb_string_serial_number usb_serial_number = {
    sizeof(usb_serial_number) - sizeof(wchar_t),
    USB_DESC_BYTE(USB_DESCRIPTOR_STRING),
    USB_STRING_SERIAL_NUMBER};

static const USBDescriptor vcom_strings[] = {
    {sizeof vcom_string0, vcom_string0},
    {sizeof usb_vendor - sizeof(wchar_t), (uint8_t *)(&usb_vendor)},
    {sizeof usb_device_description - sizeof(wchar_t), (uint8_t *)(&usb_device_description)},
    {sizeof usb_serial_number - sizeof(wchar_t), (uint8_t *)(&usb_serial_number)},
};

static void IntToUnicode(uint32_t value, uint8_t *pbuf, uint8_t len)
{
    for (uint8_t idx = 0; idx < len; idx++)
    {
        pbuf[2 * idx] = ((value >> 28) < 0xA) ? ((value >> 28) + '0') : ((value >> 28) + 'A' - 10);
        value <<= 4;
        pbuf[2 * idx + 1] = 0;
    }
}

static void Get_SerialNum(uint8_t *pbuf)
{
    uint32_t deviceserial0 = *(uint32_t *)DEVICE_ID1;
    uint32_t deviceserial1 = *(uint32_t *)DEVICE_ID2;
    uint32_t deviceserial2 = *(uint32_t *)DEVICE_ID3;

    deviceserial0 += deviceserial2;

    if (deviceserial0 != 0)
    {
        IntToUnicode(deviceserial0, pbuf, 8);
        pbuf += 16;
        IntToUnicode(deviceserial1, pbuf, 4);
    }
}

static const USBDescriptor *get_descriptor(USBDriver *usbp, uint8_t dtype, uint8_t dindex, uint16_t lang)
{
    (void)usbp;
    (void)lang;

    switch (dtype)
    {
        case USB_DESCRIPTOR_DEVICE:
            return &vcom_device_descriptor;
        case USB_DESCRIPTOR_CONFIGURATION:
            return &vcom_configuration_descriptor;
        case USB_DESCRIPTOR_STRING:
            if (dindex < 4)
            {
                if (dindex == 3)
                {
                    Get_SerialNum((uint8_t *)&usb_serial_number.bPropertyData[INDEX_OF_WCHAR_FOR_UNIQUE_ID]);
                }
                return &vcom_strings[dindex];
            }
            break;
        default:
            break;
    }

    return NULL;
}

static USBInEndpointState ep1instate;
static USBOutEndpointState ep1outstate;

static const USBEndpointConfig ep1config = {
    USB_EP_MODE_TYPE_BULK,
    NULL,
    sduDataTransmitted,
    sduDataReceived,
    0x0040,
    0x0040,
    &ep1instate,
    &ep1outstate,
    4,
    NULL};

static USBInEndpointState ep2instate;

static const USBEndpointConfig ep2config = {
    USB_EP_MODE_TYPE_INTR,
    NULL,
    sduInterruptTransmitted,
    NULL,
    0x0010,
    0x0000,
    &ep2instate,
    NULL,
    1,
    NULL};

static void usb_event(USBDriver *usbp, usbevent_t event)
{
    switch (event)
    {
        case USB_EVENT_ADDRESS:
            return;

        case USB_EVENT_CONFIGURED:
            chSysLockFromISR();
            usbInitEndpointI(usbp, USBD1_DATA_REQUEST_EP, &ep1config);
            usbInitEndpointI(usbp, USBD1_INTERRUPT_REQUEST_EP, &ep2config);
            sduConfigureHookI(&SDU1);
            chSysUnlockFromISR();
            return;

        case USB_EVENT_RESET:
        case USB_EVENT_UNCONFIGURED:
        case USB_EVENT_SUSPEND:
            chSysLockFromISR();
            sduSuspendHookI(&SDU1);
            chSysUnlockFromISR();
            return;

        case USB_EVENT_WAKEUP:
            chSysLockFromISR();
            sduWakeupHookI(&SDU1);
            chSysUnlockFromISR();
            return;

        case USB_EVENT_STALLED:
            return;
    }
}

static void sof_handler(USBDriver *usbp)
{
    (void)usbp;

    osalSysLockFromISR();
    sduSOFHookI(&SDU1);
    osalSysUnlockFromISR();
}

const USBConfig usbcfg = {usb_event, get_descriptor, sduRequestsHook, sof_handler};

const SerialUSBConfig serusbcfg = {
    &USBD1,
    USBD1_DATA_REQUEST_EP,
    USBD1_DATA_AVAILABLE_EP,
    USBD1_INTERRUPT_REQUEST_EP};
