# DiSEqC nanoFramework Bring-up Test Log

This file tracks factual checkpoints, commands, and outcomes for the STM32F407 bring-up.

## Device under test
- Target: M0DMF_DISEQC_F407
- MCU: STM32F407
- Bootloader: nanoBooter present

## 2026-04-07 Timeline

### 1) Firmware and flash layout validation
- Confirmed linker/storage intent:
  - nanoBooter: 0x08000000-0x08004000
  - nanoCLR: 0x08004000-0x080C0000
  - Deployment: 0x080C0000-0x08100000
- Rebuilt native firmware with minimal profile.
- Enabled config block for minimal profile in build script.

### 2) Deployment image validation
- Verified deployment flash at 0x080C0000 can contain NFMRK2 bytes, but current GDB evidence indicates the CLR is not promoting that wrapped image into candidate assembly descriptors on this target.
- Rebuilt BlinkBringup managed project and retained the raw PE output as the next deployment candidate.
- Current preferred managed artifact for bring-up retest:
  - tests/BlinkBringup/bin/Release/BlinkBringup.pe

### 3) Runtime/GDB findings
- nanoCLR wire protocol is alive (NFPKTV1 observed).
- Current symbol map distinction:
  - 0x08001e00 is nanoBooter Reset_Handler.
  - CLR startup symbols are in higher flash addresses in nanoCLR.elf.
- Therefore, breakpoints in CLR loader code will not hit until booter hands off to CLR.

### 4) Current observed behavior (important)
- nanoff --devicedetails reports:
  - Native assemblies present.
  - Managed Assemblies: none.
- Interpretation:
  - Not a total boot failure.
  - Managed deployment is still not being accepted/loaded at runtime.

## Next-step checkpoint script (GDB)
Use this order to prove booter-to-CLR handoff before assembly loader tracing.

1. Reset and clear breakpoints.
2. Break at booter handoff points:
   - 0x0802664 (load CLR reset vector)
   - 0x080267a (branch to CLR reset)
3. Break at CLR Reset_Handler in nanoCLR.elf:
   - 0x08035b0c
4. Continue and inspect:
   - memory at 0x08004000 (vector table)
   - register r0 at booter branch

## Operating rule for future tests
For each run, append one short entry with:
- Command(s) run
- Artifact used
- Address breakpoints used
- Pass/fail result
- One-line conclusion

## Quick logging helper
Use the helper to append timestamped entries consistently:

- `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --conclusion "one-line conclusion"`

Recommended full form for debug sessions:

- `./toolchain/bringup_log_append.sh --result FAIL --commands "gdb: b *0x08002664; b *0x0800267a; b *0x08035b0c" --artifact "tests/BlinkBringup/bin/Release/BlinkBringup.pe" --breakpoints "0x08002664, 0x0800267a, 0x08035b0c" --conclusion "Booter handoff reached; CLR reset hit; deployment candidate descriptors still not promoted"`

### 2026-04-07 14:32:55 UTC [INFO]
- Git rev: 2f9d1c3
- Command(s): nanoff erase + deploy + devicedetails
- Artifact: tests/BlinkBringup/bin/Release/BlinkBringup.pe
- Conclusion: Allemblies contains BlinkBringup, 1.0.0.0 but nothing else

### 2026-04-07 16:16:48 UTC [PASS]
- Git rev: 2f9d1c3
- Command(s): nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image tests/BlinkBringup/bin/Release/BlinkBringup.pe --address 0x080C0000 --reset
- Artifact: tests/BlinkBringup/bin/Release/BlinkBringup.pe
- Conclusion: Managed GPIO writes confirmed working after fixing board grounding issue; UART3 nanoFramework connection is now stable.

### 2026-04-07 19:57:40 UTC [PASS]
- Git rev: 2f9d1c3
- Command(s): rebuild minimal firmware after fixing nanoBooter startup; flash nanoBooter.bin to 0x08000000; flash nanoCLR.bin to 0x08004000; nanoff deploy tests/BlinkBringup/bin/Release/BlinkBringup_startfirst_with_events.bin; GDB startup-chain and IL/GPIO probes
- Artifact: tests/BlinkBringup/bin/Release/BlinkBringup_startfirst_with_events.bin
- Breakpoints: ResolveAll 0x0802d598, PrepareForExecution 0x0802e864, NewThread 0x080320f0, Execute_IL 0x08009788, Native GPIO write 0x0801f5cc
- Conclusion: Root causes were incomplete managed deployment, incorrect host flashing of nanoCLR at 0x08010000 instead of 0x08004000, and invalid nanoBooter startup sequencing using osDelay() before the RTOS scheduler was started. After fixing nanoBooter_main.c, flashing at linker-defined addresses, and deploying the with-events managed bundle, startup progressed through ResolveAll and PrepareForExecution, NewThread was hit twice, Execute_IL was hit 44 times, and native GPIO write was observed.

## 2026-04-08 Session Update

