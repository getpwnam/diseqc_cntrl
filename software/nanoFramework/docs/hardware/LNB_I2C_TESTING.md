# LNBH26PQR I2C Testing Guide

## Purpose

Validate LNBH26PQR control paths and expected behavior over I2C.

## Scope

This guide focuses on local driver/hardware validation. MQTT steps are optional if networking is disabled in the active build profile.

## Implementation Note

The LNBH26PQR is controlled over I2C (not GPIO).

---

## I2C Configuration

```
I2C Bus: I2C1
SCL: PB8 (I2C1_SCL) - Alternate Function AF4
SDA: PB9 (I2C1_SDA) - Alternate Function AF4
Address: 0x08 (7-bit)
Speed: 100kHz (standard mode)
```

---

## 📋 LNBH26PQR Register Map

### Control Register (0x00)
```
Bit 0: EN     - Enable LNB power (1=ON, 0=OFF)
Bit 1: VSEL   - Voltage select (0=13V, 1=18V)
Bit 2: TONE   - 22kHz tone (0=OFF, 1=ON)
Bit 3: DISEQC - DiSEqC mode (1=enabled)
Bit 4: ILIM   - Current limit (0=600mA, 1=400mA)
```

**Default value:** `0x0B` (EN=1, VSEL=0, TONE=0, DISEQC=1, ILIM=600mA)
- LNB enabled
- 13V (vertical polarization)
- No 22kHz tone (low band)
- DiSEqC mode enabled
- 600mA current limit

### Status Register (0x01)
```
Bit 0: OCP  - Overcurrent protection (1=triggered)
Bit 1: OTP  - Over-temperature protection (1=triggered)
Bit 2: VMON - Voltage monitor status
```

---

## 🧪 Testing Procedure

### Test 1: I2C Communication

**Verify I2C is working:**
```cpp
// In your native code (diseqc_native.cpp or board_diseqc.cpp)
#include "lnb_control.h"

void test_lnb_i2c() {
    lnb_handle_t lnb;
    
    // Initialize I2C and LNB
    i2cStart(&I2CD1, &i2c_cfg);
    lnb_init(&lnb, &I2CD1, 0x08);
    
    // Read status register
    uint8_t status;
    lnb_status_t result = lnb_read_status(&lnb, &status);
    
    if (result == LNB_OK) {
        chprintf("LNB Status: 0x%02X\r\n", status);
    } else {
        chprintf("LNB I2C Error!\r\n");
    }
}
```

**Expected:** Status register read successfully (no I2C errors)

### Test 2: Voltage Control (13V/18V)

**MQTT commands:**
```bash
# Set 13V (Vertical)
mosquitto_pub -t diseqc/command/lnb/voltage -m "13"

# Read back (should show 13)
mosquitto_sub -t diseqc/status/lnb/voltage -C 1

# Set 18V (Horizontal)
mosquitto_pub -t diseqc/command/lnb/voltage -m "18"

# Read back (should show 18)
mosquitto_sub -t diseqc/status/lnb/voltage -C 1
```

**Verify with multimeter:**
- Measure voltage at LNB output connector
- Should read ~13V or ~18V depending on setting

### Test 3: 22kHz Tone

**MQTT commands:**
```bash
# Enable tone (high band)
mosquitto_pub -t diseqc/command/lnb/tone -m "on"

# Check status
mosquitto_sub -t diseqc/status/lnb/tone -C 1
# Should show: on

# Disable tone (low band)
mosquitto_pub -t diseqc/command/lnb/tone -m "off"
```

**Verify with spectrum analyzer or satellite finder:**
- 22kHz tone should appear/disappear on LNB output
- LNBH26 generates tone internally (no PWM needed)

### Test 4: Read Status Register

**Add to your C# code:**
```csharp
// TODO: Add LNB.ReadStatus() to C# API if needed
// For now, check logs in native code
```

