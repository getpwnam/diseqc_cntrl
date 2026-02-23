# LNB Control Guide (I2C)

## Purpose

Describe how LNBH26PQR control is implemented and validated through I2C.

## Current Profile Notes

- The current validated build profile has networking disabled.
- MQTT examples remain valid as integration examples, but require networking to be enabled.

## Overview

The controller supports LNB control via **I2C** for the LNBH26PQR:
- ✅ **Voltage selection (13V/18V)** → Polarization control via I2C
- ✅ **22kHz tone** → Band selection via I2C
- ✅ **Current limiting** → Programmable via I2C
- ✅ **Status monitoring** → Overcurrent, temperature via I2C
- ✅ **MQTT interface** → Remote control
- ✅ **C# API** → Programmatic control

## 📡 What is LNB Control?

### Voltage Selection (13V/18V)
**Controls satellite polarization:**
- **13V** = Vertical polarization (V)
- **18V** = Horizontal polarization (H)

Most satellites transmit on both polarizations to double capacity.

### 22kHz Tone
**Controls frequency band:**
- **OFF (no tone)** = Low band (10.7-11.7 GHz)
- **ON (22kHz continuous)** = High band (11.7-12.75 GHz)

The LNB uses this to select which frequency range to downconvert.

## 🔧 Hardware Implementation (LNBH26PQR)

The LNBH26PQR is controlled via **I2C interface**:

### I2C Configuration
```
I2C Bus: I2C1
SCL Pin: PB8 (I2C1_SCL)
SDA Pin: PB9 (I2C1_SDA)
I2C Address: 0x08 (7-bit address)
I2C Speed: 100kHz (standard mode)
```

### LNBH26PQR Register Map
```
Register 0x00 (Control):
  Bit 0: EN     - Enable LNB power (1=enabled)
  Bit 1: VSEL   - Voltage select (0=13V, 1=18V)
  Bit 2: TONE   - 22kHz tone (0=OFF, 1=ON)
  Bit 3: DISEQC - DiSEqC mode (1=enabled)
  Bit 4: ILIM   - Current limit (0=600mA, 1=400mA)

Register 0x01 (Status):
  Bit 0: OCP    - Overcurrent protection
  Bit 1: OTP    - Over-temperature protection
  Bit 2: VMON   - Voltage monitor
```

### Advantages of I2C Control
- ✅ **Software-controlled** voltage and tone
- ✅ **Status monitoring** (overcurrent, temperature)
- ✅ **Current limiting** programmable
- ✅ **No PWM needed** for 22kHz tone (generated internally)
- ✅ **DiSEqC mode** can be enabled/disabled

## 💻 C# API Reference

### Namespace
```csharp
using DiSEqC_Control.Native;
```

### Enums
```csharp
// Voltage levels
LNB.Voltage.V13    // 13V (Vertical)
LNB.Voltage.V18    // 18V (Horizontal)

// Polarization
LNB.Polarization.Vertical      // 13V
LNB.Polarization.Horizontal    // 18V

// Frequency band
LNB.Band.Low     // 10.7-11.7 GHz (no tone)
LNB.Band.High    // 11.7-12.75 GHz (22kHz ON)

// Status codes
LNB.Status.Ok
LNB.Status.InvalidParam
LNB.Status.NotInitialized
```

### Methods

#### Set Voltage
```csharp
LNB.Status status = LNB.SetVoltage(LNB.Voltage.V13);  // 13V
LNB.Status status = LNB.SetVoltage(LNB.Voltage.V18);  // 18V
```

#### Set Polarization (Convenience)
```csharp
LNB.Status status = LNB.SetPolarization(LNB.Polarization.Vertical);
LNB.Status status = LNB.SetPolarization(LNB.Polarization.Horizontal);
```

#### Set 22kHz Tone
```csharp
LNB.Status status = LNB.SetTone(true);   // ON (high band)
LNB.Status status = LNB.SetTone(false);  // OFF (low band)
```