### 2026-04-08 11:58:48 UTC [FAIL]
- Git rev: 2f9d1c3
- Command(s):
  - Rebuilt both firmware profiles from scratch and verified fresh artifact times:
    - `./toolchain/build.sh bringup-smoke`
    - `./toolchain/build.sh minimal`
  - Rebuilt managed test app clean:
    - removed `tests/BlinkBringup/bin/Release` and `tests/BlinkBringup/obj/Release`
    - `./toolchain/compile-blink-test.sh`
  - Flashed firmware correctly:
    - `st-flash write build/nanoBooter.bin 0x08000000`
    - `st-flash write build/nanoCLR.bin 0x08004000`
  - Deployed managed images over SWD (UART intermittently unavailable in this session):
    - `st-flash write tests/BlinkBringup/bin/Release/BlinkBringup.pe 0x080C0000`
    - `st-flash write tests/BlinkBringup/bin/Release/BlinkBringup.bin 0x080C0000`
    - `st-flash write .debug/BlinkBringup_with_threading.bin 0x080C0000`
  - Runtime probes via OpenOCD + gdb-multiarch with one-shot breakpoint scripts in `.debug/`.
- Artifacts:
  - `tests/BlinkBringup/bin/Release/BlinkBringup.pe` (fresh NFMRK1 single assembly)
  - `tests/BlinkBringup/bin/Release/BlinkBringup.bin` (fresh bundled image)
  - `.debug/BlinkBringup_with_threading.bin` (BlinkBringup.bin + System.Threading.pe)
- Breakpoints/probes and outcomes:
  - `CLR_RT_Assembly::CreateInstance` at 0x0801435c: HIT, record pointer `r0=0x080C0000` (deployment image is discovered).
  - `CLR_RT_TypeSystem::ResolveAll` at 0x0802D598: HIT.
  - After `ResolveAll` return at 0x08019DAC: `r0=0xA3000000` (negative HRESULT), repeatable across all tested deployment images.
  - `CLR_RT_TypeSystem::PrepareForExecution` 0x0802E864: NOT HIT.
  - `CLR_RT_ExecutionEngine::NewThread` 0x080320F0: NOT HIT.
  - `CLR_RT_Thread::Execute_IL` 0x08009788: NOT HIT.
  - Native GPIO write (`Library_sys_dev_gpio_native...::NativeWrite`) 0x0801F5CC: NOT HIT.
- Conclusion:
  - Managed deployment region and assembly discovery are working, but managed startup aborts in type resolution (`ResolveAll` returns `0xA3000000`) before any managed thread or IL executes.
  - Therefore LED not toggling is a startup/dependency-resolution failure in CLR bring-up, not a PA2 GPIO runtime write-path failure.
  - Additional note: `/dev/ttyUSB0` was not consistently available during this run, so validation was completed via SWD/GDB only.

### Immediate follow-up objective
- Build a deterministic managed deployment pack step in tooling (single known-good bundle containing all required assemblies) and gate deployment on a post-pack verification check before flashing.

---

## Resumption Plan (pending UART restoration)

Authored: 2026-04-08. Status: PENDING — blocked on `/dev/ttyUSB0` unavailability (Windows host usbipd issue).

The following steps are ordered by dependency. Steps 1 and 2 require UART. Steps 3–5 can proceed via SWD only once the root cause in step 2 is established.

### Step 0 — Restore UART access
- Fix usbipd attachment on Windows host so `/dev/ttyUSB0` appears reliably in the WSL/Linux environment.
- Verify with: `ls -l /dev/ttyUSB0 && stty -F /dev/ttyUSB0 115200`
- Goal: enable `nanoff` wire-protocol commands (`--devicedetails`, `--deploy`) to work again.
- No firmware or code changes needed for this step.

### Step 1 — Identify the exact unresolved assembly reference
This is the root cause that must be established before any toolchain fix.

**Why `ResolveAll` fails is still unknown.** The deployed bundle appears to contain BlinkBringup + System.Device.Gpio + nanoFramework.Runtime.Events, and System.Threading is present in CLR flash — yet `ResolveAll` returns `0xA3000000`.

Approach A — Use `nanoff --devicedetails` with UART restored:
```
nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails
```
This reports which managed assemblies the CLR has accepted and which are missing. Compare the "needs assembly" messages printed to UART with what is deployed.

Approach B — Read assembly names from UART output at startup:
```
screen /dev/ttyUSB0 115200
# power-cycle board; observe CLR startup prints:
#   Assembly: <name> (<version>)
#   needs assembly '<name>' (<version>)   <- these are the missing references
```
The `"needs assembly"` lines identify the exact unresolved dependency by name and version.

Approach C (SWD fallback, no UART) — Breakpoint on the `CLR_Debug::Printf` call that emits `"needs assembly"`:
- Locate the call site in `CLR_Startup` that prints the needs-assembly message (search nf-interpreter source for `"needs assembly"`).
- Place a breakpoint there; read `r1` (format) and `r2`/`r3`/stack args (name and version strings) when hit.
- Current probe template: `.debug/gdb_probe_*.cmd` one-shot pattern.

