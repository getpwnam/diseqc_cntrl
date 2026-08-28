# DiSEqC Symbol-Timing Fault Isolation and Fix Plan

Date: 2026-08-28

## Objective

Identify the source of the excessive DiSEqC symbol duration, replace the timing-sensitive managed transmit loop with deterministic timing, and verify the complete transmit path from MCU gate output to J8 against the DiSEqC bus specification.

## Evidence and limits

### Current measurement

The 2026-08-28 stopped-waveform analysis at J8 found:

- Carrier: 22.106 kHz (45.236 us period), based on 50 accepted periods.
- Logical-zero marks: 1.110 ms and 1.140 ms.
- Following spaces: 0.720 ms and 0.670 ms.
- Complete symbol times: 1.830 ms and 1.810 ms.
- Envelope-bin and edge uncertainty: approximately +/-10 us.
- Capture integrity warning: the descriptor declared 10,000,000 samples, but only 5,000,000 samples were returned. Only two complete symbols were available.

The carrier is correct. The excessive symbol duration is in the carrier-envelope gate timing.

### Specification-derived acceptance limits

Source: `.debug/diseqc_spec.pdf`, with searchable extraction in `.debug/diseqc_spec.txt`.

DiSEqC Bus Functional Specification 4.2 states:

- Base timing: 500 us +/-100 us.
- Logical zero: nominal 1.0 ms mark followed by 0.5 ms space.
- Logical one: nominal 0.5 ms mark followed by 1.0 ms space.
- Carrier: nominal 22 kHz +/-20%.
- End of message: at least 6 ms silence.
- Signalling amplitude: 650 mVpp +/-250 mVpp.

Protocol gates:

| Quantity | Allowed range |
| --- | ---: |
| Short interval | 0.400-0.600 ms |
| Long interval | 0.800-1.200 ms |
| Complete symbol, derived from the interval limits | 1.200-1.800 ms |
| Carrier | 17.6-26.4 kHz |
| End silence | >=6 ms |
| Bus amplitude | 0.400-0.900 Vpp |

The measured zero mark is within the long-interval limit, but both measured spaces exceed the 0.600 ms short-interval maximum. Both complete symbols exceed the derived 1.800 ms maximum even before allowing for the approximately 10 us analysis quantization. The approximately 0.78 Vpp amplitude and 22.106 kHz carrier pass.

Use tighter engineering targets after the fix to retain margin:

- Short interval: 0.480-0.520 ms.
- Long interval: 0.980-1.020 ms.
- Symbol: 1.480-1.520 ms.
- Run-to-run boundary jitter: <=20 us where the analog envelope permits it.

## Leading fault hypothesis

The current transmitter performs every envelope transition from managed code:

1. `PwmChannel.Start()` or `PwmChannel.Stop()` crosses the CLR/native boundary.
2. `DelayMicroseconds()` repeatedly calls `DateTime.UtcNow` in managed code.
3. The next managed/native PWM call occurs only after that delay returns.

The measured long mark exceeds its requested delay by 110-140 us, while the short space exceeds its requested delay by 170-220 us. A complete symbol gains approximately 310-330 us. That shape is consistent with fixed transition/interop overhead plus managed runtime scheduling, not with a carrier-frequency error.

Do not make subtractive calibration of the managed delays the production fix. It may pass one runtime load while remaining sensitive to garbage collection, USB, Ethernet/MQTT, debugger state, and firmware changes.

## Phase 1 — improve and preserve the baseline

1. Copy the stopped waveform, preamble, screenshot, and analysis JSON into a dated repository artifact directory. Retain hashes of the raw files.
2. Correct or explicitly document the scope descriptor/sample-count discrepancy before relying on full-frame duration.
3. Capture a complete `E0 10 38 F0` frame with enough post-trigger depth for all 36 encoded bits (eight data bits plus odd parity per byte) and at least 10 ms of trailing silence.
4. Extend the offline analyzer output to include every complete mark/space pair, decoded bit value, per-class min/mean/max, total frame duration, and pre/post silence. Fail analysis on truncated runs or an unexpected encoded bit sequence.
5. Repeat at least 10 transmissions to establish distributions rather than two samples.

Exit criterion: a reproducible baseline containing both logical-zero and logical-one intervals, complete-frame timing, and no unexplained acquisition truncation.

## Phase 2 — separate delay, transition, and analog-path contributions

Use two scope channels where possible:

- Channel 1: PD12 or TP15/DSQIN-A, referenced to board ground.
- Channel 2: J8 through the already validated AC-coupled 10x setup.

Confirm probe attenuation, channel setting, coupling, grounding, and safe voltage range before treating the capture as a physical measurement.

Run these bounded diagnostics:

1. **GPIO-only delay test:** toggle an otherwise safe diagnostic GPIO around managed `DelayMicroseconds(500)` and `DelayMicroseconds(1000)`. This measures the managed clock loop and GPIO-call overhead without PWM.
2. **PWM transition test:** emit repeated 500/1000 us PWM gates without frame encoding. Compare PD12 boundary timing with the requested schedule.
3. **Path latency test:** compare corresponding PD12 and J8 envelope edges. A stable small offset implicates software timing; variable or asymmetric propagation implicates LNBH26/path behavior.
4. **Load sensitivity test:** repeat idle, Ethernet/MQTT active, USB CDC traffic active, and debugger detached. Record min/mean/max and outliers for each interval class.
5. **Pattern test:** transmit frames chosen to provide long runs of zeros, long runs of ones, and alternating values. Include odd parity in the expected sequence.

