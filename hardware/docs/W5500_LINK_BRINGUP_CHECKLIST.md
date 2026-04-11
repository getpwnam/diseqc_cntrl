# W5500 Ethernet Link Bring-Up Checklist

This checklist is for in-system debug when SPI communication with W5500 works, but Ethernet link does not come up.

## Quick Answer: Transformer Continuity

You are correct in principle.

- Continuity within one winding side is normal (end-to-end and end-to-center-tap).
- No DC continuity should exist across the isolation barrier (PHY side to cable side).
- If there is low-ohm continuity across primary to secondary, suspect wrong part, solder bridge, or damage.

For this design:

- PHY-side winding nets: TD+, TD-, RD+, RD-
- Cable-side winding nets: TX+, TX-, RX+, RX-
- Isolation component: T1 (H1102NL)

## Required Tools

- DMM with resistance/continuity mode
- Oscilloscope with 10x probe, ideally >=100 MHz bandwidth
- Ethernet switch known to be good
- Known-good cable

## Bring-Up Order (Fast Path)

1. Confirm power and reset first.
2. Confirm 25 MHz oscillator.
3. Confirm W5500 PHY register status over SPI.
4. Confirm TX analog activity with cable plugged to live switch.
5. Confirm magnetics and center-tap networks.

## 1) DMM Checks (Power Off)

### 1.1 Transformer continuity sanity

Expected:

- T1 primary winding groups show continuity:
  - TD+ <-> CT_1 <-> TD-
  - RD+ <-> CT_2 <-> RD-
- T1 secondary winding groups show continuity:
  - TX+ <-> CT_4 <-> TX-
  - RX+ <-> CT_3 <-> RX-
- No continuity between any primary pin and any secondary pin.

If primary-to-secondary continuity is present, stop and inspect for assembly fault.

### 1.2 Resistor value spot-checks

Measure in-circuit where possible (expect some parallel-path deviation):

- R23, R24, R25, R26: 33 ohm series (TX/RX differential legs)
- R27, R28: 49.9 ohm to +3V3 (TX bias)
- R18: 12.4k (EXRES1 to GND)
- R29, R30: 470 ohm LED resistors

## 2) DMM Checks (Power On)

### 2.1 W5500 supply rails

- AVDD/VDD pins: around 3.3 V
- 1V2O pin: around 1.2 V
- RESETn pin: high in run state

If 1V2O is missing or low, link will not come up.

### 2.2 Key bias nodes

- EXRES1 node (through R18 to GND) should be stable, not floating.
- PMODE pins should resolve to intended strap state after reset.

## 3) Scope Checks

### 3.1 Crystal/clock check (critical)

Probe XI/XO network:

- Expect oscillation near 25 MHz.
- No oscillation -> fix clock path before any Ethernet debug.

### 3.2 TX pair activity during autonegotiation

With cable connected to active switch:

- Probe on IC6-side TX pair (TXP/TXN) and after 33 ohm resistors.
- Expect periodic burst activity during negotiation.

Interpretation:

- Flatline on TX pair: likely reset/clock/PHY config issue.
- TX activity present but no link: likely magnetics mapping, center-tap network, connector-side issue, or marginal analog layout/termination.

### 3.3 LED lines

- Check ACTLED and LNKLED at IC6 and across R29/R30 to jack LED pins.
- If PHY says link up but LEDs never change, check LED polarity and resistor routing.

## 4) SPI Register Checks (Runtime)

Read repeatedly while plugging/unplugging cable:

- PHYCFGR (especially LNK, SPD, DPX)

Expected behavior:

- LNK toggles low/high with cable unplug/plug to active switch.
- If LNK never asserts despite valid SPI reads, focus on analog front-end path.

## 5) Design-Specific Nets to Probe

From this design netlist:

- W5500 PHY pins:
  - TXN: IC6 pin 1
  - TXP: IC6 pin 2
  - RXN: IC6 pin 5
  - RXP: IC6 pin 6
- Through series resistors:
  - TXN -> R24 -> TD-
  - TXP -> R23 -> TD+
  - RXN -> R26 -> RD-
  - RXP -> R25 -> RD+
- Transformer to jack:
  - TX+/TX- -> J8 pins 1/2
  - RX+/RX- -> J8 pins 3/6
- Center taps:
  - CT_1 and CT_2 AC-terminated via C42/C41 to GND

## 6) If Link Still Fails: High-Probability Causes

1. Missing/unstable 25 MHz oscillation.
2. Reset timing or PMODE strap mismatch.
3. Wrong transformer orientation or pin mapping mismatch.
4. Center-tap termination/ground reference issue.
5. Cable-side common-mode handling is too weak for this environment.
6. Damaged jack/magnetics from ESD or soldering.

## 7) Practical A/B Tests

- Swap cable and switch port.
- Test with forced 10 Mbps mode (if firmware allows), then auto-negotiation.
- Compare against known-good W5500 board using same cable/switch.

## 8) Notes on Your Current Topology

- Your core pair mapping and resistor values look reasonable for W5500 + 10/100 magnetics.
- Do not worry about continuity within a winding.
- Do be concerned only if continuity crosses isolation barrier.

---

Revision date: 2026-04-11
