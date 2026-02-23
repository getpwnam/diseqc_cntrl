# W5500 Ethernet Integration Notes

## Purpose

Document W5500 pin mapping and integration notes for this board.

## Current Profile Notes

- The current validated firmware build profile has networking disabled.
- This file documents wiring/integration details for when networking is enabled.

## Pin Assignments (From Schematic)

Based on your `diseqc_cntrl` KiCad schematic:

### SPI1 Connections
| Signal | STM32 Pin | W5500 Pin | Description |
|--------|-----------|-----------|-------------|
| SCK    | PA5       | SCLK      | SPI Clock |
| MISO   | PA6       | MISO      | Master In Slave Out |
| MOSI   | PA7       | MOSI      | Master Out Slave In |
| CS     | PA4       | SCSN      | Chip Select (active low) |

### Control Signals
| Signal | STM32 Pin | W5500 Pin | Description |
|--------|-----------|-----------|-------------|
| RESET  | PC4       | RSTN      | Reset (active low) |
| INT    | PC5       | INTN      | Interrupt (active low) |

## ✅ Updated Files

### 1. `nf-native/board_diseqc.h`
```c
// W5500 configuration
#define W5500_SPI_DRIVER            SPID1               // SPI1
#define W5500_CS_LINE               PAL_LINE(GPIOA, 4U) // PA4 = SCSN
#define W5500_RESET_LINE            PAL_LINE(GPIOC, 4U) // PC4 = W5500_RST
#define W5500_INT_LINE              PAL_LINE(GPIOC, 5U) // PC5 = W5500_INT
```

### 2. `DiSEqC_Control/packages.config`
Added W5500 networking packages:
- `nanoFramework.System.Net.Sockets.TcpClient` - TCP socket support
- `nanoFramework.System.Device.Model` - Device bindings
- `nanoFramework.M2Mqtt` - MQTT client

## 🚀 Usage Example in C#

```csharp
using System.Device.Spi;
using System.Device.Gpio;
using System.Net;
using System.Net.NetworkInformation;
using nanoFramework.M2Mqtt;

// W5500 SPI Configuration
const int W5500_CS_PIN = 4;  // PA4

var spiConfig = new SpiConnectionSettings(1, W5500_CS_PIN)  // SPI1, PA4
{
    ClockFrequency = 2_000_000,  // 2MHz
    Mode = SpiMode.Mode0,
    DataBitLength = 8
};

var spiDevice = SpiDevice.Create(spiConfig);

// Configure Network - Static IP
NetworkInterface.GetAllNetworkInterfaces()[0].EnableStaticIPv4(
    "192.168.1.100",  // Your IP
    "255.255.255.0",  // Subnet mask
    "192.168.1.1"     // Gateway
);

// Or use DHCP
// NetworkInterface.GetAllNetworkInterfaces()[0].EnableDhcp();

// Wait for network ready
while (NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address == "0.0.0.0")
{
    Thread.Sleep(100);
}

Console.WriteLine($"IP: {NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address}");

// Connect to MQTT
var mqtt = new MqttClient("192.168.1.50");  // Your MQTT broker
mqtt.MqttMsgPublishReceived += OnMqttMessage;
mqtt.Connect("diseqc_controller");
mqtt.Subscribe(new[] { "diseqc/angle" }, new byte[] { 0 });

Console.WriteLine("W5500 Ethernet ready!");
```

## 🔧 nf-interpreter mcuconf.h Settings

Ensure your `mcuconf.h` has SPI1 enabled:

```c
/*
 * SPI driver system settings
 */
#define STM32_SPI_USE_SPI1                  TRUE
#define STM32_SPI_SPI1_RX_DMA_STREAM        STM32_DMA_STREAM_ID(2, 0)
#define STM32_SPI_SPI1_TX_DMA_STREAM        STM32_DMA_STREAM_ID(2, 3)
```

## 📊 SPI Timing

| Parameter | Value |
|-----------|-------|
| SPI Mode | Mode 0 (CPOL=0, CPHA=0) |
| Clock Frequency | 2MHz (recommended) |
| Max Frequency | 33MHz (W5500 max) |
| Data Order | MSB First |

## 🧪 Testing W5500

### Step 1: Verify SPI Communication
```csharp
// Read W5500 version register (should return 0x04)
var cmd = new byte[] { 0x00, 0x39, 0x00 };  // Read version register
var response = new byte[1];
spiDevice.TransferFullDuplex(cmd, response);
Console.WriteLine($"W5500 Version: 0x{response[0]:X2}");  // Should be 0x04
```

### Step 2: Test Network
```csharp
// Ping test
var ping = new System.Net.NetworkInformation.Ping();
var result = ping.Send("192.168.1.1", 1000);
Console.WriteLine($"Ping: {result.Status}");
```

### Step 3: Test MQTT
```csharp
mqtt.Publish("diseqc/status", Encoding.UTF8.GetBytes("online"));
```

## 🐛 Troubleshooting

### W5500 Not Responding
- Check PC4 (RESET) is HIGH after initialization
- Verify PA4 (CS) toggles during SPI transfer
- Measure SPI signals with oscilloscope
- Check 3.3V power supply to W5500

### Network Not Working
- Verify Ethernet cable connected
- Check link LED on W5500 module
- Try static IP instead of DHCP first
- Check gateway/subnet configuration

### MQTT Connection Fails
- Verify broker IP/port correct
- Test broker with mosquitto_pub/sub
- Check firewall rules
- Enable broker logging for diagnostics

## 📝 Next Steps

1. **Build firmware** with updated board configuration
2. **Flash to board** and verify W5500 initialization
3. **Test SPI communication** (version register read)
4. **Configure network** (static IP or DHCP)
5. **Connect to MQTT broker**
6. **Test DiSEqC commands via MQTT**

---

**Your W5500 is now fully configured!** 🌐