Decision rule:

- If PD12 already has the excess duration, fix firmware timing.
- If PD12 passes but J8 fails, investigate LNBH26 EXTM/TEN gating and the C31-to-1-kOhm modification path.
- If only managed GPIO timing fails, `DateTime.UtcNow`/runtime scheduling is dominant.
- If GPIO timing passes but PWM gating fails, repeated `PwmChannel.Start()`/`Stop()` dispatch or timer-channel behavior is dominant.

## Phase 3 — implement deterministic native transmission

Preferred production design: one native call owns an entire frame transmission. Managed code validates/builds the frame and reports the result; it does not schedule individual symbol edges.

1. Append a `CubleyNative` InternalCall such as `LNBH26.NativeTransmitDiseqc(byte[] frame, int offset, int count)`. Do not reorder or repurpose existing interop slots.
2. In target-local native firmware, validate the frame length and encode each byte MSB-first with odd parity, matching `DiseqcFrameCodec`.
3. Keep TIM4 channel 1 as the 22 kHz carrier source on PD12.
4. Use a dedicated internal basic timer, provisionally TIM6 at 1 MHz, as a one-shot boundary scheduler for 500 and 1000 us intervals. TIM7 is the fallback subject to the final DAC/timer resource audit. At each callback, use the ChibiOS ISR-safe PWM channel enable/disable operation and schedule the next boundary. TIM2 must not be used: this target already assigns it to the ChibiOS system-timer driver through `STM32_ST_USE_TIMER 2`.
5. Block only the calling managed thread on a bounded completion event/semaphore. Do not busy-wait for the approximately 54 ms four-byte frame and do not mask interrupts for the frame duration.
6. Force carrier off on completion, validation error, timeout, or exception. Return a specific status and publish detailed native diagnostics without adding noisy user-facing console output.
7. Preserve at least 6 ms end silence; retain the current 15 ms inter-frame/guard gap unless a protocol sequence requires a different documented gap.
8. Audit TIM4 sharing with motor channels and confirm that TIM6 is not selected as a DAC trigger or used by another native subsystem before enabling GPT support. The transmitter must not silently reconfigure a timer used by an active peripheral. Enabling TIM6 requires target-local software configuration (`HAL_USE_GPT=TRUE` and `STM32_GPT_USE_TIM6=TRUE`); it does not require a PCB change or external timer pin.
9. Remove `EmitDiseqcBit()` and `DelayMicroseconds()` from the production frame path after native transmission passes. Continuous-tone behavior may remain separate, but carrier ownership must be explicit and mutually exclusive.

A TIM6 ISR introduces only symbol-boundary interrupts, unlike a 22 kHz periodic ISR. If the final resource audit rejects TIM6, use TIM7 or another unowned hardware timer, or use a timer/DMA edge schedule; do not return to managed edge timing. Keep the ISR limited to deterministic state advancement, PWM gating, next-deadline programming, and completion signalling.

## Phase 4 — software verification before hardware deployment

1. Add deterministic tests for:
   - MSB-first byte order.
   - Odd parity for `00`, `FF`, `E0`, `10`, `38`, and `F0`.
   - Exact 500/1000 us schedule generation for zeros and ones.
   - 36 encoded bits and nominal 54 ms data duration for `E0 10 38 F0`.
   - Frame-length rejection and timeout cleanup with carrier forced off.
2. Run the interop guard and checksum checks after appending the InternalCall.
3. Build both managed and native firmware from the target-local source of truth.
4. Verify that generated community-target files match target-local inputs; do not edit generated target files directly.
5. Deploy matching native firmware and managed assemblies together because the interop checksum will change.

Exit criterion: tests, interop guard, managed build, and native build all pass with the carrier-off failure invariant covered.

## Phase 5 — scope acceptance

Capture at least 30 complete `E0 10 38 F0` transmissions, including idle and Ethernet/MQTT/USB load cases. Analyze raw samples, not scope packet-wide automatic period measurements.

Acceptance requires:

- Every short and long interval inside the protocol gates.
- Engineering targets met across normal load, or a documented margin analysis if analog edge detection prevents the tighter target.
- Every symbol inside 1.200-1.800 ms; target 1.480-1.520 ms.
- Correct decoded 36-bit data/parity sequence.
- Nominal data duration approximately 54 ms for the four-byte frame.
- Carrier inside both the DiSEqC range and the board's existing 20-24 kHz LNBH26 target.
- J8 amplitude 0.400-0.900 Vpp.
- At least 6 ms trailing silence and the intended inter-frame gaps.
- Carrier forced off after success, rejected input, and induced timeout/error.
- No timing regression when MQTT, Ethernet interrupts, USB CDC, and the debugger state vary.

Append the final commands, artifact paths, measured distributions, and conclusion to the bring-up log only after the acceptance capture is complete.

## Immediate next action

Perform Phase 1 recapture and the Phase 2 dual-node/pattern measurements before modifying interop. They will confirm that the fault is present at PD12 and quantify the exact managed delay versus PWM-transition contributions. In parallel, the TIM4/TIM6/TIM7 ownership audit can proceed without changing hardware state. TIM2 has already been excluded because it is the active ChibiOS system timer.