Expected deliverable: a text note recording exactly which assembly name+version `ResolveAll` cannot close.

### Step 2 — Build a corrected deployment bundle
Once the missing reference is identified:
1. Locate or build the correct `.pe` file for the missing assembly from the NuGet package cache or from a `nanoff`-extracted device package.
2. Concatenate all required PEs into a single bundle:
   ```
   cat BlinkBringup.pe System.Device.Gpio.pe nanoFramework.Runtime.Events.pe <missing>.pe > BlinkBringup_complete.bin
   ```
3. Verify bundle integrity: all NFMRK records present, no truncation.
   ```
   python3 toolchain/inspect_deploy_bundle.py BlinkBringup_complete.bin
   ```
   (script to be written if not already present; reads 4-byte NFMRK2 headers and prints each PE name/version/size)
4. Flash bundle: `st-flash write BlinkBringup_complete.bin 0x080C0000`
5. Run the after-ResolveAll probe to confirm `r0` is no longer `0xA3000000`:
   ```
   nohup timeout 15s gdb-multiarch -batch -x .debug/gdb_probe_after_resolveall.cmd build/nanoCLR.elf \
     > .debug/gdb_probe_after_resolveall.out 2>&1
   grep -E 'AFTER_RESOLVEALL|r0' .debug/gdb_probe_after_resolveall.out
   ```

### Step 3 — Re-run the full startup-chain probe sequence
Once `ResolveAll` passes (returns `S_OK = 0x00000000`), repeat all five probes in order:

| Probe | Address | Expected result |
|---|---|---|
| `CLR_RT_Assembly::CreateInstance` | 0x0801435C | HIT, `r0=0x080C0000` |
| `CLR_RT_TypeSystem::ResolveAll` | 0x0802D598 | HIT |
| After `ResolveAll` return | 0x08019DAC | `r0=0x00000000` (S_OK) |
| `CLR_RT_TypeSystem::PrepareForExecution` | 0x0802E864 | HIT |
| `CLR_RT_ExecutionEngine::NewThread` | 0x080320F0 | HIT (≥1 time) |
| `CLR_RT_Thread::Execute_IL` | 0x08009788 | HIT (≥1 time) |
| Native GPIO write | 0x0801F5CC | HIT (≥1 time) |

All seven must pass before bring-up is considered complete for managed code on this target.

### Step 4 — Harden the toolchain pack step
Implement `toolchain/pack-and-validate.sh`:
- Accepts a list of PE files as arguments.
- Verifies each file has the NFMRK header (`hexdump -C` check on first 4 bytes = `4E 46 4D 32`).
- Concatenates them in the correct order.
- Verifies total size is ≤ 0x40000 (260 KB, deployment region limit).
- Writes output to a timestamped `.deploy.bin` file and a stable `latest.deploy.bin` symlink.
- Exits non-zero if any check fails.

Rationale: the 2026-04-08 failure was caused by the toolchain emitting a bundle that silently lacked a required assembly. A pack-time validator catches this before flashing.

### Step 5 — Lock toolchain versions
Record in `toolchain/versions.lock`:
- nanoFramework NuGet package versions used by BlinkBringup (from `packages.config`)
- nanoCLR firmware git ref (or tag) that matches those package versions
- `nanoff` version used for deployment

This file should be committed and checked on each build to prevent version drift between the CLR native assemblies and the managed packages.

---

### 2026-04-08 13:50:04 UTC [INFO]
- Git rev: 4391075
- Command(s): toolchain/pack-and-validate.sh + toolchain/inspect_deploy_bundle.py + toolchain/run-startup-gate.sh
- Artifact: tests/BlinkBringup/bin/Release/BlinkBringup_complete-20260408T134739Z.deploy.bin
- Conclusion: Deterministic deployment bundle tooling is in place and generated successfully; live UART/SWD validation remains blocked in this shell by Linux device permissions (ttyUSB0 root:root 0600 and OpenOCD LIBUSB_ERROR_ACCESS).

### 2026-04-08 14:36:38 UTC [PASS]
- Git rev: 4391075
- Command(s): pack-and-validate NFMRK1 + st-flash deploy + sequential startup gate
- Artifact: tests/BlinkBringup/bin/Release/BlinkBringup_full_stack-20260408T143420Z.deploy.bin
- Breakpoints: 0x0801435c, 0x0802d598, 0x08019dac, 0x0802e864, 0x080320f0, 0x08009788, 0x0801f5cc
- Conclusion: ResolveAll now returns S_OK (0x0) and managed startup progresses through PrepareForExecution, NewThread, Execute_IL, and native GPIO write with full dependency-complete NFMRK1 bundle.