**Native code (lnb_control.cpp):**
```cpp
uint8_t status;
lnb_read_status(&g_lnb, &status);

if (status & LNBH26_STAT_OCP) {
    chprintf("WARNING: Overcurrent detected!\r\n");
}
if (status & LNBH26_STAT_OTP) {
    chprintf("WARNING: Over-temperature!\r\n");
}
```

---

## 🔍 Debugging I2C Issues

### Issue: "LNB I2C Error"

**Possible causes:**
1. **Wrong I2C address** - Verify LNBH26 ADDR pin state
   - ADDR tied to GND → Address 0x08
   - ADDR tied to VCC → Address 0x09
   
2. **I2C not initialized** - Check mcuconf.h:
   ```c
   #define STM32_I2C_USE_I2C1  TRUE
   ```

3. **Wrong pins** - Verify PB8/PB9 are I2C1:
   ```c
   // board_diseqc.h should have:
   #define VAL_GPIOB_AFRH  (PIN_AFIO_AF(8, 4U) | PIN_AFIO_AF(9, 4U))
   ```

4. **Pull-up resistors** - I2C needs pull-ups (usually on board)
   - Check schematic for 4.7kΩ resistors on SCL/SDA

5. **Bus conflict** - Check if anything else uses I2C1

### Issue: Voltage doesn't change

**Debug steps:**
1. Check I2C write succeeds (no error code)
2. Read back control register to verify
3. Measure voltage at LNBH26 output
4. Check LNBH26 power supply (VCC)

### Issue: Tone doesn't work

**Debug steps:**
1. Verify TONE bit is set in control register
2. Check LNBH26 has proper power
3. Verify with spectrum analyzer
4. Some LNBs don't respond to 22kHz - test with known-good LNB

---

## 🎯 Complete Test Sequence

```bash
# 1. Boot and check I2C communication
# Serial output should show: "LNB initialized"

# 2. Test vertical polarization, low band (BBC channels on Astra 28.2E)
mosquitto_pub -t diseqc/command/lnb/polarization -m "vertical"
mosquitto_pub -t diseqc/command/lnb/band -m "low"
mosquitto_pub -t diseqc/command/goto/angle -m "28.2"
# Measure: ~13V, no 22kHz

# 3. Test horizontal polarization, high band
mosquitto_pub -t diseqc/command/lnb/polarization -m "horizontal"
mosquitto_pub -t diseqc/command/lnb/band -m "high"
# Measure: ~18V, 22kHz present

# 4. Test all 4 combinations
for pol in vertical horizontal; do
    for band in low high; do
        mosquitto_pub -t diseqc/command/lnb/polarization -m "$pol"
        mosquitto_pub -t diseqc/command/lnb/band -m "$band"
        echo "Testing: $pol, $band"
        sleep 2
        # Check signal quality here
    done
done
```

---

## 📊 Expected I2C Traffic

**Initialization:**
```
Master → Slave [0x08]:  0x00 0x0B   (Write control reg: EN=1, VSEL=0, TONE=0, DISEQC=1)
```

**Set 18V:**
```
Master → Slave [0x08]:  0x00 0x0B   (Read current value)
Master → Slave [0x08]:  0x00 0x0D   (Write: EN=1, VSEL=1, TONE=0, DISEQC=1)
```

**Enable 22kHz:**
```
Master → Slave [0x08]:  0x00 0x0F   (Write: EN=1, VSEL=1, TONE=1, DISEQC=1)
```

---

## ✅ Success Criteria

- [ ] I2C communication works (no errors)
- [ ] Control register can be written
- [ ] Status register can be read
- [ ] Voltage switches between 13V and 18V (verified with multimeter)
- [ ] 22kHz tone can be enabled/disabled (verified with analyzer)
- [ ] MQTT commands control LNB correctly
- [ ] No overcurrent/over-temperature errors in status

---

**Your LNBH26PQR is now fully integrated via I2C!** 🎉

All LNB control (voltage, tone, status) happens via I2C - no GPIO or PWM needed for LNB functions.

