# Phase A Baseline Reference

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

The native firmware build runs inside the Docker container defined by
`software/nanoFramework/Dockerfile.build`.  The following versions are
pinned by that file and constitute the baseline toolchain.

| Tool                          | Version constraint       | Source                    |
|-------------------------------|--------------------------|---------------------------|
| Docker build base image       | `ubuntu:24.04`           | `Dockerfile.build` line 1 |
| cmake                         | `>= 3.31` (via pip)      | `Dockerfile.build` line 27|
| kconfiglib                    | `>= 14.1` (via pip)      | `Dockerfile.build` line 30|
| gcc-arm-none-eabi             | ubuntu:24.04 package     | `Dockerfile.build` line 20|
| binutils-arm-none-eabi        | ubuntu:24.04 package     | `Dockerfile.build` line 21|
| Docker Compose                | V2 (`docker compose`)    | `build-native.sh` line 4  |
| nf-interpreter branch         | `main`                   | `NF_INTERPRETER_REF` default|

Host-side tools (outside Docker):

| Tool            | Install command                                 | Minimum version |
|-----------------|-------------------------------------------------|-----------------|
| stlink-tools    | `sudo apt install stlink-tools`                 | any             |
| nanoff          | `dotnet tool install -g nanoff`                 | any             |
| mono-complete   | `sudo apt install mono-complete`                | any             |

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

## Host Setup Assumptions

These assumptions apply to all Phase A runs:

1. **Host OS**: Linux or WSL2 (Ubuntu 22.04 or later recommended).
2. **Docker Compose V2** is installed and `docker compose` (no hyphen) works.
3. `stlink-tools` is installed and `st-flash` is on `PATH`.
4. `nanoff` is installed as a global dotnet tool (`~/.dotnet/tools/nanoff`).
5. `mono-complete` and `msbuild` are installed for managed builds.
6. The `xbuild` shim exists:
   `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`
7. The target board's USB-UART adapter appears as `/dev/ttyUSB0`
   (adjust `--serialport` if the device path differs).
8. The ST-Link V2 is connected and recognized by `st-flash --version`.

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
- [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md) — Run history
- [Dockerfile.build](../../software/nanoFramework/Dockerfile.build) — Pinned build image
- [build-native.sh](../../software/nanoFramework/toolchain/build-native.sh) — Firmware build script
- Parent issue: [getpwnam/diseqc_cntrl#12](https://github.com/getpwnam/diseqc_cntrl/issues/12)