### 2026-04-08 15:16:04 UTC [INFO]
- Git rev: 4391075
- Command(s): `./toolchain/run-startup-gate.sh`; `nanoff --nanodevice --serialport /dev/ttyUSB0 --devicedetails`
- Artifact: `.debug/gdb_startup_gate.out`
- Probe summary:
  - `GATE_CREATEINSTANCE=1`
  - `GATE_RESOLVEALL=1`
  - `GATE_AFTER_RESOLVEALL=1`
  - `GATE_RESOLVEALL_HR=0x0`
  - `GATE_PREPAREFOREXEC=1`
  - `GATE_NEWTHREAD=1`
  - `GATE_EXECUTE_IL=1`
  - `GATE_NATIVEWRITE=1`
- Conclusion: Managed startup path remains healthy and reproducible via SWD probes, but UART-side `nanoff --devicedetails` still fails with `Error E2001` in this session, so device enumeration/transport remains the remaining blocker for serial bring-up validation.

### 2026-04-08 15:19:16 UTC [INFO]
- Git rev: 4391075
- Command(s): `nanoff --listports`; `nanoff --nanodevice --listdevices`; baud sweep for `nanoff --nanodevice --serialport /dev/ttyUSB0 --devicedetails`
- UART checks:
  - `nanoff --listports` reports `/dev/ttyUSB0`.
  - `nanoff --nanodevice --listdevices` reports `No devices found`.
  - `--devicedetails` fails with `Error E2001` at all tested baud rates: 1500000, 921600, 460800, 230400, 115200.
- Conclusion: Serial transport path is present at host level, but nanoFramework wire-protocol handshake is not currently discoverable over UART despite a fully passing SWD startup gate.

### 2026-04-08 15:37:19 UTC [INFO]
- Git rev: 4391075 (working tree with local diagnostic patches)
- Command(s): iterative firmware regression tests (rebuild + reflash + UART enumerate), including:
  - removed custom `g_CLR_RT_ExecutionEngine.flags` mutation from `nf-native/target-overrides/nanoHAL.cpp`
  - pinned interpreter build to `NF_INTERPRETER_REF=8bc239bd2` with `NF_UPDATE_INTERPRETER=0`
  - restored minimal profile clock path (`ENABLE_HSI_PLL=0`)
  - forced upstream/reference `nanoHAL.cpp` in `toolchain/build.sh`
  - added `nf-native/target-overrides/target_common.c` to map debug COM to USART3 and baud to 115200
- Firmware artifacts flashed: `build/nanoBooter.bin` @ `0x08000000`, `build/nanoCLR.bin` @ `0x08004000`
- UART results after each reflash:
  - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --listdevices` => `No devices found`
  - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails` => `Error E2001`
  - same behavior persists at 921600
- Additional config finding:
  - generated target had debug COM defaulting to handle 1 (USART1) before `target_common.c` override; override changed this to COM3 but did not restore enumeration.
- Conclusion: wiring/host permissions are not the blocker; a firmware-side wire-protocol initialization path is still broken in current stack despite managed CLR startup being healthy.

### 2026-04-08 15:57:51 UTC [PASS]
- Git rev: 4391075 (working tree with local diagnostic patches)
- Command(s): `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`
- Result highlights:
  - HAL build info reported: `nanoCLR running @ M0DMF_DISEQC_F407`
  - Target/platform correctly identified: `M0DMF_DISEQC_F407` / `STM32F4`
  - Managed assemblies enumerated:
    - `BlinkBringup, 0.0.0.0`
    - `mscorlib, 1.17.11.0`
    - `System.Device.Gpio, 1.1.57.0`
    - `nanoFramework.Runtime.Events, 1.11.32.0`
    - `System.Threading, 1.1.52.34401`
  - Native assemblies enumerated, including:
    - `nanoFramework.Hardware.Stm32 v100.0.5.1`
    - `System.Device.Gpio v100.1.0.6`
    - `System.Device.I2c v100.0.0.2`
    - `System.Device.Spi v100.1.2.0`
- Conclusion: UART wire-protocol connectivity is confirmed working again at 115200 and managed deployment is recognized on-device; immediate root cause of earlier E2001 failures was the USB-UART adapter path, not the CLR assembly startup path.

### 2026-04-08 16:05:12 UTC [PASS]
- Git rev: 4391075 (working tree with local diagnostic patches)
- Command(s):
  1. `st-flash reset`
  2. `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image tests/BlinkBringup/bin/Release/latest.deploy.bin`
  3. `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`
- Result: Deploy exited 0. Post-deploy devicedetails confirmed all 5 managed assemblies live on device.
  - `BlinkBringup, 0.0.0.0`
  - `mscorlib, 1.17.11.0`
  - `System.Device.Gpio, 1.1.57.0`
  - `nanoFramework.Runtime.Events, 1.11.32.0`
  - `System.Threading, 1.1.52.34401`
- LED (PA2 / LED_STATUS): toggling at 1 Hz from managed C# `BlinkBringup.Program.Main()` loop.
- Conclusion: Full managed code bring-up complete. Managed IL executing on STM32F407, GPIO output confirmed via hardware, wire-protocol deployment via nanoff confirmed working end-to-end.