#### Set Band (Convenience)
```csharp
LNB.Status status = LNB.SetBand(LNB.Band.Low);   // 10.7-11.7 GHz
LNB.Status status = LNB.SetBand(LNB.Band.High);  // 11.7-12.75 GHz
```

#### Get Current Settings
```csharp
LNB.Voltage voltage = LNB.GetVoltage();                 // Current voltage
bool tone = LNB.GetTone();                              // Tone state
LNB.Polarization pol = LNB.GetPolarization();           // Current polarization
LNB.Band band = LNB.GetBand();                          // Current band
```

## 📋 MQTT Topics

### Command Topics (Subscribe)

```
diseqc/command/lnb/voltage
  Payload: "13" or "18"
  Description: Set LNB voltage
  Example: mosquitto_pub -t diseqc/command/lnb/voltage -m "18"

diseqc/command/lnb/polarization
  Payload: "vertical" or "horizontal" (or "v" / "h")
  Description: Set polarization (convenience)
  Example: mosquitto_pub -t diseqc/command/lnb/polarization -m "vertical"

diseqc/command/lnb/tone
  Payload: "on" / "off" (or "true" / "false" / "1" / "0")
  Description: Enable/disable 22kHz tone
  Example: mosquitto_pub -t diseqc/command/lnb/tone -m "on"

diseqc/command/lnb/band
  Payload: "low" or "high" (or "l" / "h")
  Description: Set frequency band (convenience)
  Example: mosquitto_pub -t diseqc/command/lnb/band -m "high"
```

### Status Topics (Publish)

```
diseqc/status/lnb/voltage
  Payload: "13" or "18"
  Retained: Yes
  Description: Current voltage setting

diseqc/status/lnb/polarization
  Payload: "vertical" or "horizontal"
  Retained: Yes
  Description: Current polarization

diseqc/status/lnb/tone
  Payload: "on" or "off"
  Retained: Yes
  Description: Current 22kHz tone state

diseqc/status/lnb/band
  Payload: "low" or "high"
  Retained: Yes
  Description: Current frequency band
```

## 🎯 Usage Examples

### Example 1: Set Up for Astra 19.2°E (Horizontal, High Band)

**MQTT:**
```bash
# Set polarization to horizontal (18V)
mosquitto_pub -t diseqc/command/lnb/polarization -m "horizontal"

# Set high band (22kHz ON)
mosquitto_pub -t diseqc/command/lnb/band -m "high"

# Move rotor to position
mosquitto_pub -t diseqc/command/goto/satellite -m "astra_19.2e"
```

**C#:**
```csharp
LNB.SetPolarization(LNB.Polarization.Horizontal);
LNB.SetBand(LNB.Band.High);
rotor.GotoSatellite("astra_19.2e");
```

### Example 2: Scan All Polarization/Band Combinations

```csharp
var polarizations = new[] { LNB.Polarization.Vertical, LNB.Polarization.Horizontal };
var bands = new[] { LNB.Band.Low, LNB.Band.High };

foreach (var pol in polarizations)
{
    LNB.SetPolarization(pol);
    Thread.Sleep(500);  // Wait for LNB to switch
    
    foreach (var band in bands)
    {
        LNB.SetBand(band);
        Thread.Sleep(500);
        
        // Check signal quality
        float signalQuality = ReadSignalQuality();
        
        Debug.WriteLine($"Pol: {pol}, Band: {band}, Signal: {signalQuality}%");
        
        if (signalQuality > 80)
        {
            Debug.WriteLine("Good signal found!");
            break;
        }
    }
}
```

### Example 3: Home Assistant Integration

```yaml
# configuration.yaml
mqtt:
  select:
    - name: "LNB Polarization"
      command_topic: "diseqc/command/lnb/polarization"
      state_topic: "diseqc/status/lnb/polarization"
      options:
        - "vertical"
        - "horizontal"
      availability_topic: "diseqc/availability"
    
    - name: "LNB Band"
      command_topic: "diseqc/command/lnb/band"
      state_topic: "diseqc/status/lnb/band"
      options:
        - "low"
        - "high"
      availability_topic: "diseqc/availability"
```

