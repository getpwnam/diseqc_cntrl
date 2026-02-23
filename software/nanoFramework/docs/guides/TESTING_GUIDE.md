# Testing Guide

## Purpose

Bring up and validate board behavior in a staged way: power, flash, protocol output, and functional control.

## Current Profile Notes

- The currently validated build profile has `System.Net` disabled.
- MQTT/network test phases in this guide are optional unless networking is re-enabled in the build profile.

## Prerequisites

### Hardware Needed
- ✅ STM32F407VGT6 DiSEqC controller board (assembled)
- ✅ USB-to-Serial adapter (for debug output) - UART2 on PA2/PA3
- ✅ ST-Link V2 (for programming)
- ✅ Ethernet cable
- ✅ Router/switch with DHCP
- ✅ MQTT broker (Mosquitto on PC or Raspberry Pi)
- ✅ Oscilloscope (for verifying DiSEqC signal)
- ✅ Multimeter
- ✅ DiSEqC rotor (for final testing)
- ✅ LNBH26 power supply (18V for LNB)

### Software Needed
- ✅ nanoFramework firmware (built from `nf-native/`)
- ✅ Mosquitto MQTT broker
- ✅ MQTT Explorer or mosquitto_sub/pub
- ✅ Serial terminal (PuTTY, Arduino IDE, etc.)

---

## 🔌 Phase 1: Power-On & Serial Debug

### Step 1.1: Connect Serial Debug

```
STM32 Board          USB-Serial Adapter
-----------          ------------------
PA2 (TX) --------→   RX
PA3 (RX) ←--------   TX
GND      ←-------→   GND
```

**Serial Settings:**
- Baud: 115200
- Data: 8 bits
- Parity: None
- Stop: 1 bit

### Step 1.2: Flash Firmware

```bash
# Using ST-Link
st-flash write nanoCLR.bin 0x08000000

# Or using OpenOCD
openocd -f interface/stlink.cfg -f target/stm32f4x.cfg \
    -c "program nanoCLR.bin 0x08000000 verify reset exit"
```

### Step 1.3: Verify Boot Sequence

Expected serial output:
```
==============================================
DiSEqC Controller Starting...
STM32F407VGT6 + W5500 + nanoFramework
==============================================

--- Network Initialization ---
Network Interface: Ethernet
MAC Address: DE:AD:BE:EF:12:34
Requesting DHCP address...
......
✓ Network Ready!
  IP Address: 192.168.1.123
  Subnet Mask: 255.255.255.0
  Gateway: 192.168.1.1
  DNS: 192.168.1.1

Initializing DiSEqC native driver...

--- MQTT Initialization ---
Broker: 192.168.1.50:1883
✓ Connected to MQTT broker!
Published availability: online

--- Subscribing to MQTT Topics ---
✓ Subscribed to 10 topics

--- Publishing Initial Status ---
✓ Initial status published

Entering main loop...
[HEARTBEAT] Uptime: 0 seconds
```

✅ **Success Criteria:**
- Serial output appears
- DHCP address acquired
- MQTT connection successful

❌ **Troubleshooting:**
| Issue | Fix |
|-------|-----|
| No serial output | Check TX/RX wiring, baud rate |
| DHCP timeout | Check Ethernet cable, W5500 connections |
| MQTT connection failed | Check broker IP, firewall |

---

## 🌐 Phase 2: Network & MQTT Testing

### Step 2.1: Verify MQTT Connection

On your PC/broker:
```bash
# Subscribe to all topics
mosquitto_sub -h localhost -t 'diseqc/#' -v

# Should see:
# diseqc/availability online
# diseqc/status/state idle
# diseqc/status/position/angle 0.0
# diseqc/status/busy false
```

### Step 2.2: Test MQTT Commands

#### Test Availability
```bash
# Device should be online
mosquitto_sub -h localhost -t 'diseqc/availability' -v
# Output: diseqc/availability online
```

#### Test Halt Command
```bash
mosquitto_pub -h localhost -t 'diseqc/command/halt' -m ''

# Check serial output:
# [MQTT] Topic: diseqc/command/halt
# [CMD] Halting rotor
```

#### Test Status Query
```bash
# Monitor status updates
mosquitto_sub -h localhost -t 'diseqc/status/#' -v

# Should show:
# diseqc/status/state idle
# diseqc/status/busy false
# diseqc/status/position/angle 0.0
```

✅ **Success Criteria:**
- MQTT messages published correctly
- Commands received and logged
- Status updates appear

---

## 🎛️ Phase 3: DiSEqC Signal Verification

### Step 3.1: Connect Oscilloscope