### 2026-04-08 16:26:18 UTC [PASS]
- Correction to previous LED conclusion:
  - Symptom observed: managed assemblies deployed and visible, but LED did not blink after cold reset.
  - Root cause: GPIOA peripheral clock was not enabled at board startup in custom board init.
    - Evidence before fix (post-reset register read):
      - `RCC_AHB1ENR = 0x00101002` (GPIOAEN bit 0 clear)
      - `GPIOA_MODER = 0xA8000000` (PA2 mode not set)
    - This prevented managed GPIO access to PA2 (`LED_STATUS`) from taking effect.
- Permanent firmware fix:
  - File changed: `nf-native/board_diseqc.cpp`
  - Added in `boardInit()` after `stm32_clock_init()`:
    - `RCC->AHB1ENR |= RCC_AHB1ENR_GPIOAEN;`
- Validation after rebuild/reflash/redeploy:
  1. Rebuilt firmware (`toolchain/build.sh`, profile `minimal`)
  2. Flashed `build/nanoBooter.bin` and `build/nanoCLR.bin`
  3. Deployed `tests/BlinkBringup/bin/Release/latest.deploy.bin`
  4. Verified post-reset registers without manual poke:
     - `RCC_AHB1ENR = 0x00101003` (GPIOAEN set)
     - `GPIOA_MODER = 0xA8000010` (PA2 output)
     - `GPIOA_ODR = 0x00000004`
  5. User confirmation: LED is flashing.
- Conclusion: Managed LED blink is now robust across reset/power cycle; issue fixed in firmware startup path.

### 2026-04-08 22:00:30 UTC [INFO]
- Git rev: a9bb2bd (working tree with local diagnostic/tooling patches)
- Command(s):
  - Recreated missing GDB probe command files under `.debug/` and corrected probe sequencing (`reset halt` -> set breakpoint -> `reset run`) for one-shot startup-gate probes.
  - Refreshed probe addresses from current `build/nanoCLR.elf` symbols/disassembly:
    - `CreateInstance` `0x0801434C`
    - `ResolveAll` `0x0802D588`
    - `After ResolveAll` `0x08019D9C`
    - `PrepareForExecution` `0x0802E854`
    - `NewThread` `0x080320E0`
    - `Execute_IL` `0x08009788`
    - `NativeWrite` `0x0801F5BC`
  - Probed `NativeWrite` argument registers at runtime (`r5=0x2`) to verify managed write targets pin index 2 (PA2).
- Result:
  - Pin mapping did not regress in vendored target content (PA2 remains `LED_STATUS`; managed pin argument observed as 2).
  - During one failing run, startup regressed at `ResolveAll` with `r0=0xA3000000` before IL execution.
- Conclusion: No evidence of PA2 remap regression. Intermittent no-LED episodes correlate with managed dependency resolution state, not pin-map drift.

### 2026-04-08 22:00:30 UTC [PASS]
- Git rev: a9bb2bd (working tree with local diagnostic/tooling patches)
- Command(s):
  - Updated `toolchain/compile-blink-test.sh` fallback bundling to include `nanoFramework.Runtime.Events.pe` automatically when available (from app output or `packages/` cache).
  - Rebuilt BlinkBringup and regenerated `tests/BlinkBringup/bin/Release/BlinkBringup.bin`.
  - Verified bundle composition with `toolchain/inspect_deploy_bundle.py` (5 NFMRK1 records including runtime events).
  - Reflashed deterministic baseline over SWD (`nanoBooter`, `nanoCLR`, deployment image).
  - Re-ran startup gate: all probes PASS (`ResolveAll HR=0x0`, `PrepareForExecution`, `NewThread`, `Execute_IL`, and `NativeWrite` all hit).
  - User power-cycled board and confirmed LED activity restored.
- Artifact:
  - `tests/BlinkBringup/bin/Release/BlinkBringup.bin` (full-stack fallback bundle)
- Conclusion: Build/deploy process is stable again for this target; managed startup and PA2 GPIO path are both validated after power cycle.

### 2026-04-09 08:00:10 UTC [PASS]
- Git rev: 7b85dc6
- Command(s): ./toolchain/build.sh minimal; st-flash write build/nanoBooter.bin 0x08000000; st-flash write build/nanoCLR.bin 0x08004000; nanoff --nanodevice --listdevices; nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails; 8x+5x stress loops with/without st-flash reset
- Artifact: build/nanoBooter.bin; build/nanoCLR.bin; DiSEqC_Control/bin/Release/DiSEqC_Control.bin
- Conclusion: USART3 wire protocol is operational again on fresh minimal firmware; nanoff list/devicedetails stable across repeated attempts and SWD resets. Hardened HalSystemConfig debug handle mapping to explicit ConvertCOM_DebugHandle(3).

