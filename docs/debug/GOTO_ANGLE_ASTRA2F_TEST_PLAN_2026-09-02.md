# GoTo-Angle Astra 2F Repeatability Test Plan

Date: 2026-09-02
DUT: Cubley `cubley-a02663` with TM-2300 M3 positioner
Target: Astra 2F / 28.2 degrees east orbital slot

## Purpose

Prove that `diseqc goto-angle` can return the dish to the same 28.2 degrees east
RF peak after approaching from both lower and higher motor angles. This is a
bounded Gate 0 test, not an autonomous scan and not proof of motor feedback.

The test uses a stable horizontal, low-band anchor near 11.224 GHz RF
(approximately 1.474 GHz IF with a 9.75 GHz LO). Confirm the carrier assignment
before the run. Without independently verified service or catalogue metadata,
the RF result proves pointing at the 28.2 degrees east Astra cluster rather than
identifying the transmitting spacecraft uniquely as Astra 2F.

## Safety Gates

1. Confirm the motor's physical east/west stops, clear travel path, cable slack,
   dish fasteners, and mechanical zero/reference before applying movement.
2. Keep an immediately available `diseqc stop` command throughout the test.
3. Use conservative volatile software limits strictly inside the physical stops.
   Limits start disabled after every Cubley boot and must never be copied from
   this document without checking the installation.
4. Test only the target and two nearby flank positions. Do not use continuous
   drive or raw `diseqc tx`.
5. Stop on an unexpected direction, LNB fault, obstruction, cable tension,
   watchdog expiry, MQTT/console loss, or uncertain motion state.
6. User commands are issued over USB CDC. That console is not accessible from
   the development container.

## Values Required Before Arming Motion

Record these values before sending `diseqc angle-limits`:

| Item | Value |
| --- | --- |
| Station latitude, north positive | |
| Station longitude, east positive | |
| Independently calculated target direction | |
| Independently calculated target magnitude | |
| Cubley target after tenth-degree GoToX quantization (`A`) | |
| Lower signed-motor-angle flank (`L`) | |
| Higher signed-motor-angle flank (`H`) | |
| Command-coordinate mapping to observed physical east/west movement | |
| East software limit | |
| West software limit | |
| Motion watchdog | |
| RF receiver path and configuration | |
| Anchor centre frequency | |

Calculate the initial target using the host helper, substituting the measured
station coordinates:

```bash
cd /workspaces/diseqc_cntrl
PYTHONPATH=software/transponder-mapper/src /usr/bin/python3 -c \
  'from satmap.central.usals import calculate_motor_target; print(calculate_motor_target(LATITUDE, LONGITUDE, 28.2))'
```

Cross-check the result against one independent USALS implementation or a
known-good receiver before motion. Use a signed motor coordinate only in the
worksheet: east is positive and west is negative. Convert each command to
Cubley's explicit direction plus unsigned magnitude. In this plan, increasing
and decreasing refer only to that signed command coordinate. Record the observed
physical dish direction separately during the first screening legs; do not infer
it from the words `L`, `H`, east, or west.

Start with flank positions one motor degree on either side of `A`:

$$L=A-1.0^\circ$$

$$H=A+1.0^\circ$$

Both flanks must remain inside the verified software and hardware limits. A
one-degree offset should produce a clear anchor reduction for a typical Ku dish.
If it does not, increase the offset only after stopping and reassessing clearance;
do not exceed two degrees during this Gate 0 test.

## Measurement Metric

Use one receiver path and unchanged gain, bandwidth, detector, averaging, and
LNB state for the entire run. Prefer the rooftop bladeRF/Cubley path; the indoor
Siglent may provide a separate corroborating series.

For each dwell, record integrated anchor power relative to adjacent noise:

$$C/N_{adj}=P_{carrier}-P_{adjacent\ noise}$$

This relative metric is less sensitive than raw power to slow gain drift. Do not
change SDR gain or analyser reference settings after collecting the baseline.

## Preflight

1. Start an MQTT event/state capture if available, while retaining USB CDC for
   the interactive safety command.
2. Set and verify the anchor LNB state:

   ```text
   lnb a enable
   lnb a polarization horizontal
   lnb a band low
   show lnb a
   ```

3. Tune the receiver to the anchor and allow the LNB/receiver to warm and settle.
4. With the dish already peaked at the known anchor, collect ten stationary
   $C/N_{adj}$ measurements at fixed intervals. Record mean $\mu_0$ and standard
   deviation $\sigma_0$. Define the measurable-change threshold
   $T=\max(1.0\ \mathrm{dB},3\sigma_0)$ before moving.
5. Confirm fail-closed behavior after boot: `diseqc angle-limits status` must show
   disabled until deliberately armed. Do not issue a movement merely to test
   rejection if the current physical position is uncertain.
6. Configure checked limits and a watchdog no longer than 30 seconds for these
   one-to-two-degree legs, then read them back. Reduce it further only if prior
   measured travel time provides adequate margin; do not use the 90-second
   default for this bounded test.

   ```text
   diseqc angle-limits <east_limit_deg> <west_limit_deg>
   diseqc timeout <seconds>
   diseqc angle-limits status
   diseqc timeout status
   show diseqc
   ```

