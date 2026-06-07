# Phase A Functional Smoke Checks

## Purpose

This document defines **minimal functional smoke checks** for each hardware and
software component on the `M0DMF_CUBLEY_F407` target.  These checks verify
that each component *functions*, not merely that it is electrically present.

Smoke checks here are intentionally narrow: they establish a deterministic
pass/fail gate that is repeatable in the field.  Deeper characterisation,
corner-case coverage, and parametric validation belong in the Phase D test
campaigns (issues #15–#17).

---

## Relationship to Other Documents

| Document | Role |
|----------|------|
| [PHASE_A_BASELINE.md](./PHASE_A_BASELINE.md) | Build profile, flash map, and toolchain baseline that all Phase A runs must follow |
| [TESTING_GUIDE.md](./TESTING_GUIDE.md) | Step-by-step bring-up runbook that references these smoke checks |
| [DIAGNOSTICS_MAILBOX.md](./DIAGNOSTICS_MAILBOX.md) | Mailbox word encoding used by several checks below |
| [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md) | Log where pass/fail outcomes of each run are recorded |

---

## Retry and Failure Classification Policy

Applied uniformly to all checks unless overridden per component:

| Category | Definition |
|----------|-----------|
| **Hard fail** | Failure reproduces on every attempt across at least 3 independent power cycles |
| **Intermittent fail** | Failure reproduces on ≥ 1 but < all attempts; treat as hard fail for gate purposes until root-cause is established |
| **Pass** | All iterations complete successfully with expected evidence |

Minimum iteration count for a gate-level PASS: **3 consecutive successful runs** after any MCU reset.

---

## Component Smoke Checks

---

### 1 · UART (Wire Protocol)

**Component:** USART3 on PB10/PB11 — nanoFramework wire-protocol path

**Functional requirement:**
The nanoFramework wire protocol must be able to exchange messages with an
external host so that `nanoff` can read device status, deploy managed
assemblies, and the host can read debug output.

**Test procedure:**
1. Flash `cubley-stable` firmware (see PHASE_A_BASELINE.md).
2. Connect USB-UART adapter at `/dev/ttyUSB0`; 115200 8N1.
3. Run each of the following in sequence:

```bash
# (a) Enumerate device
nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --listdevices

# (b) Read device details
nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails

# (c) Deploy managed application
./toolchain/build-managed.sh build \
    --deploy --serialport /dev/ttyUSB0 --address 0x080C0000

# (d) Confirm managed output appears in terminal after deploy + reset
timeout 10s cat /dev/ttyUSB0
```

4. Repeat steps (a)–(d) across 3 MCU resets.

**Expected evidence:**
- (a) `nanoff --listdevices` exits rc=0 and enumerates the target.
- (b) `nanoff --devicedetails` exits rc=0 and reports HAL/firmware version.
- (c) Deploy completes without `E2001`/`E2002` errors.
- (d) Terminal shows managed boot banner (`DiSEqC Controller Starting…` or startup probe output).
- Evidence consistent across all 3 reset iterations.

**Pass criteria:**
All four evidence cases complete successfully and consistently across 3
consecutive MCU resets.

**Fail criteria:**
- `--listdevices` or `--devicedetails` returns a non-zero exit code or `E2001`/`E2002` error on any iteration.
- Deploy fails, times out, or reports assembly mismatch.
- No readable output on `/dev/ttyUSB0` after reset.
- Any evidence case fails intermittently across the 3 iterations.

**Notes / deferred deeper validation:**
High-speed baud rates (> 115200), wire-protocol stress testing, and
multi-host connection scenarios are Phase D scope.

---

### 2 · USB (CDC Serial)

**Component:** USB full-speed peripheral — CDC serial port to external host

**Functional requirement:**
The USB interface must enumerate on the external host as a CDC serial port and
support bidirectional data exchange at 115200 8N1.

**Test procedure:**
1. Flash a firmware profile with USB CDC enabled (e.g., `cubley-usb`).
2. Connect USB cable between board and host.
3. Observe host-side device enumeration:

```bash
# Linux host
dmesg | tail -20           # expect "cdc_acm" driver bound to /dev/ttyACM0 (or similar)
ls /dev/ttyACM*
```

4. Open a serial terminal (e.g., `screen /dev/ttyACM0 115200` or equivalent).
5. Trigger known output from the firmware (e.g., reset the board) and observe text.
6. Send a known character sequence and verify echo or response if the firmware supports it.
7. Repeat steps 2–6 across 3 MCU resets.

**Expected evidence:**
- `dmesg` shows successful CDC ACM enumeration on each reset.
- Serial terminal receives boot/diagnostic text from the MCU.
- Data sent from host is received by MCU (observed via debug log or status marker).

**Pass criteria:**
External host enumerates the CDC device and successfully sends and receives
data across all 3 reset iterations.

**Fail criteria:**
- Host does not enumerate a serial interface after USB connection.
- Host enumerates but cannot send or receive data reliably.
- Enumeration or data transfer fails intermittently across iterations.

**Notes / deferred deeper validation:**
USB suspend/resume cycling, high-throughput transfers, and multi-OS
compatibility are Phase D scope.

---

### 3 · LED / GPIO

**Component:** User LED driven via managed GPIO (labelled LED line on schematic)

**Functional requirement:**
The managed application must be able to drive the LED line HIGH and LOW
deterministically, and a timed pulse sequence must produce observable
state transitions at the pin.

**Test procedure:**
1. Deploy the `DiSEqC_Control` managed application (see PHASE_A_BASELINE.md).
2. Ensure the application has reached the main execution loop (confirmed via
   UART boot banner or mailbox boot-probe value).
3. From managed code (or via a dedicated smoke procedure), execute:
   - `SetHigh` → observe pin HIGH.
   - `SetLow` → observe pin LOW.
   - `Pulse(3, 250 ms)` → observe 3 transitions.
4. Observe with one or more of the following:
   - Physical eye/LED (for lit-LED proof).
   - Oscilloscope or logic analyser at the GPIO pin.
   - Mailbox/status marker written by the managed procedure.
5. Repeat the sequence 3 times with an MCU reset between each.

**Expected evidence:**
- Physical LED illuminates on `SetHigh` and extinguishes on `SetLow`.
- Oscilloscope shows matching high/low transitions at the pin.
- Pulse sequence produces 3 measured state changes within timing tolerance (± 20 ms per pulse).
- Status marker (if used) encodes expected stage/result per DIAGNOSTICS_MAILBOX.md.

**Pass criteria:**
All three operations (`SetHigh`, `SetLow`, `Pulse`) complete with observable
and correctly timed state transitions on every iteration.

**Fail criteria:**
- No transition observed on `SetHigh` or `SetLow`.
- LED or pin behaves inversely (inverted polarity without documented inversion).
- `Pulse` produces wrong count or out-of-tolerance timing.
- Any transition intermittently absent across iterations.

**Notes / deferred deeper validation:**
PWM duty-cycle accuracy, GPIO drive strength, and port-expander paths are
Phase D scope.

---

### 4 · FRAM

**Component:** FRAM (non-volatile memory) on the I2C bus

**Functional requirement:**
A byte-level write/read round-trip must produce an exact match across the
tested address range.

**Test procedure:**
1. Deploy the managed application with FRAM driver enabled.
2. From the managed FRAM smoke procedure:
   - Write a known byte pattern (e.g., `0xA5, 0x5A, 0x00, 0xFF, 0x01, …`) to a
     test address range (minimum 16 bytes; suggested range `0x00`–`0x0F`).
   - Read back the same range.
   - Compare read-back bytes to written bytes.
3. Power-cycle the board (full power remove + restore, not just reset) and
   repeat the read without a preceding write, to exercise non-volatility.
4. Repeat the full sequence (write → read → power-cycle → read) 3 times.

**Expected evidence:**
- Read-back byte dump matches written byte pattern exactly on every iteration.
- Post-power-cycle read returns the same pattern without a fresh write.

**Pass criteria:**
Exact byte-for-byte match across the test range on every read-back iteration,
including post-power-cycle reads.

**Fail criteria:**
- Any byte mismatch on any read-back.
- Partial write (some bytes correct, others wrong or defaulted).
- Nondeterministic or changing read values across iterations.
- Post-power-cycle read returns erased/default values (confirms FRAM is not
  retaining data).

**Notes / deferred deeper validation:**
Full-range endurance, wear-levelling, concurrent access, and config persistence
layer validation are Phase D scope.

---

### 5 · LNBH26

**Component:** LNBH26 LNB power and tone controller

**Functional requirement:**
Control commands must alter device state and the resulting status must be
readable and consistent with the commanded state.

**Test procedure:**
1. Deploy the managed application with LNBH26 driver enabled.
2. From the managed LNBH26 smoke procedure, execute in order:

```
Init()           → device initialises, no fault flags set
SetEnable(true)  → device enabled
SetVoltage(13V)  → 13 V output selected
ReadStatus()     → read and record status register
SetVoltage(18V)  → 18 V output selected
ReadStatus()     → read and record status register
SetTone(true)    → 22 kHz tone enabled
ReadStatus()     → read and record status register
SetTone(false)
SetEnable(false)
```

3. Record return codes and status register values at each step.
4. Where electrical measurement is available, confirm output voltage and tone
   presence with a multimeter/oscilloscope on the LNB output line.
5. Repeat across 3 MCU resets.

**Expected evidence:**
- `Init()` returns success; no fault flags in initial status register.
- Each `SetVoltage` command causes a corresponding status register change.
- Each `SetTone` command causes a corresponding status register change.
- All return codes indicate success.
- (If measurable) LNB output voltage matches commanded state (13 V / 18 V).
- Status register values are consistent and stable across all iterations.

**Pass criteria:**
Valid status transitions and consistent status register reads on all iterations.
Return codes from all commands indicate success.

**Fail criteria:**
- A command returns an error code.
- Status register does not change when a state-changing command is issued.
- Status register is unstable or returns different values on consecutive reads
  without intervening commands.
- Device reports a fault flag after initialisation.

**Notes / deferred deeper validation:**
OCP/OLP protection response, output accuracy calibration, 22 kHz tone
frequency tolerance, and DiSEqC pulse injection are Phase D scope.

---

### 6 · W5500 (Ethernet)

**Component:** W5500 SPI-connected Ethernet controller

**Functional requirement:**
The core SPI transport path must respond deterministically: version register
and PHY configuration register reads must return stable, plausible values.

**Test procedure:**
1. Deploy the managed application (firmware profile with W5500 enabled).
2. From the managed W5500 smoke procedure:

```
NativeOpen()          → initialise W5500 SPI path
ReadVersionRegister() → read VERSIONR register (expected: 0x04)
ReadPhyConfig()       → read PHYCFGR register
NativeClose()         → close/release path
```

3. Repeat `NativeOpen / ReadVersionRegister / ReadPhyConfig / NativeClose` 5
   times in the same run without an MCU reset.
4. Repeat the entire test across 3 MCU resets.

**Expected evidence:**
- `NativeOpen()` returns success on each call.
- `VERSIONR` reads `0x04` on every iteration.
- `PHYCFGR` returns a stable, non-zero value on every iteration (exact value
  depends on cable/link state; stability matters more than the specific value
  for this smoke check).
- `NativeClose()` returns success.
- SWD mailbox (if available) does not show a W5500 error word (`0xE1` family).

**Pass criteria:**
Deterministic, consistent `VERSIONR` = `0x04` and stable `PHYCFGR` across all
5 in-run repetitions and across all 3 MCU resets.

**Fail criteria:**
- `NativeOpen()` fails.
- `VERSIONR` returns `0x00` or any value other than `0x04`.
- `PHYCFGR` returns different values on consecutive reads without a link state
  change.
- Any hang or timeout during register reads.
- Inconsistent results across MCU resets.

**Notes / deferred deeper validation:**
Full TCP/UDP socket lifecycle, link autonegotiation, throughput, and W5500
interop contract stability are Phase D2 scope (issue #16).

---

### 7 · DiSEqC Transmit Path

**Component:** DiSEqC waveform output (TIM4_CH1 / LNBH26 DSQIN path)

**Functional requirement:**
Invoking a DiSEqC transmit command must produce a valid carrier waveform
with the correct structure at the output pin.

**Test procedure:**
1. Deploy the managed application with DiSEqC driver enabled.
2. Connect an oscilloscope to the DiSEqC output pin (TIM4_CH1 / DSQIN line):
   - Timebase: 100 µs/div
   - Voltage: 2 V/div
   - Trigger: rising edge, ~1 V
3. Issue a minimal DiSEqC command from the managed driver, e.g.:
   ```
   DiSEqC.Transmit(new byte[] { 0xE0, 0x31, 0x68, 0x01 })  // StepEast(1)
   ```
4. Observe and record:
   - Carrier frequency (expected: ~22 kHz).
   - Bit timing: bit-0 = 1 ms carrier + 0.5 ms silence; bit-1 = 0.5 ms carrier + 1 ms silence.
   - Total burst duration (4-byte frame ≈ 54 ms).
5. Verify via status marker or return code that the managed transmit call completed.
6. Repeat 3 times with an MCU reset between iterations.

**Expected evidence:**
- Oscilloscope shows a carrier burst at ~22 kHz during each transmit call.
- Bit-level structure is recognisable (alternating carrier/silence segments of
  ~0.5 ms and ~1 ms).
- Transmit return code indicates success.
- Status marker (if used) reflects a completed transmit stage.

**Pass criteria:**
Waveform present with expected carrier frequency (~22 kHz ± 10 %) and
recognisable bit structure on every iteration.

**Fail criteria:**
- No output observed on oscilloscope.
- Carrier frequency significantly outside 22 kHz ± 10 %.
- Waveform present but bit timing is malformed or non-deterministic.
- Transmit call returns an error or hangs.
- Output absent or malformed intermittently across iterations.

**Notes / deferred deeper validation:**
Full DiSEqC protocol decode, reply path (master-slave exchange), tone burst
accuracy, and positioner command set validation are Phase D3 scope (issue #17).

---

### 8 · Diagnostics Transport / Mailbox

**Component:** SWD-readable diagnostics mailbox (`g_cubley_diag_*` symbols)

**Functional requirement:**
Boot-stage and runtime markers written to the diagnostics mailbox must be
readable over SWD with the correct magic bytes and must progress in the
expected order, proving the diagnostics channel is reliable and decodable.

**Test procedure:**
1. Flash `cubley-stable` firmware and deploy the managed application.
2. After the managed application reaches its main loop, read all mailbox slots:

```bash
cd software/nanoFramework
./tests/swd_read_bringup_status.sh
```

3. Inspect the output for:
   - `g_cubley_diag_boot_probe_status`: magic `0xD5`, non-zero result field.
   - `g_cubley_diag_clr_status`: magic `0xD5`, stage progression matching
     startup sequence.
   - `g_cubley_diag_current_status`: magic `0xD5`, valid stage/result.
   - `g_cubley_diag_last_error`: `0x00000000` (no error) for a clean boot.
4. Power-cycle and re-read without new deployment.  Sticky slots must retain
   their values; transient current-status slot may differ.
5. Repeat across 3 MCU resets.

**Expected evidence:**
- Each slot read by the script returns a value whose top byte is `0xD5` (status
  magic) for status slots, confirming the symbol is initialised by managed code.
- `boot_probe_status` is non-zero after a successful managed startup.
- Stage bytes in `clr_status` increase monotonically through the startup
  sequence (no backwards jumps).
- `last_error` is `0x00000000` for a clean boot; any non-zero error value
  must be explicitly justified.
- Sticky slots (`boot_probe_status`) retain their values across power cycles.
- Behaviour is deterministic across all 3 reset iterations.

**Pass criteria:**
All slots return correctly-encoded values with valid magic bytes.  Stage
progression is in the expected order.  No unexpected error words.  Results are
deterministic across all iterations.

**Fail criteria:**
- Any slot returns `0x00000000` when a non-zero value is expected (indicates
  managed startup did not reach that stage).
- Magic byte in a status slot is not `0xD5` (indicates wrong symbol or
  uninitialised data being read).
- Stage bytes are out of order or missing.
- Sticky slots do not retain values across power cycles.
- Results differ across iterations without explanation.

**Notes / deferred deeper validation:**
Full coverage of all diagnostic stage codes, error injection testing, and
SWD-probing under load are Phase D scope.  For word encoding reference see
[DIAGNOSTICS_MAILBOX.md](./DIAGNOSTICS_MAILBOX.md).

---

## Summary Table

| # | Component | Key evidence | Minimum iterations |
|---|-----------|-------------|-------------------|
| 1 | UART (wire protocol) | `nanoff --listdevices` + `--devicedetails` both rc=0; deploy succeeds; boot banner visible | 3 resets |
| 2 | USB (CDC serial) | Host enumerates CDC device; bidirectional data exchange succeeds | 3 resets |
| 3 | LED / GPIO | `SetHigh`, `SetLow`, `Pulse(3)` all produce observed transitions within tolerance | 3 resets |
| 4 | FRAM | Exact byte-for-byte match on write/read round-trip; data retained across power cycle | 3 resets |
| 5 | LNBH26 | Status register changes on `SetVoltage`/`SetTone`; no fault flags; return codes success | 3 resets |
| 6 | W5500 | `VERSIONR` = `0x04`; stable `PHYCFGR`; no SWD error word | 3 resets × 5 in-run reads |
| 7 | DiSEqC transmit path | Oscilloscope shows ~22 kHz carrier burst with recognisable bit structure | 3 resets |
| 8 | Diagnostics mailbox | All slots correct magic; boot-probe non-zero; stage order valid; sticky slots persist | 3 resets |

---

## References

- [PHASE_A_BASELINE.md](./PHASE_A_BASELINE.md) — Build profile and toolchain baseline
- [TESTING_GUIDE.md](./TESTING_GUIDE.md) — Bring-up runbook
- [DIAGNOSTICS_MAILBOX.md](./DIAGNOSTICS_MAILBOX.md) — Mailbox word encoding
- [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md) — Run history and evidence log
- Parent issue: [getpwnam/diseqc_cntrl#23](https://github.com/getpwnam/diseqc_cntrl/issues/23)
- Phase A umbrella: [getpwnam/diseqc_cntrl#12](https://github.com/getpwnam/diseqc_cntrl/issues/12)