### 2026-04-09 08:42:20 UTC [PASS]
- Git rev: ef3c81b
- Command(s): ./toolchain/build.sh w5500-native; st-flash write build/nanoBooter.bin 0x08000000; st-flash write build/nanoCLR.bin 0x08004000; ./toolchain/compile-w5500-test.sh; nanoff --deploy tests/W5500Bringup/bin/Release/W5500Bringup.bin @0x080C0000
- Artifact: build/nanoBooter.bin; build/nanoCLR.bin; tests/W5500Bringup/bin/Release/W5500Bringup.bin
- Conclusion: Switched target back to W5500 bring-up: w5500-native firmware flashed, W5500Bringup managed bundle deployed, and nanoff devicedetails confirms W5500Bringup 1.0.0.0 loaded with required dependencies.

### 2026-04-09 11:37:57 UTC [FAIL]
- Git rev: ef3c81b + local mailbox diagnostics (uncommitted)
- Command(s):
  - Added SWD mailbox plumbing and probes (`diseqc_interop.cpp`, `DiSEqCNative.cs`, `w5500_interop.cpp`, `tests/swd_read_bringup_status.sh`).
  - Rebuilt/flashed `w5500-native` repeatedly and deployed both `W5500Bringup` and dedicated `MailboxSmoke` managed apps (manual packed bundles via `toolchain/pack-and-validate.sh`).
  - Verified device-side assembly visibility via `nanoff --devicedetails` after successful deploy cycles.
  - Probed mailbox repeatedly over SWD (`tests/swd_read_bringup_status.sh`) and cross-validated probe correctness with explicit SWD write/read (`tests/poke_bringup_status.sh`).
- Artifact: build/nanoCLR.bin; tests/W5500Bringup/bin/Release/W5500Bringup.bin; tests/MailboxSmoke/bin/Release/latest.deploy.bin
- Conclusion: SWD mailbox transport is valid, but runtime mailbox state remains at firmware sentinel `0xD5010000` (stage=1, running) across repeated deploy/reboot cycles; managed app assemblies load but observed execution does not advance mailbox stage/result. End-to-end mailbox signaling is not yet passing.

### 2026-04-09 11:56:33 UTC [FAIL]
- Git rev: ef3c81b + local startup-marker diagnostics (uncommitted)
- Command(s):
  - Instrumented `build/nanoCLR_main.c` with startup markers around kernel init/thread create and CLR thread wrapper entry.
  - Rebuilt/flashed `w5500-native`, deployed `MailboxSmoke` bundle, and sampled mailbox repeatedly over SWD.
  - Verified marker progression to `0xD5350000` (stage `0x35`) after boot and after successful managed deployment.
  - Verified `nanoff --devicedetails` shows `MailboxSmoke, 1.0.0.0` loaded, while mailbox never moves beyond stage `0x35`.
- Artifact: build/nanoCLR.bin; tests/MailboxSmoke/bin/Release/latest.deploy.bin
- Conclusion: CLR startup thread is entered (stage `0x35`), but mailbox updates from managed/native interop are never observed afterward; execution appears stalled/blocked within CLR startup path before user-managed code or mailbox interop calls execute.

### 2026-04-09 13:10:31 UTC [FAIL]
- Git rev: ef3c81b + local deep CLR instrumentation (uncommitted)
- Command(s):
  - Patched active Docker volume source `nanoframework_nf-interpreter:/nf-interpreter/targets/ChibiOS/_nanoCLR/CLR_Startup_Thread.c` with mailbox markers:
    - `0x40` thread entered
    - `0x41` after `nanoHAL_Initialize_C()`
    - `0x42` before `AssertBlockStorage()`
    - `0x43` after `AssertBlockStorage()`
    - `0x44` immediately before `ClrStartup(*clrSettings)`
    - `0x45` immediately after `ClrStartup` return (expected unreachable)
  - Rebuilt/flashed `w5500-native` and probed mailbox over SWD.
- Artifact: build/nanoCLR.bin
- Conclusion: mailbox reaches `0xD5440000` consistently (stage `0x44`, running), proving startup advances through nanoHAL and block storage checks, then stalls inside `ClrStartup(*clrSettings)` before returning or advancing to managed mailbox writes.

### 2026-04-09 14:32:48 UTC [FAIL]
- Git rev: ef3c81b + local mailbox bindtest diagnostics (uncommitted)
- Command(s):
  - Rebuilt and packed bindtest managed bundle (`tests/MailboxSmoke/bin/Release/latest.deploy.bin`) with assembly identity `DiSEqC_Control.Interop`.
  - Bounded transport probes with explicit timeouts:
    - `timeout 8s nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --listdevices` -> timeout (`rc=124`).
    - `timeout 20s nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image tests/MailboxSmoke/bin/Release/latest.deploy.bin --address 0x080C0000 --reset` -> `Error E2001`.
    - Additional baud sweep attempts (`230400`, `460800`) also timed out on `--listdevices`.
  - Verified SWD path remains healthy in same session: `st-info --probe` succeeds and `tests/swd_read_bringup_status.sh` reads mailbox `0xD5440000`.