```
Oscilloscope Setup:
- Channel 1: TIM4_CH1 output pin (DiSEqC output)
- Channel 2: GND
- Trigger: Rising edge, 1V
- Timebase: 100µs/div
- Voltage: 2V/div
```

### Step 3.2: Send Test Command

```bash
# Send GotoAngle command
mosquitto_pub -h localhost -t 'diseqc/command/goto/angle' -m '0'
```

**Expected Serial Output:**
```
[MQTT] Topic: diseqc/command/goto/angle
[MQTT] Payload: 0
[CMD] Moving to angle: 0°
[CMD] Movement complete: 0°
```

**Expected on Oscilloscope:**
- Transmission duration: ~67ms (5 bytes)
- Carrier frequency: ~22kHz (verify with cursor)
- Bit '0': 1ms carrier + 0.5ms silence
- Bit '1': 0.5ms carrier + 1ms silence

### Step 3.3: Decode DiSEqC Bytes

For `GotoAngle(0)`, expect these bytes:
```
E0 31 6E D0 00

Breakdown:
E0 = Master command, no reply
31 = Any positioner
6E = GotoX command
D0 = Direction (East) + angle high bits
00 = Angle low bits (0° × 16 = 0x0000)
```

**Verify with oscilloscope:**
- Count 5 bytes × 9 bits = 45 bits
- Each bit ~1.5ms = ~67.5ms total
- 22kHz carrier visible during ON periods

✅ **Success Criteria:**
- DiSEqC waveform visible
- Correct frequency (~22kHz ±10%)
- Correct timing (1ms/0.5ms)
- All bytes transmitted

❌ **Troubleshooting:**
| Issue | Fix |
|-------|-----|
| No output | Check TIM4 initialization and TIM4_CH1 GPIO/AF config |
| Wrong frequency | Verify 168MHz system clock, check PSC/ARR |
| Distorted signal | Check LNBH26 connections, power supply |

---

## 🎮 Phase 4: Manual Control Testing

### Step 4.1: Test Step Commands

```bash
# Step East
mosquitto_pub -h localhost -t 'diseqc/command/manual/step_east' -m '1'

# Wait 2 seconds, then Step West
mosquitto_pub -h localhost -t 'diseqc/command/manual/step_west' -m '1'
```

**Expected Serial Output:**
```
[CMD] Step East: 1 steps
[CMD] Step West: 1 steps
```

**Oscilloscope:**
- Each command sends DiSEqC packet
- `StepEast(1)` = bytes `E0 31 68 01`
- `StepWest(1)` = bytes `E0 31 69 01`

### Step 4.2: Test Continuous Drive

```bash
# Start driving East
mosquitto_pub -h localhost -t 'diseqc/command/manual/drive_east' -m ''

# Wait 3 seconds, then halt
sleep 3
mosquitto_pub -h localhost -t 'diseqc/command/halt' -m ''
```

**Expected:**
- Drive command sends continuously
- Halt stops transmission

✅ **Success Criteria:**
- Commands execute
- Status updates correctly
- Oscilloscope shows correct bytes

---

## 🛰️ Phase 5: Rotor Integration

### Step 5.1: Connect Rotor

**IMPORTANT:** Ensure LNBH26 power supply is correct!
- 13V for vertical polarization
- 18V for horizontal polarization

```
LNBH26 Connections:
- DSQIN (from TIM4_CH1 output pin) → Rotor DiSEqC input
- LNB_OUT → LNB coax
- VCC → 18V power supply
- GND → Common ground
```

### Step 5.2: Test Movement

```bash
# Move to reference (0°)
mosquitto_pub -h localhost -t 'diseqc/command/goto/angle' -m '0'

# Wait for movement to complete
sleep 10

# Move to +20° East
mosquitto_pub -h localhost -t 'diseqc/command/goto/angle' -m '20'
```

**Expected:**
- Rotor moves (hear motor)
- LED on rotor indicates movement
- Movement stops at target

### Step 5.3: Test Satellite Positions

```bash
# Move to Astra 19.2°E
mosquitto_pub -h localhost -t 'diseqc/command/goto/satellite' -m 'astra_19.2e'

# Check status
mosquitto_sub -h localhost -t 'diseqc/status/position/satellite' -v
# Output: diseqc/status/position/satellite astra_19.2e
```

✅ **Success Criteria:**
- Rotor responds to commands
- Movement smooth
- Reaches target position
- Status updates correctly

---

## 📊 Phase 6: System Integration

### Step 6.1: Home Assistant Integration

Add to `configuration.yaml`:
```yaml
mqtt:
  select:
    - name: "Satellite Position"
      command_topic: "diseqc/command/goto/satellite"
      state_topic: "diseqc/status/position/satellite"
      options:
        - "astra_19.2e"
        - "hotbird_13e"
        - "astra_28.2e"
      availability_topic: "diseqc/availability"
```

