# USB-First Profile Checklist (Phase 4)

## Purpose

Provide a staged, low-risk migration path to make native USB the primary deployment/debug wire-protocol interface on the STM32F407 + USB-C (USB2.0 data-only, self-powered) hardware.

## Hardware Assumptions

- USB-C connector is used for data (D+/D-) only.
- Board is externally powered (do not rely on USB bus power).
- PA11/PA12 are routed to USB D-/D+.
- ST-Link/SWD remains available for recovery/programming.

## Stage A: Create `usb-first` Build Profile (No Wire-Protocol Cutover Yet)

### A1. Add profile selector in build script

File: `software/nanoFramework/toolchain/build.sh`

Add new case:

- `NF_BUILD_PROFILE=usb-first`
- Keep baseline feature toggles aligned with `w5500-native` unless explicitly needed:
  - `ENABLE_SYSTEM_NET="OFF"`
  - `ENABLE_CONFIG_BLOCK="OFF"`
  - `ENABLE_SNTP="OFF"`
  - `ENABLE_MBEDTLS="OFF"`
  - `ENABLE_HAL_MAC="FALSE"`
  - `ENABLE_STM32_MAC_ETH="FALSE"`

Acceptance:

- `NF_BUILD_PROFILE=usb-first` is accepted by script.
- Script banner prints profile note for `usb-first`.

### A2. Force USB OTG1 on in generated target `mcuconf.h`

File: `software/nanoFramework/toolchain/build.sh` (section appending `EOF_MCU_OVERRIDES`)

Append profile-independent USB overrides for `usb-first` profile:

- `#undef STM32_USB_USE_OTG1`
- `#define STM32_USB_USE_OTG1 TRUE`
- `#undef STM32_USB_USE_OTG2`
- `#define STM32_USB_USE_OTG2 FALSE`

Do not change these baseline values unless troubleshooting requires it:

- `STM32_USB_OTG1_IRQ_PRIORITY` (14)
- `STM32_USB_OTG1_RX_FIFO_SIZE` (512)

Acceptance:

- Generated target `mcuconf.h` in the build container resolves to `STM32_USB_USE_OTG1 TRUE`.
- Firmware builds successfully with `NF_BUILD_PROFILE=usb-first`.

### A3. Keep serial wire protocol on USART3 for first pass

Current baseline:

- `SERIAL_DRIVER SD3` in board/config path.

Do **not** switch wire protocol to USB in Stage A. This preserves a known-good debug channel while validating USB enumeration.

Acceptance:

- Existing UART-based config command flow still works unchanged.

---

## Stage B: Validate Native USB Enumeration

### B1. Flash and boot `usb-first` firmware

- Build: `docker compose run --rm -e NF_BUILD_PROFILE=usb-first nanoframework-build /work/toolchain/build.sh`
- Flash via ST-Link as usual.

### B2. Host-side USB validation (Linux)

On cable connect/boot:

- Check device enumeration: `lsusb`
- Check kernel messages: `dmesg | tail -n 100`
- If CDC ACM is enabled by target config, expect `/dev/ttyACM*`.

Acceptance:

- USB device reliably enumerates after reset/replug.
- No repeated disconnect/reconnect loop in `dmesg`.

### B3. CDC console validation (if exposed)

If `/dev/ttyACM*` exists:

- Open terminal: `screen /dev/ttyACM0 115200` (or equivalent)
- Verify boot logs appear.

Acceptance:

- Stable readable console over USB.

---

## Stage C: Move Wire Protocol Primary Interface to USB

### C1. Switch wire-protocol driver binding

Files to update:

- `software/nanoFramework/toolchain/build.sh` (generated `serialcfg.h` section)
- potential board-level serial/wire-protocol config headers copied from reference target

Change objective:

- Replace `SERIAL_DRIVER SD3` with USB serial driver symbol used by your target's serial USB stack (commonly `SDU1` in ChibiOS-based setups).

Important:

- Keep USART3 fallback path documented for rollback.

Acceptance:

- Wire protocol tools connect over USB without requiring UART bridge.
- Deploy/debug operations succeed over USB path.

### C2. Regression and fallback verification

- Reboot/reconnect multiple times and confirm deterministic reconnect behavior.
- Confirm fallback path still available (temporary): rebuild with USART3 wire protocol binding.

Acceptance:

- USB-first workflow is stable for connect/deploy/debug.
- Recovery path via ST-Link + UART remains available.

---

## Stage D: ISP / Update Path Clarification

- SWD/ST-Link remains the primary guaranteed programming/recovery path.
- Optional USB update path can be added separately:
  - STM32 ROM DFU (BOOT0-driven) and/or
  - nanoBooter USB update flow (if enabled in target).

Acceptance:

- Team documents which USB update path is officially supported and tested.

---

## Validation Matrix (Pass/Fail)

- Build
  - `usb-first` profile builds cleanly.
- USB enumerate
  - Host sees USB device on each reboot.
- Console
  - USB serial console works (if CDC enabled).
- Wire protocol
  - Connect/deploy/debug over USB passes.
- Rollback
  - Reverting to UART wire protocol is quick and reliable.

## Rollback Plan

Immediate rollback to known-good behavior:

1. Build with current stable profile (`minimal` or `w5500-native`).
2. Keep `SERIAL_DRIVER SD3` binding.
3. Flash with ST-Link.
4. Continue debug/commands over UART bridge.