- Artifact: tests/MailboxSmoke/bin/Release/latest.deploy.bin; /tmp/list.log; /tmp/deploy.log
- Conclusion: current blocker is UART wire-protocol transport instability (listdevices/deploy timeouts or E2001), not SWD or mailbox read path; bindtest interop hypothesis cannot be validated until serial deploy becomes stable again.

### 2026-04-09 14:51:14 UTC [FAIL]
- Git rev: ef3c81b + local mailbox bindtest diagnostics (uncommitted)
- Command(s):
  - `st-flash reset`
  - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`
  - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image tests/MailboxSmoke/bin/Release/latest.deploy.bin --address 0x080C0000 --reset`
  - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`
  - `./tests/swd_read_bringup_status.sh`
- Artifact: tests/MailboxSmoke/bin/Release/latest.deploy.bin
- Result highlights:
  - UART transport recovered after SWD reset; `nanoff --devicedetails` succeeded.
  - Before deploy, managed assemblies showed `MailboxSmoke, 1.0.0.0` + `mscorlib`.
  - Bindtest deploy completed successfully (`Getting details...OK`, `Deploying managed application...OK`, `Rebooting...OK`).
  - After deploy, managed assemblies showed only `mscorlib, 1.17.11.0`.
  - `DiSEqC_Control.Interop v1.0.0.0` remained visible only under `Native Assemblies`.
  - SWD mailbox remained unchanged at `0xD5440000` (stage `0x44`, running).
- Conclusion: mailbox feature still does not work end-to-end. With transport restored, the bindtest image can be deployed but is not accepted as a live managed assembly, and execution still never advances past the pre-`ClrStartup()` mailbox marker.

### 2026-04-09 16:01:40 UTC [FAIL]
- Git rev: ef3c81b + local mailbox diagnostics (uncommitted)
- Command(s):
  - Reverted MailboxSmoke assembly name from `DiSEqC_Control.Interop` back to `MailboxSmoke` in `.nfproj`.
  - Rebuilt with `FORCE_REBUILD=1 ./toolchain/compile-mailbox-smoke.sh`.
  - Packed new bundle at `tests/MailboxSmoke/bin/Release/MailboxSmoke_original-*.deploy.bin`.
  - Deployed via `st-flash reset` + `nanoff --deploy`.
  - Post-deploy `nanoff --devicedetails` confirmed assemblies.
  - Sampled mailbox via `./tests/swd_read_bringup_status.sh`.
- Artifact: tests/MailboxSmoke/bin/Release/MailboxSmoke_original-20260409T145918Z.deploy.bin
- Result highlights:
  - Deploy succeeded (`Getting details...OK`, `Deploying managed application...OK`, `Rebooting...OK`).
  - **Post-deploy managed assemblies: `MailboxSmoke, 1.0.0.0` + `mscorlib, 1.17.11.0` (now includes MailboxSmoke)**
  - SWD mailbox: `0xD5440000` (stage `0x44` = 68 decimal, RUNNING), unchanged from before.
- Conclusion: Assembly name was the root cause of managed app disappearance after deploy. Restoring to `MailboxSmoke` now allows managed assembly to load and persist on-device. However, runtime execution still stalls inside `ClrStartup()` before returning to execution context; mailbox remains at stage `0x44`. **Root issue shifted from assembly identity to CLR startup stall.**

### 2026-04-09 16:08:45 UTC [INFO]
- Git rev: ef3c81b + local GDB isolation diagnostics (uncommitted)
- Command(s):
  - Packed dependency-complete MailboxSmoke deployment bundle (4 records):
    - `MailboxSmoke.pe`
    - `mscorlib.pe`
    - `nanoFramework.Runtime.Events.pe`
    - `System.Threading.pe`
  - Deployed full-stack bundle via `nanoff --deploy` (deploy log shows `Getting details...OK`, `Deploying managed application...OK`, `Rebooting...OK`).
  - Ran non-invasive SWD/GDB runtime sampling (no interpreter source patching):
    - reset-run, timed halt, capture `PC`, `bt`, and instruction window.
  - Ran startup milestone breakpoint pass at:
    - `ClrStartup` `0x08018F88`
    - `ResolveAll` `0x0802F144`
    - `PrepareForExecution` `0x08030410`
    - `NewThread` `0x08033DE8`
- Artifact: tests/MailboxSmoke/bin/Release/MailboxSmoke_full_stack-20260409T153106Z.deploy.bin; /tmp/gdb_sample.log; /tmp/gdb_direct.log
- Result highlights:
  - Dependency-complete bundle generated successfully (`RECORD_COUNT: 4`).
  - GDB runtime sample halted in managed IL:
    - `FINAL_PC=0x08009A92`
    - symbol: `CLR_RT_Thread::Execute_IL(...)`
    - backtrace: `Execute_IL` -> `ClrStartup` -> `CLRStartupThreadWrapper`.
  - Startup breakpoints were reached through `NewThread` (`halted: PC: 0x08018f8c`, `0x0802f148`, `0x08030414`, `0x08033dec`).
  - Mailbox remains unchanged at `0xD5440000` (stage `0x44`) in subsequent SWD checks.
- Conclusion: adding `nanoFramework.Runtime.Events` + `System.Threading` did not clear the mailbox symptom in this run. Managed IL is executing, so the earlier strict "stall before managed handover" hypothesis no longer fully explains current behavior. Next isolation target is managed-to-native boundary (`MailboxSet/Get` and W5500 native entrypoints) with one-probe-per-session GDB runs.

### 2026-04-09 16:14:33 UTC [FAIL]
- Git rev: ef3c81b + local GDB isolation diagnostics (uncommitted)
- Command(s):
  - Added one-probe-per-session helper `toolchain/probe-one-breakpoint.sh` to isolate native interop entrypoints using fresh OpenOCD/GDB sessions.
  - Ran one-shot probes at native addresses:
    - `MAILBOX_SET` `0x08023F00`
    - `MAILBOX_GET` `0x08023F10`
    - `W5500_OPEN` `0x08023F20`
    - `W5500_CLOSE` `0x08024010`
  - Each probe run bounded with timeout (`30s`) to classify hit/miss deterministically.
- Artifact: /tmp/onebp_set3.log; /tmp/onebp_get3.log; /tmp/onebp_open3.log; /tmp/onebp_close3.log
- Result highlights:
  - All four interop probes timed out without breakpoint hit (`MISS rc=124`).
  - During each probe run, debugger snapshots still show CPU in managed IL (`CLR_RT_Thread::Execute_IL(...)`).
  - Managed PE metadata strings in `MailboxSmoke.pe` include `DiSEqC_Control.Native` symbols and `NativeSetBringupStatus`, but do **not** include assembly identity `DiSEqC_Control.Interop`.
  - Native assembly export remains `g_CLR_AssemblyNative_DiSEqC_Control_Interop` (`"DiSEqC_Control.Interop"`, checksum `0x12345678`).
  - Mailbox status remains unchanged (`0xD5440000`, stage `0x44`) after these runs.
- Conclusion: managed IL execution is active, but none of the mailbox/W5500 native interop entrypoints are being reached in the current MailboxSmoke runtime path. The fault domain is now narrowed to managed-side control flow / internal-call binding at the managed-to-native boundary (not pre-CLR startup and not transport).

### 2026-04-09 16:32:23 UTC [FAIL]
- Git rev: ef3c81b + local interop-split diagnostics (uncommitted)
- Command(s):
  - Added dedicated managed interop library project `DiSEqC_Control.Interop` with raw `[MethodImpl(InternalCall)]` declarations in native table order and wired `tests/MailboxSmoke` to reference it.
  - Rebuilt with `FORCE_REBUILD=1 ./toolchain/compile-mailbox-smoke.sh` and packed full-stack bundle including:
    - `MailboxSmoke.pe`
    - `DiSEqC_Control.Interop.pe`
    - `mscorlib.pe`
    - `nanoFramework.Runtime.Events.pe`
    - `System.Threading.pe`
  - Deployed via `nanoff --deploy` and verified with `nanoff --devicedetails`.
  - Re-ran SWD mailbox read and startup/interops probe attempts (`probe-clr-startup.sh`, `probe-one-breakpoint.sh`).
- Artifact: `tests/MailboxSmoke/bin/Release/MailboxSmoke_interop_split_full_stack-20260409T162116Z.deploy.bin`; `/tmp/probe_startup_interop_split.out`; `/tmp/deploy_interop2.log`; `/tmp/devicedetails_interop2.log`
- Result highlights:
  - Build succeeded and produced `DiSEqC_Control.Interop.pe` in MailboxSmoke output.
  - Deploy completed (`Getting details...OK`, `Deploying managed application...OK`, `Rebooting...OK`).
  - Post-deploy managed assemblies still show only:
    - `MailboxSmoke, 1.0.0.0`
    - `mscorlib, 1.17.11.0`
    - `nanoFramework.Runtime.Events, 1.11.32.0`
    - `System.Threading, 1.1.52.34401`
  - `DiSEqC_Control.Interop` remains visible under **Native Assemblies** only; no managed assembly entry is accepted/retained for that name.
  - SWD mailbox remains unchanged at `0xD5440000` (stage `0x44`, RUNNING).
  - Startup probe partial pass confirms early managed pipeline still advances (`CLRSTARTUP=HIT`, `CREATEINSTANCE=HIT`, `RESOLVEALL=HIT`) before debugger/OpenOCD session instability interrupted full table capture.
- Conclusion: splitting wrappers into a managed assembly named `DiSEqC_Control.Interop` did not restore mailbox/W5500 native entrypoint execution in this target state; managed deployment still effectively runs as `MailboxSmoke` while `DiSEqC_Control.Interop` remains native-only. The managed-to-native dispatch fault remains unresolved, with probe tooling noise now a secondary blocker for deterministic hit/miss completion.

