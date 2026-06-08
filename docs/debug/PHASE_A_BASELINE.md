# Phase A Baseline Reference

> IMPORTANT (2026-06-08): This document describes the legacy `cubley-stable`
> baseline and is now **reference-only**. The program baseline has moved to
> `cubley-base` (`M0DMF_CUBLEY_V0.4`) as part of the native firmware
> reimplementation decision. Phase A and Phase B must be revisited and
> re-baselined on the new configuration before gate completion claims.

This document is the **canonical baseline** for all Phase A bring-up and flash
campaigns on the `M0DMF_CUBLEY_F407` target.  Every Phase A run must be
executed against this baseline unless the deviation is explicitly declared
(see [Non-baseline runs](#non-baseline-runs)).

---

## Selected Build Profile

| Field            | Value           |
|------------------|-----------------|
| Profile name     | `cubley-stable` |
| Profile status   | stable          |
| Wire protocol    | USART3 (UART)   |
| RTC              | enabled         |
| Config block     | enabled         |
| System.Net       | disabled        |
| HSI PLL          | enabled         |

`cubley-stable` is the default Phase A firmware profile.  All runs that do
not state a different profile are implicitly using `cubley-stable`.

---

## Flash Map

The build script patches the reference linker script so the final in-use layout is:

| Region       | Start address | End address  | Size   | Contents                       |
|--------------|---------------|--------------|--------|-------------------------------|
| nanoBooter   | `0x08000000`  | `0x08004000` | 16 KB  | nanoBooter (bootloader)        |
| nanoCLR      | `0x08004000`  | `0x080C0000` | 752 KB | nanoCLR (runtime kernel)       |
| Deployment   | `0x080C0000`  | `0x08100000` | 256 KB | Managed application deployment |

Total flash on STM32F407xG: **1 MB** (0x08000000 – 0x08100000).

The addresses `0x08000000`, `0x08004000`, and `0x080C0000` are the **only three
addresses** used in baseline flash and deploy commands.

---

## Repeatable Build and Flash Commands

All commands are run from `software/nanoFramework/`.

### 1. Build firmware

```bash
./toolchain/build-native.sh build --profile cubley-stable
```

Artifacts are written to `build/`:

- `build/nanoBooter.bin`
- `build/nanoCLR.bin`
- `build/nanoCLR.elf`

### 2. Flash firmware over SWD (ST-Link)

```bash
st-flash write build/nanoBooter.bin 0x08000000
st-flash write build/nanoCLR.bin    0x08004000
```

### 3. Build managed application

```bash
./toolchain/build-managed.sh compile
```

### 4. Deploy managed application over UART wire protocol

```bash
./toolchain/build-managed.sh build \
    --deploy --serialport /dev/ttyUSB0 --address 0x080C0000
```

Alternatively, using `nanoff` directly:

```bash
nanoff --nanodevice \
    --serialport /dev/ttyUSB0 \
    --baud 115200 \
    --deploy \
    --image build/DiSEqC_Control/DiSEqC_Control.bin \
    --address 0x080C0000 \
    --reset
```

---

## Tooling Versions

All build, flash, and deploy operations run **inside the devcontainer** defined
by `.devcontainer/Dockerfile`.  No host-side tool installs or Docker Compose
invocations are required.  The following versions are pinned by the
devcontainer image and constitute the baseline toolchain.

| Tool                | Version / constraint  | Source                                     |
|---------------------|-----------------------|--------------------------------------------|
| gcc-arm-none-eabi   | `15.2.rel1` (pinned)  | `.devcontainer/Dockerfile` `ARG GCC_VERSION`   |
| cmake               | `3.31.6` (pinned)     | `.devcontainer/Dockerfile` `ARG CMAKE_VERSION` |
| kconfiglib          | `>= 14.1` (pip)       | `.devcontainer/Dockerfile` line 150        |
| stlink-tools        | ubuntu package        | `.devcontainer/Dockerfile` line 45         |
| nanoff              | dotnet global tool    | `.devcontainer/Dockerfile` lines 137–139   |
| nf-interpreter ref  | `main`                | `NF_INTERPRETER_REF` default               |

---

## Baseline Wiring

### Debug UART (USART3)

```
STM32 Board          USB-Serial Adapter
-----------          ------------------
PB10 (TX) -------→   RX
PB11 (RX) ←-------   TX
GND      ←-------→   GND
```

- Baud rate: **115200**
- Data: 8 bits, Parity: None, Stop: 1 bit (8N1)

### SWD Programming (ST-Link V2)

```
ST-Link V2           STM32 Board
----------           -----------
SWDIO   ←---------→  SWDIO
SWDCLK  ←---------→  SWDCLK
GND     ←---------→  GND
3.3 V   ←---------→  VCC (only if board is not self-powered)
```

---

## Setup Assumptions

All Phase A runs are performed **inside the devcontainer** defined by
`.devcontainer/`.  The devcontainer pre-installs every required tool; there
is no need to install Docker Compose, nanoff, or stlink-tools on the host.

1. **Open the devcontainer** in VS Code (or equivalent) — this is the sole
   prerequisite for build and flash work.
2. **USB passthrough** is configured in `.devcontainer/devcontainer.json`:
   - `--privileged` for ST-Link USB access.
   - `--device=/dev/ttyUSB0:/dev/ttyUSB0` for UART wire protocol.
   - On WSL2/Windows hosts, attach the USB device to WSL first
     (e.g. with `usbipd`), then rebuild/reopen the container.
3. The target board's USB-UART adapter is expected at `/dev/ttyUSB0`
   inside the container (adjust `--serialport` if the path differs).
4. The ST-Link V2 is connected and `st-flash --version` works inside the
   container before running any flash commands.

---

## Non-Baseline Runs

Any run that deviates from this baseline (different profile, different flash
addresses, different tooling, partial wiring, etc.) **must** be declared
non-baseline at the point of logging.

### In bringup log entries

Use the `--baseline no` flag when appending to `BRINGUP_TEST_LOG.md`:

```bash
./toolchain/bringup_log_append.sh \
    --result INFO \
    --baseline no \
    --conclusion "Experimental cubley-uart run — non-baseline, W5500 bring-up only"
```

The log entry will be annotated with `[NON-BASELINE]` to make the deviation
immediately visible in the log.

### In ad-hoc notes

Prefix any description or commit message with `[NON-BASELINE]` when the run
is outside the parameters above.

---

## References

- [MANAGED_DEPLOYMENT.md](./MANAGED_DEPLOYMENT.md) — Detailed deployment guide
- [TESTING_GUIDE.md](./TESTING_GUIDE.md) — Bring-up validation workflow
- [PHASE_A_FUNCTIONAL_SMOKE_CHECKS.md](./PHASE_A_FUNCTIONAL_SMOKE_CHECKS.md) — Per-component functional smoke check definitions and pass/fail criteria
- [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md) — Run history
- [.devcontainer/Dockerfile](../../.devcontainer/Dockerfile) — Pinned devcontainer image (toolchain versions)
- [.devcontainer/devcontainer.json](../../.devcontainer/devcontainer.json) — USB device passthrough config
- [build-native.sh](../../software/nanoFramework/toolchain/build-native.sh) — Firmware build script
- Parent issue: [getpwnam/diseqc_cntrl#12](https://github.com/getpwnam/diseqc_cntrl/issues/12)
