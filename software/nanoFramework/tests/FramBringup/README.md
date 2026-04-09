# FramBringup

Dedicated FRAM + LED bring-up app for the DiSEqC controller.

This test app intentionally keeps diagnostics out of the production app startup path.

## What It Tests

1. FRAM raw byte read from a fixed test address (default: 2046).
2. FRAM probe write with a reversible XOR pattern.
3. FRAM verify readback of probe value.
4. Restore original byte value after test.

This keeps the test self-contained and avoids dependencies on production app classes.

## LED Result Codes (PA2)

The app drives the status LED on PA2 to show test state/result without UART.

1. Test running: fast blink at 2 Hz (250 ms ON, 250 ms OFF).
2. PASS latched: one long pulse + two short pulses, then 1 Hz heartbeat.
3. FAIL latched: one long separator pulse, then N slow pulses, then a long pause (repeats), where N is failure code.

Failure code mapping:

1. Raw read failed.
2. Probe write failed.
3. Verify read failed.
4. Value mismatch.
5. Baseline restore failed.
6. Write ignored (readback unchanged from baseline).

## Notes

- LED-only signaling is useful when wire protocol/UART is busy or unreliable.
- Fail code 6 is a strong indicator of write-protect or hardware-level write gating.

## Interpreting Current Bring-up Outcome

If startup markers progress through stage 5 and diagnostics report code 6, the software stack is running and I2C reads are functional, but FRAM writes are not taking effect.

This points to board-level write gating rather than a managed app startup issue.

Recommended hardware checks:

1. Verify FM24CL16B pin 7 (WP) is solidly tied low on the assembled board.
2. Confirm FM24CL16B orientation and pin-1 alignment on the PCB.
3. Verify SDA/SCL pull-ups and bus levels during the write transaction.
4. Check continuity from MCU I2C3 lines (PA8/PC9) to FRAM pins.