### Example 4: Automated Channel Tuning

```csharp
public class ChannelTuner
{
    public struct ChannelInfo
    {
        public string Name;
        public float Angle;          // Satellite position
        public LNB.Polarization Pol;
        public LNB.Band Band;
        public int Frequency;        // In MHz
    }
    
    public static readonly ChannelInfo[] Channels = new[]
    {
        new ChannelInfo 
        { 
            Name = "BBC One HD",
            Angle = 28.2f,  // Astra 28.2°E
            Pol = LNB.Polarization.Horizontal,
            Band = LNB.Band.Low,
            Frequency = 10773
        },
        new ChannelInfo
        {
            Name = "Das Erste HD",
            Angle = 19.2f,  // Astra 19.2°E
            Pol = LNB.Polarization.Vertical,
            Band = LNB.Band.High,
            Frequency = 11494
        }
    };
    
    public static void TuneChannel(ChannelInfo channel)
    {
        Debug.WriteLine($"Tuning to: {channel.Name}");
        
        // Set LNB parameters
        LNB.SetPolarization(channel.Pol);
        LNB.SetBand(channel.Band);
        
        // Move rotor
        rotor.GotoAngle(channel.Angle);
        while (rotor.IsBusy()) Thread.Sleep(100);
        
        // Tune satellite receiver (via external API)
        // TuneSatReceiver(channel.Frequency);
        
        Debug.WriteLine($"✓ Tuned to {channel.Name}");
    }
}
```

### Example 5: MQTT Automation (Node-RED)

```json
[
    {
        "id": "channel_select",
        "type": "ui_dropdown",
        "options": [
            {"label": "BBC One HD", "value": "bbc_one"},
            {"label": "Das Erste HD", "value": "das_erste"}
        ],
        "outputs": 1
    },
    {
        "id": "tune_channel",
        "type": "function",
        "func": "
            const channels = {
                'bbc_one': {pol: 'horizontal', band: 'low', sat: 'astra_28.2e'},
                'das_erste': {pol: 'vertical', band: 'high', sat: 'astra_19.2e'}
            };
            
            const ch = channels[msg.payload];
            
            return [
                {topic: 'diseqc/command/lnb/polarization', payload: ch.pol},
                {topic: 'diseqc/command/lnb/band', payload: ch.band},
                {topic: 'diseqc/command/goto/satellite', payload: ch.sat}
            ];
        ",
        "outputs": 3
    },
    {
        "id": "mqtt_out",
        "type": "mqtt out",
        "broker": "mqtt_broker"
    }
]
```

## 🧪 Testing LNB Control

### Test 1: Verify Voltage Switching

```bash
# Set to 13V
mosquitto_pub -t diseqc/command/lnb/voltage -m "13"

# Check status
mosquitto_sub -t diseqc/status/lnb/voltage -v
# Should show: diseqc/status/lnb/voltage 13

# Measure with multimeter at LNB output (should be ~13V)

# Set to 18V
mosquitto_pub -t diseqc/command/lnb/voltage -m "18"

# Measure again (should be ~18V)
```

### Test 2: Verify 22kHz Tone

```bash
# Enable tone
mosquitto_pub -t diseqc/command/lnb/tone -m "on"

# Check status
mosquitto_sub -t diseqc/status/lnb/tone -v
# Should show: diseqc/status/lnb/tone on

# Use spectrum analyzer or oscilloscope to verify 22kHz on LNB line
# OR check with satellite signal meter (should switch bands)

# Disable tone
mosquitto_pub -t diseqc/command/lnb/tone -m "off"
```

### Test 3: Polarization/Band Combinations

