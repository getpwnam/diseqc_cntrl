# Single-Channel Scope Diagnosis Plan for W5500 SPI2 Bringup

## Current State
- Firmware deployed with scope-assist GPIO pulse block (PB13/PB15 driven as GPIO ~24 ms on/off for ~500 ms post-reset)
- CS (PB12) confirmed toggling on scope during SPI transactions (proves hardware path works)
- VERSIONR read returns 0x00 for all bytes (MISO data path issue suspected)
- **Main hypothesis**: SPI2 peripheral may not be enabled, or AF5 pin mux not applied at runtime

## Single-Channel Capture Sequence

### Phase 1: Verify GPIO Pulse Block is Running (Confirms Pin Path + AF5 Mux)
**Channel**: PB13 (SCK)
**Settings**:
- DC coupling ✓
- Normal trigger (not auto)
- Timebase: 100 ms/div
- Trigger level: 1.5V (midpoint between GND and 3.3V)
- Peak-detect: ON (captures all edges despite 24 ms pulse width)
- Persistence: ON (shows all captured edges)

**Expected**: 
- Should see ~20 square-wave pulses (24 ms on, 24 ms off)
- After ~500 ms, pulses stop and PB13 goes high (AF5 mode, idle state)

**Result interpretation**:
- ✅ Pulses visible → GPIO control + pin path confirmed; issue is likely SPI peripheral enable or clocking
- ❌ No pulses → Hardware problem (pin stuck, wrong net, open); not a config issue

---

### Phase 2: Check for SCK During VERSIONR Read (Verify Peripheral Running)
**Only if Phase 1 passes**

**First**, capture just the pulse block as baseline (same settings) - note exact timing.

**Then**, switch channel to probe SCK **during** the VERSIONR read sequence:
- Timebase: 10 µs/div (to see SPI bit timing ~5–10 MHz or ~164 kHz depending on mode)
- Trigger: rising edge on SCK (if visible)
- Persistence: ON with peak-detect

**Expected**:
- After GPIO pulses and reset, during CS low window, should see **clock train** on SCK
- Each SPI byte: 8 pulses (~8–10 µs/pulse for ~1 MHz, or ~40–60 µs/byte at ~164 kHz)

**Result interpretation**:
- ✅ SCK clock train visible during CS low → SPI2 peripheral confirmed running; issue is likely MISO or data handling
- ❌ SCK silent during CS low → SPI2 not clocking; check CR1 peripheral enable, clock source, PCLK config

---

### Phase 3: Probe MOSI / Verify Frame Structure
**Only if Phase 2 sees SCK**

Repeat Phase 2 but on PB15 (MOSI) to confirm command frame is sent:
- Timebase: 10 µs/div
- Trigger: on CS falling edge
- Expected: byte-aligned data train matching VERSIONR read command frame

---

## Key Timings
- Scope-assist GPIO block: ~500 ms total (active right after reset)
- W5500 reset hold: 20 ms
- W5500 POR wait: 50 ms total startup time
- First VERSIONR probe (after boot): within first 50–100 ms of reset release

## Persistence/Capture Tips
- **Peak-detect**: Captures all rising/falling edges even if pulse width < 1 timebase div
- **Persistence**: Accumulates all edges over many sweeps; helps see repetitive patterns
- **Single trigger mode**: Captures one clean pulse sequence; easier to verify exact timing
- **AC coupling trap**: Missed ~80% of SPI signals earlier; must use **DC coupling**

## Decision Tree
```
┌─ Phase 1: GPIO pulses visible?
│  │
│  ├─ YES → Pin path + AF5 work; proceed to Phase 2
│  │        (Check SPI2 enable, clock config, data path)
│  │
│  └─ NO  → Hardware problem (pin/net), not config
│           (Check continuity, solder, wrong net)
│
├─ Phase 2: SCK clock train visible during CS low?
│  │
│  ├─ YES → SPI2 peripheral running; proceed to Phase 3
│  │        (Check MISO, data reception, bit order)
│  │
│  └─ NO  → SPI2 stalled/not started
│           (Verify CR1 peripheral enable, clock source, RCC config)
│
└─ Phase 3: MOSI frame matches expected VERSIONR command?
   │
   ├─ YES → Full frame path working; issue is MISO reception
   │        (Check MISO pin pull, FIFO, DMA, endianness)
   │
   └─ NO  → SPI2 command not being sent
            (Check GPIO-AF5 state during SPI, DMA config)
```

## Next Steps After Scope Verification
1. If Phase 1 passes but Phase 2 fails: enable SPI2 in RCC, verify FIFOs, check DMA bus
2. If Phase 2 passes but VERSIONR still 0x00: capture MISO line, check byte order/reception
3. If all phases pass but logic state is wrong: verify W5500 reset sequence, SPI mode (CPOL/CPHA)

---

## Scope Channel Usage Summary
| Phase | Pin    | Purpose                      | Notes                          |
|-------|--------|------------------------------|--------------------------------|
| 1     | PB13   | Verify GPIO pulse block      | Easy slow signal, safe to probe |
| 2     | PB13   | Verify SCK during SPI txn    | Harder (fast); needs peak-det  |
| 3     | PB15   | Verify MOSI frame            | Confirm command sent            |
| (ref) | PB12   | CS reference (already works) | Use as timing sync if needed    |

---

## Success Criteria
- **minimum**: Phase 1 passes (GPIO pulses visible) = pin path proven
- **target**: Phase 2 passes (SCK clock) = SPI2 peripheral proven running
- **ideal**: Phase 3 passes (MOSI frame) + scope-captured VERSIONR shows 0x04 in logic analyzer
