# Motor Enable Notes

## Purpose

Clarify why external motor-enable control is not used for the current board design.

## Current Decision

Motor-enable GPIO control is not used for this hardware profile.

### Why

1. **PB1 is NC (Not Connected)** in your schematic
2. **LNBH26 handles power automatically** - it provides 13V/18V to the LNB based on the VSEL pin
3. **DiSEqC commands control the rotor directly** - no separate enable signal needed

### What This Means

The original C# code had `MotorEnablerManager` that controlled a separate motor enable pin. This was likely from an ESP32 design where motor power was switched separately.

**Your hardware doesn't need this** because:
- LNBH26 always provides power when board is on
- DiSEqC GotoX commands tell the rotor when to move
- The rotor itself decides when to enable its motor based on DiSEqC commands

### Changes Made

1. ✅ **Removed from `board_diseqc.h`**:
   - `#define MOTOR_ENABLE_LINE` removed
   - GPIOB configuration cleaned up (all default)

2. ⚠️ **Native driver still has motor enable code** (in `diseqc_native.h/cpp`):
   - This code won't be used
   - Can be safely ignored
   - Functions like `motor_enable_init()`, `motor_enable_turn_on()` exist but **don't call them**

3. ✅ **C# wrapper should NOT use**:
   - Don't call `MotorEnable.TurnOn()`
   - Don't use `MotorEnable.StartTracking()`
   - Just send DiSEqC commands directly

### Simplified Usage

**OLD way (with motor enable):**
```csharp
MotorEnable.TurnOn(5);  // ❌ Don't do this - no motor enable pin!
DiSEqC.GotoAngle(45.0f);
```

**NEW way (direct):**
```csharp
DiSEqC.GotoAngle(45.0f);  // ✅ Just send the command
```

The rotor will:
1. Receive the DiSEqC command
2. Parse the target angle
3. Enable its own motor
4. Move to position
5. Disable its motor when done

**You don't control the motor - the rotor does!**

### If You Want Motor Control Later

If you decide to add external motor control (like a relay), you could:
1. Add a GPIO pin in your schematic
2. Update `board_diseqc.h` with the new pin
3. Use the existing motor enable code

But for now, **you don't need it** - your design is simpler!

### Board Initialization

```cpp
// In board_diseqc.cpp - boardInit()
void boardInit(void) {
    // Initialize DiSEqC native driver
    diseqc_init(&PWMD1, &GPTD2);
    
    // NO motor enable init - not needed!
    // motor_enable_init(MOTOR_ENABLE_LINE);  ← Don't call this
}
```

### C# Usage

```csharp
using DiSEqC_Control.Native;

// Just use DiSEqC directly
DiSEqC.GotoAngle(45.0f);

// No need for:
// MotorEnable.TurnOn(5);  ❌
// MotorEnable.StartTracking();  ❌
```

---

**Summary**: Your board is simpler - DiSEqC commands go straight to the rotor, which controls its own motor. No external motor enable needed! 🎯

