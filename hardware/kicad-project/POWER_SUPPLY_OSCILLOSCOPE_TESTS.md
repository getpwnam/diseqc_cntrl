# Power Supply Oscilloscope Tests (Beginner Guide)

This guide is for checking the 12V -> 3.3V buck regulator section with an FNIRSI 1013D.

Use this after replacing/repairing the power IC and before connecting sensitive loads.

## 1) Safety and Setup

1. Power the board from a current-limited bench supply if possible.
2. Start with current limit around `0.15A` to `0.25A`, then raise if needed.
3. Use a `10x` probe (recommended). Set both:
   - Probe switch to `10x`
   - Scope channel probe ratio to `10x`
4. Keep probe ground lead short. Prefer the little spring ground over the long clip lead for ripple/noise measurements.
5. Use Channel 1 for `VOUT`, Channel 2 for `PH` (switch node).
6. Save screenshots for each test so you can compare later.

## 2) Probe Points (on your board)

1. `VOUT / +3V3`: output rail after inductor.
2. `PH`: switch node at regulator pin/inductor input.
3. `VSENSE`: feedback pin (should regulate near reference).
4. `VIN`: regulator input pin (12V rail near IC, not far away connector).

## 3) FNIRSI 1013D Recommended Starting Settings

Use these as starting points, then fine-tune.

### A) For VOUT ripple

1. Coupling: `AC`
2. Vertical: start at `20 mV/div` (or `50 mV/div` if too noisy)
3. Timebase: start at `5 us/div`
4. Trigger: edge on CH1, auto, rising
5. Bandwidth limit: if your firmware has a BW limit option, enable it for ripple checks

### B) For switch node (PH)

1. Coupling: `DC`
2. Vertical: start at `2 V/div` then `5 V/div` if clipping
3. Timebase: `500 ns/div` to `2 us/div`
4. Trigger: edge on CH2, rising, level near midpoint of PH waveform

### C) For startup capture

1. Coupling: `DC`
2. Timebase: `1 ms/div` to `10 ms/div`
3. Trigger: single-shot edge trigger on `VIN` rising (or `VOUT` rising)
4. Capture CH1 = `VOUT`, CH2 = `PH` or `VIN`

## 4) Test Sequence (Exact Order)

## Test 1: DC sanity check first

1. Measure with DMM:
   - `VIN` (expected near 12V)
   - `VOUT` (target around 3.3V)
2. If VOUT is far off (for example <3.0V or >3.6V), stop and fix before scope deep-dive.

## Test 2: VOUT ripple (most important)

1. Probe directly across output capacitor pads (as close as possible to regulator output capacitor ground).
2. Use short ground spring.
3. Read `Vpp` (peak-to-peak).

What to look for:

1. Good: stable sawtooth/ripple, usually tens of mV p-p.
2. Caution: >`80 mVpp` at light/normal load.
3. Bad: large spikes, random bursts, or ripple jumping dramatically with no load change.

## Test 3: Switch node PH waveform

1. Probe PH node.
2. Confirm pulses swing from near 0V toward VIN.
3. Check ringing on edges.

What to look for:

1. Good: regular switching pulses, clean repetitive pattern.
2. Caution: moderate ringing at edges.
3. Bad: very large overshoot, irregular missing pulses under constant load, or repeated start-stop hiccup pattern.

## Test 4: Startup behavior

1. Use single-shot trigger.
2. Power-cycle board and capture VOUT startup.

What to look for:

1. Good: VOUT ramps up once and settles near target.
2. Caution: small overshoot then settle.
3. Bad: repeated startup attempts (hiccup), large overshoot above safe rail limits.

For a 3.3V rail, keep an eye on overshoot:

1. Preferably below about `3.5V`.
2. Investigate immediately if it exceeds about `3.6V`.

## Test 5: Light-load and normal-load comparison

1. Capture VOUT ripple at very light load.
2. Capture again at expected real load.

What to look for:

1. Light load may show burst/PFM behavior (normal for many buck regulators).
2. Normal load should look more regular.
3. If heavy-load ripple or droop is much worse than expected, inspect output caps, inductor, and layout return path.

## Test 6: VSENSE node check

1. Probe VSENSE with DC coupling.
2. In regulation, VSENSE should be near the IC reference.

What to look for:

1. Good: around reference (commonly about `0.8V` for this class of buck).
2. Bad: pinned very low or unstable while PH is actively switching.

## 5) Quick Pass/Fail Checklist

Pass if all are true:

1. VOUT is near target (roughly 3.3V range you expect).
2. VOUT ripple is modest and repeatable.
3. PH waveform is regular under steady load.
4. Startup is one clean ramp (no hiccup loops).
5. VSENSE is near reference in steady state.

Fail and investigate if any are true:

1. VOUT overshoot above safe limit for your 3.3V logic.
2. Very high ripple or random burst behavior under fixed load.
3. VSENSE very low with active switching.
4. Repeated startup cycles.

## 6) Common Beginner Mistakes

1. Using long probe ground clip for ripple measurement (creates fake noise/spikes).
2. Probe set to 10x but scope channel set to 1x (wrong voltage readings).
3. Measuring far from regulator instead of at IC/capacitor pads.
4. Judging ripple only by visual trace without reading measured `Vpp`.

## 7) Suggested First Session Plan (15 minutes)

1. DMM check VIN/VOUT.
2. Scope VOUT ripple at light load.
3. Scope PH waveform.
4. Startup capture.
5. Save 4 screenshots and label them with load condition.

If you want, add a short section with your measured results directly in this file after each test so future bring-up is faster.