7. Verify the reported LNB voltage is appropriate for horizontal polarization
   and record it. The movement voltage affects travel time, not destination.

## Motion Handling For Every Leg

1. Send one `diseqc goto-angle <east|west> <degrees>` command and record its
   motion ID, requested angle, encoded angle, direction, and movement voltage.
2. Confirm the initial physical movement is in the expected direction. Send
   `diseqc stop` immediately if it is not.
3. Wait until the motor has physically stopped, then allow a fixed mechanical
   settle interval of at least five seconds.
4. Send `diseqc complete <motion_id>` only after observing the physical stop.
   This command sends no Halt; it only releases the matching firmware motion
   lock and records `completion=external`, `position_confidence=estimated`. This
   is an open-loop estimate at the encoded target, not stop sensing or RF
   verification. If
   physical cessation is uncertain, use `diseqc stop` instead and abort the run.
5. Run `show diseqc` and confirm the lock is idle, completion is `external`, and
   the requested/encoded target matches the worksheet.
6. Reapply the LNB state, then verify it before measuring RF:

   ```text
   lnb a polarization horizontal
   lnb a band low
   show lnb a
   show diseqc
   ```

7. Reject the dwell if completion is `timeout`, confidence is
   `verification_failed`, the encoded target differs from the worksheet, or an
   LNB/motion fault occurred.

If the watchdog expires, firmware attempts Halt, clears the motion lock, and
sets `position_confidence=verification_failed`. Do not send another movement.
Confirm physical cessation, issue `diseqc stop` if any doubt remains, inspect
`show diseqc` and `show lnb a`, disable angular limits, and end the test. If Halt
fails or motion continues, disable LNB/motor power using the station's emergency
power procedure.

## Test Sequence

Run one screening cycle first:

1. `A -> L`: confirm the physical direction matches the recorded mapping and the
   anchor decreases by at least $T$.
2. `L -> A`: settle, reapply LNB state, and measure the anchor.
3. `A -> H`: confirm the physical direction reverses and the anchor decreases by
   at least $T$.
4. `H -> A`: settle, reapply LNB state, and measure the anchor.

Stop here if either return fails. If both returns pass, run two more cycles for
three arrivals at `A` from each direction. Alternate the order to limit drift:

```text
Cycle 1: A -> L -> A -> H -> A
Cycle 2: A -> H -> A -> L -> A
Cycle 3: A -> L -> A -> H -> A
```

Do not count the initial already-peaked baseline as a directional arrival.

## Results

| Cycle | Approach to A | Motion ID | Requested A | Encoded A | Completion | Settle (s) | $C/N_{adj}$ (dB) | Result/notes |
| ---: | --- | ---: | ---: | ---: | --- | ---: | ---: | --- |
| 1 | from L / increasing angle | | | | | | | |
| 1 | from H / decreasing angle | | | | | | | |
| 2 | from H / decreasing angle | | | | | | | |
| 2 | from L / increasing angle | | | | | | | |
| 3 | from L / increasing angle | | | | | | | |
| 3 | from H / decreasing angle | | | | | | | |

Record separately:

| Metric | Result |
| --- | ---: |
| Baseline mean $\mu_0$ | |
| Baseline standard deviation $\sigma_0$ | |
| Measurable-change threshold $T$ | |
| Mean after increasing-angle approach | |
| Mean after decreasing-angle approach | |
| Difference between directional means | |
| Worst return below baseline mean | |
| Unexpected direction/fault/watchdog count | |

## Pass Criteria

All conditions must pass:

1. Six of six directional arrivals reacquire the same anchor carrier.
2. Every arrival encodes exactly the same target `A`; no arrival completes by
   watchdog timeout or reports `verification_failed`.
3. Every returned $C/N_{adj}$ is no more than $T$ below the stationary baseline
   mean.
4. The two directional mean values differ by no more than
   $T$.
5. Both flanks are at least $T$ below the stationary target baseline, proving
   that the sequence crossed the local RF peak rather than remaining inside a
   flat measurement region.
6. There are no unexpected-direction events, LNB faults, motion faults,
   obstructions, cable concerns, or emergency stops.

If only the directional-mean criterion fails, record the result as backlash or
approach-direction dependence, not as a reliable pass. Retain separate preferred
angles by approach direction until a later lobe fit determines a compensation.

## Shutdown

1. Return to `A` only if the last movement state is known and the test remains
   inside all safety gates; otherwise send `diseqc stop` and leave position
   confidence unknown.
2. Disable angular movement for the remainder of the session:

   ```text
   diseqc angle-limits off
   diseqc angle-limits status
   show diseqc
   ```

3. Apply the station's normal LNB power policy.
4. Preserve receiver traces, command/event capture, calculated target, firmware
   build identity, and this completed worksheet.
5. Append one factual PASS or FAIL entry to `docs/debug/BRINGUP_TEST_LOG.md` after
   reviewing the artifacts. Do not log a timeout as successful arrival.