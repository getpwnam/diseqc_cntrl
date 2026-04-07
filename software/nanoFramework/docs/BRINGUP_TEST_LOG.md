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