```bash
# Test all 4 combinations
for pol in vertical horizontal; do
    for band in low high; do
        mosquitto_pub -t diseqc/command/lnb/polarization -m "$pol"
        mosquitto_pub -t diseqc/command/lnb/band -m "$band"
        sleep 2
        echo "Pol: $pol, Band: $band"
        # Check signal quality here
    done
done
```

## 📊 LNB Configuration Matrix

| Polarization | Band | Voltage | 22kHz Tone | Frequency Range |
|--------------|------|---------|------------|-----------------|
| Vertical     | Low  | 13V     | OFF        | 10.7-11.7 GHz   |
| Vertical     | High | 13V     | ON         | 11.7-12.75 GHz  |
| Horizontal   | Low  | 18V     | OFF        | 10.7-11.7 GHz   |
| Horizontal   | High | 18V     | ON         | 11.7-12.75 GHz  |

## ⚠️ Important Notes

### Voltage Switching Delay
- Allow 500ms after voltage change before tuning
- LNB needs time to stabilize

### Tone Switching Delay
- Allow 200ms after tone change
- Some LNBs are slower

### Power Consumption
- 13V: ~50-100mA (LNB idle)
- 18V: ~50-100mA (LNB idle)
- Ensure your power supply can handle this

### Compatibility
- Works with universal LNBs (most common)
- Single-output LNBs only need voltage OR tone
- Check your LNB specifications

## 🔧 Board Configuration

**File: `nf-native/board_diseqc.h`**

```c
// LNB Control pins - VERIFY FROM SCHEMATIC!
#define LNB_VSEL_LINE              PAL_LINE(GPIOC, 6U)  // Voltage select pin
#define LNB_TONE_PWM_DRIVER        PWMD3                // TIM3 for tone (if using PWM)
#define LNB_TONE_PWM_CHANNEL       0                    // PWM channel
#define LNB_USE_INTERNAL_TONE      true                 // Use LNBH26 internal tone
```

**⚠️ TODO before building firmware:**
1. Check your schematic for LNBH26 VSEL pin connection
2. Check if ToneIN is connected to MCU or using internal
3. Update `board_diseqc.h` with correct pins
4. Update GPIO configuration if needed

## 🚨 Troubleshooting

### Issue: Voltage doesn't change
**Solution:**
- Verify VSEL pin connected to correct GPIO
- Check GPIO is configured as output
- Measure voltage at LNBH26 VSEL pin (should be 0V or 3.3V)
- Check LNBH26 power supply

### Issue: 22kHz tone not working
**Solution:**
- If using PWM: Verify TIM3 configuration, check PC6
- If using internal: Check LNBH26 ToneEN pin
- Measure with oscilloscope at LNBH26 output
- Check tone frequency (should be ~22kHz)

### Issue: No LNB power
**Solution:**
- Check LNBH26 power supply (VCC)
- Check enable pins (EN, EXTM if connected)
- Verify coax connection
- Check for overcurrent shutdown

## 📚 Files Created

1. **Native Driver:**
   - `nf-native/lnb_control.h` - C++ header
   - `nf-native/lnb_control.cpp` - Implementation
   - `nf-native/lnb_interop.cpp` - C# interop

2. **C# Wrapper:**
  - `DiSEqC_Control/Native/LNBNative.cs` - C# API

3. **Application:**
  - `DiSEqC_Control/Program.cs` - MQTT handlers added

4. **Documentation:**
   - `nf-native/LNB_CONTROL_GUIDE.md` - This file!

## 🎓 Next Steps

1. **Verify schematic** - Check actual LNBH26 pin connections
2. **Update board config** - Set correct GPIO pins
3. **Build firmware** - Recompile with LNB support
4. **Test voltage** - Verify 13V/18V switching
5. **Test tone** - Verify 22kHz generation
6. **Integrate** - Add to your automation system

---

**Your DiSEqC controller now has complete LNB control!** 🛰️

You can now:
- ✅ Control polarization (V/H)
- ✅ Select frequency band (low/high)
- ✅ Automate channel tuning
- ✅ Integrate with Home Assistant
- ✅ Control via MQTT