**Test:**
1. Restart Home Assistant
2. Check Entities → `select.satellite_position`
3. Select satellite from dropdown
4. Verify rotor moves

### Step 6.2: Node-RED Flow

```json
[
    {
        "id": "mqtt_in",
        "type": "mqtt in",
        "topic": "diseqc/status/#",
        "broker": "mqtt_broker"
    },
    {
        "id": "dashboard",
        "type": "ui_text",
        "label": "Rotor Status"
    }
]
```

### Step 6.3: Python Automation

```python
import paho.mqtt.client as mqtt

def on_connect(client, userdata, flags, rc):
    client.subscribe("diseqc/status/#")

def on_message(client, userdata, msg):
    print(f"{msg.topic}: {msg.payload.decode()}")

client = mqtt.Client()
client.on_connect = on_connect
client.on_message = on_message
client.connect("192.168.1.50", 1883)

# Move to satellite
client.publish("diseqc/command/goto/satellite", "astra_19.2e")

client.loop_forever()
```

✅ **Success Criteria:**
- Home Assistant integration works
- Node-RED displays status
- Python control functions

---

## 🧪 Phase 7: Performance Testing

### Test 7.1: Response Time

```bash
# Measure command-to-execution time
time mosquitto_pub -h localhost -t 'diseqc/command/halt' -m ''
```

**Expected:** <100ms from publish to execution

### Test 7.2: Reliability

```bash
# Send 100 commands
for i in {1..100}; do
    mosquitto_pub -h localhost -t 'diseqc/command/goto/angle' -m '10'
    sleep 2
    mosquitto_pub -h localhost -t 'diseqc/command/goto/angle' -m '0'
    sleep 2
done
```

**Expected:** 0% packet loss, all commands execute

### Test 7.3: Network Stability

```bash
# Disconnect/reconnect Ethernet
# Controller should auto-reconnect within 5 seconds
```

**Monitor serial output:**
```
[MQTT] Connection lost! Attempting to reconnect...
[MQTT] Disconnected. Reconnecting...
✓ Connected to MQTT broker!
```

---

## 📝 Acceptance Checklist

### Hardware
- [  ] Board powers on without smoke
- [  ] Serial debug output works
- [  ] W5500 Ethernet link LED on
- [  ] DHCP address acquired
- [  ] TIM4_CH1 output pin produces 22kHz DiSEqC signal
- [  ] LNBH26 provides LNB power

### Software
- [  ] Firmware flashes successfully
- [  ] MQTT connection establishes
- [  ] All command topics work
- [  ] Status updates publish correctly
- [  ] Availability (LWT) works

### DiSEqC
- [  ] GotoAngle commands work
- [  ] GotoSatellite commands work
- [  ] Halt command works
- [  ] StepEast/West work
- [  ] DriveEast/West work
- [  ] Signal visible on oscilloscope
- [  ] Rotor responds to commands

### Integration
- [  ] Home Assistant integration works
- [  ] MQTT Explorer shows all topics
- [  ] Python control works
- [  ] Network disconnection handled

---

## 🚨 Common Issues & Solutions

### Issue: "Network not ready"
**Solution:**
- Check Ethernet cable
- Verify W5500 SPI connections (PA4-PA7, PC4-PC5)
- Test with static IP instead of DHCP

### Issue: "MQTT connection failed"
**Solution:**
- Ping MQTT broker from PC
- Check broker IP in code
- Verify firewall rules
- Try mosquitto_pub from PC first

### Issue: "No DiSEqC output"
**Solution:**
- Verify the DiSEqC output pin is configured for TIM4_CH1 alternate function
- Check native driver initialized
- Use oscilloscope to verify signal
- Check LNBH26 power supply

### Issue: "Rotor doesn't move"
**Solution:**
- Verify LNBH26 connections
- Check LNB power (13V/18V)
- Test DiSEqC signal with oscilloscope
- Check rotor power supply

---

## 📊 Expected Performance

| Metric | Value |
|--------|-------|
| Boot time | <5 seconds |
| DHCP acquisition | 5-10 seconds |
| MQTT connection | <1 second |
| Command response | <100ms |
| DiSEqC transmission | 67ms (5 bytes) |
| Network reconnect | <5 seconds |
| CPU usage | <10% |
| Memory usage | ~15KB RAM |

---

**When all tests pass, your DiSEqC controller is ready for production use!** 🎉

Next steps:
1. Add configuration management
2. Implement satellite database
3. Build web interface
4. Deploy to permanent installation

