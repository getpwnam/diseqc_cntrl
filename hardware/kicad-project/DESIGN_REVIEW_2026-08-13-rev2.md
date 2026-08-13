# DiSEqC Controller Design Review, Revision 2

**Project:** `diseqc_cntrl` (KiCad 10 source, five hierarchical sheets, four-layer PCB)
**Review date:** 2026-08-13
**Review basis:** current unsuffixed KiCad source and current `production/` outputs; previous review conclusions were not used as evidence
**Verdict:** **Not ready for fabrication or assembly release.** No source-backed critical electrical blocker remains: STM32 `VREF+`, the D6 RClamp0502BA low-capacitance pin mode and direct-plane return, Ethernet MDI direction/polarity, RX orphan cleanup, and `REF_CLK` routing/transition stitching have been corrected. Release remains blocked by unresolved controlled impedance, unresolved stock, remaining open-via process requirements, incomplete finish/process metadata, and regeneration/validation of production outputs.

## Critical Findings

| Severity | Issue | Evidence and required action |
|---|---|---|
| High | Controlled impedances do not match interface targets | Raw-file and analytical verification using the declared 0.10 mm, εr=4.5 outer-layer dielectric and 35 um copper gives 45.6 Ω single-ended / approximately 84.9 Ω differential for the 0.12 mm / 0.20 mm-gap Ethernet MDI geometry, versus 100 Ω differential. Current 0.20 mm traces calculate to 36.2 Ω single-ended, affecting nominal 90 Ω USB, 50 Ω RMII, and 75 Ω IF routing. Obtain the fabricator stackup and field-solved geometries, then reroute and constrain each interface. |
| High | Assembly supply is incomplete | InvenTree matched 62 of 64 identity groups, but immediate unallocated stock is below one-board demand for IC1/IC2, IC4, D3–D6, L1–L3, multiple regulator capacitors/resistors, and one of two USB-C connectors. The production BOM has no manufacturer/supplier ordering fields. Resolve stock or purchase coverage and regenerate a traceable assembly BOM. |

## Previous Review Delta

This review was performed from source, production files, local PDFs, and fresh analyzer output before consulting prior reports.

| Status | Item |
|---|---|
| Resolved during review follow-up | IC3 pin 21 `VREF+` and PCB pad 21 now connect to the same `+3.3VA` rail as VDDA. The rail is fed through FB1 and decoupled by C18 (1 uF) and C21 (100 nF), consistent with STM32F407 Figure 52. |
| Resolved during review follow-up | D6 uses the RClamp0502BA low-capacitance one-line mode: pin 2=`IF_OUT`, pin 1=`GND`, and pin 3 open in both schematic and PCB. D6 is adjacent to the J7 launch; pin 2 has a 0.113 mm signal tap, and pin 1 reaches an off-pad 0.8/0.35 mm GND via through 1.10 mm of 0.20 mm F.Cu. The direct-plane return and via-in-pad warnings are resolved. |
| Resolved during review follow-up | All four exposed-pad thermal vias under each of IC1 and IC2 are explicitly bottom-tented (`front no`, `back yes`). Fresh analysis retains the thermal-via adequacy information and no longer reports solder-wicking warnings for these arrays. |
| Resolved during review follow-up | LAN8742 MDI direction and polarity now map directly: `TXP→TD+`, `TXN→TD-`, `RXP→RD+`, and `RXN→RD-`. Auto-MDIX/polarity correction is no longer required to compensate for PCB mapping. |
| Resolved during review follow-up | The isolated 0.508 mm RXN and 0.120 mm RXP B.Cu remnants are removed. Both nets are now F.Cu-only, have no signal vias, form one connected island each, and have no disconnected pads. |
| Resolved during review follow-up | GND stitching vias at both `REF_CLK` transitions now measure 0.950 mm center-to-center from their signal vias, with 0.150 mm copper-edge clearance. Fresh EMC analysis no longer emits `RP-001`. |
| Resolved during review follow-up | TP11, TP12, and TP16–TP24 were removed with their branch copper. This eliminates the IF_OUT, REF_CLK, MDC, MDIO, TX_EN, CRS_DV, and RMII data test branches; no segment terminates at a deleted pad coordinate. `REF_CLK` copper is now 16.344 mm and IF_OUT copper is 24.866 mm. |
| Still open | Controlled-impedance geometry, stock coverage, lifecycle uncertainty, CAM inspection, and physical electrical/thermal validation. |
| Confirmed resolved | L1/L2/L3 now have specific identities; regulator dividers resolve correctly with the manufacturer 1.000 V reference; current production Gerbers and drills are complete. |

## Design Overview

The board contains two LMR36520 bucks, an STM32F407VGT6, LNBH26PQR LNB/DiSEqC supply and IF extraction, LAN8742A RMII Ethernet with a J0011D21BNL integrated-magjack, native USB, FT230XS USB-UART, FM24CL16B FRAM, and SWD. Input protection includes a resettable fuse, reverse-polarity diode, and 24 V TVS. The PCB is 90 mm × 100 mm, 1.6 mm thick, with F.Cu / 0.10 mm prepreg / In1 GND / 1.24 mm core / In2 power / 0.10 mm prepreg / B.Cu.

## Component Summary

| Item | Result |
|---|---:|
| Schematic components | 157 total, 61 unique populated values, 3 intentional DNP |
| PCB footprints | 148, all placed on F.Cu; 121 SMD and 5 THT classified |
| Nets | 178 schematic; 170 PCB |
| Routing | 1,195 segments, 165 vias, zero reported unrouted nets |
| Zones | 7 filled zones; In1 GND is one 97%-filled region |
| Production BOM / placement | 121 populated BOM references; all 121 are positioned; placement also includes test, debug, net-tie, and fiducial references |
| Gerbers | 13 Gerber files plus PTH and NPTH drills; four copper layers, masks, pastes, silks, and edge cuts present |
| Drill output | 234 holes; X2-based classification and layer extents align |

The three cross-analysis “missing PCB” parts are expected: C9 and C10 are optional feedback capacitors marked DNP with no footprints, and R10 is DNP. This is not a synchronization defect.

## Power Tree and Regulator Review

```text
J1 VIN_RAW
  -> F1 resettable fuse -> VIN_FUSED
  -> D1 reverse-polarity protection + D2 24 V TVS -> VIN_PROTECTED
       -> IC1 LMR36520 + L1 33 uH -> +12V
            -> IC6 LNBH26 boost/linear channel -> 13/18 V LNB_OUT + IF extraction
       -> IC2 LMR36520 + L2 6.8 uH -> +3V3
            -> FB1 -> +3.3VA
            -> NT1 -> +3.3VP -> FB2 -> Ethernet 3V3_BEADED
USB J6 VBUS -> FB3 -> FT230_VCC
```

- **IC1:** R1=100 kΩ and R2=9.09 kΩ. With the LMR36520 1.000 V typical FB reference, $V_{OUT}=1.000(1+100/9.09)=12.00\text{ V}$. The analyzer's 7.20 V result used an incorrect heuristic 0.6 V reference and is rejected.
- **IC2:** R3=100 kΩ and R4=43.2 kΩ give $V_{OUT}=3.315\text{ V}$ nominal. The schematic analyzer missed this divider because of topology parsing.
- **Frequency:** LMR36520 is a fixed 400 kHz part per the manufacturer datasheet, not the analyzer's 500 kHz topology default.
- **Capacitance/inductance:** IC2's 6.8 uH and three 22 uF output capacitors agree with the datasheet's 3.3 V quick-start values. IC1's 33 uH and 4×10 uF plus bulk capacitance are plausible for 12 V but require load-transient verification at the actual VIN and load range.
- **LNBH26:** Pin numbering, I2C logic, 10 uH boost inductor, BYP capacitor, output bulk capacitor, and 27 nH/220 pF IF-isolation network are coherent with the local ST datasheet. The analyzer's 12 V/3.3 V I2C domain errors are false positives: SDA/SCL are logic pins rated independently of the IC's power output.

The actual maximum input voltage, rail loads, LNB current profile, and allowed ripple are not specified in the design files. A signed power budget remains required before release.

## PDN Impedance

The analytical model reports minima of 6.85 mΩ for +3V3 (66.6 uF, 1.58 MHz), 1.73 mΩ for +12V (140.2 uF, 1.58 MHz), 78.8 mΩ for +3.3VA (1.1 uF, 7.94 MHz), and 29.3 mΩ for Ethernet 3V3_BEADED (10.2 uF, 2.51 MHz). These are idealized lumped estimates using generic MLCC parasitics; mounting inductance, DC bias, ferrites, and plane spreading are not fully represented. Validate rail impedance/ripple at IC pins under load.

## Power Budget

The automated budget is incomplete and is not accepted quantitatively: it omits STM32 load, external loads, actual LNB output current, and USB operating states, and it carries the rejected 7.201 V IC1 estimate. It identifies the correct main consumers but only assigns 10 mA to LNBH26 and 100 mA to the PHY. Derive worst-case +12V and +3V3 budgets from firmware modes and external loads, then check converter, inductor, diode, trace, and connector margins.

## Power Sequencing

IC1 and IC2 EN pins are tied to `VIN_PROTECTED`, so +12V and +3V3 start together using each LMR36520's internal soft-start. Their open-drain PG pins are tied to GND and intentionally unused. No downstream rail-valid interlock exists; verify MCU brownout/reset behavior and LNB/PHY startup with simultaneous rails.

## Sleep Current Audit

The analyzer reports 1.42 mA always-on and 277.29 mA “realistic” sleep current, but 275.97 mA comes from treating conditional pull-ups as continuously asserted. That result is not credible without GPIO, PHY, FT230, and LNB states. Measure each supported firmware sleep mode; do not use the analyzer number for product power claims.

## Inrush Analysis

The lumped estimate is 1.01 A for 140.2 uF on +12V and 0.22 A for 66.6 uF on +3V3, assuming a 1 ms soft start. The +12V calculation also used the analyzer's incorrect 7.201 V value, so measure startup current and monotonicity at minimum/maximum VIN and with the intended LNB load.

## Analyzer Verification

### Datasheet and Pin Verification

Manufacturer PDFs in `.debug` were used directly. All analyzer-exposed complex components were compared from schematic pin number to PCB pad number: 278 of 284 normalized comparisons matched directly. The six residual names are intentional hierarchy aliases on the UART and duplicated USB-C VBUS pins; no physical pad mismatch was found.

| Device | Verification result |
|---|---|
| IC1/IC2 LMR36520ADDAR | Pins 1–9 and exposed-pad net mapping match TI Table 5-1. Feedback reference corrected to 1.000 V. |
| IC3 STM32F407VGT6 | LQFP100 pin numbering and RMII/USB/SWD alternate-function choices match the ST tables. Pin 21 `VREF+` and PCB pad 21 resolve to `+3.3VA`; C18 (1 uF) and C21 (100 nF) provide local reference decoupling. |
| IC4 FM24CL16B-GTR | SOIC-8 pin map, grounded WP, and I2C connections match the Infineon datasheet. |
| IC5 USBLC6-2P6 | Flow-through D+/D-, GND, and VBUS pin mapping matches the ST application circuit. The reported “no decoupling capacitor on IC5” is not a valid requirement for this protection array. |
| IC6 LNBH26PQR | QFN-25 pin map and exposed-pad ground match ST Table 2. Unused channel-B pins are treated consistently with the application. |
| IC7 LAN8742A-CZ-TR | VQFN-24 pin numbering, 12.1 kΩ RBIAS, 1 uF || 470 pF VDDCR bypass, supplies, crystal, and RMII pins match the Microchip datasheet. MDI direction/polarity mapping is now direct; impedance and return-path warnings remain as above. |
| U1 FT230XS-U | SSOP-16 pin map, TX/RX crossover, 3V3OUT/RESET/VCCIO network, and USB pins match FTDI documentation. |
| J9 J0011D21BNL | Exact Pulse J403 page-5 electrical schematic confirms PCB-side pins 1/2=`TD+/-`, 3/6=`RD+/-`, 4/5=center taps, 8=chassis. |
| D6 RClamp0502BA | The schematic and PCB map pin 2 to `IF_OUT`, pin 1 to GND, and pin 3 to an explicit no-connect, matching the datasheet's nominal 0.5 pF one-line mode. Pin 1 now connects directly to the GND planes through a coincident 0.8/0.35 mm via; electrically the return geometry is resolved. |
| Two-pin passives/mechanical parts | Pinout not meaningful; values and polarity were checked from topology and BOM where applicable. |

## Signal Analysis Review

No op-amp, bridge, battery, or isolated-power subcircuits are present. Detected regulator, divider, LC, protection, crystal, Ethernet, USB, I2C, UART, RMII, and SWD circuits were checked against raw connectivity. The principal accepted issues are controlled impedance, Ethernet/RMII return paths, and release-process constraints; the resolved IC3 `VREF+`, D6 electrical/assembly findings, IC1/IC2 bottom-tenting findings, and Ethernet MDI-mapping findings and rejected detector results are documented separately.

### Signal Integrity and Interfaces

### Differential pairs

| Pair | Lengths | Delta | Assessment |
|---|---:|---:|---|
| Ethernet TX | 18.042 / 15.131 mm | 2.911 mm | About 17 ps on FR-4; skew is modest, but nominal impedance is low. |
| Ethernet RX | 28.873 / 31.551 mm | 2.678 mm | About 16 ps; skew is modest. Both members are now entirely on F.Cu with no signal vias. |
| Native USB raw | 8.283 / 8.076 mm | 0.207 mm | Good matching. |
| Native USB protected | 31.535 / 32.405 mm | 0.870 mm | Good matching. |
| FT230 USB | 12.844 / 16.750 mm | 3.906 mm | Acceptable for USB full speed, but each line changes layer twice without adjacent return vias. |

### Controlled-Impedance Reverification

Fresh full PCB analysis was run after the reroute. Estimates use Wheeler/IPC-2141 single-ended microstrip equations and an edge-coupled microstrip approximation; they are geometry checks, not fabrication values. Solder mask, weave, copper roughness, actual cured dielectric thickness, and connector/package discontinuities require a 2D/3D field solve and coupon/TDR acceptance.

| Interface | Current geometry | Analytical estimate | Target and status |
|---|---|---:|---|
| Ethernet TX/RX MDI | F.Cu, 0.12 mm width, nominal 0.20 mm edge gap, 0.10 mm to In1 GND | 45.6 Ω single-ended; approximately 84.9 Ω differential on coupled runs | 100 Ω differential: **not met analytically**. Approximately 0.077 mm width at the same gap is a starting estimate only. |
| Native USB protected pair | F.Cu, 0.20 mm width, approximately 0.20 mm edge gap | 36.2 Ω single-ended; approximately 67.3 Ω differential | 90 Ω differential: **not met analytically**. Approximately 0.103 mm width at the same gap is a starting estimate only. |
| Native USB raw pair | F.Cu, 0.20 mm width, weak/variable coupling up to approximately 0.80 mm edge gap | 36.2 Ω single-ended; approximately 72.4 Ω differential when weakly coupled | 90 Ω differential: **not met analytically**; geometry is not uniform through the connector/ESD fanout. |
| FT230 USB pair | 0.20 mm width, mixed F.Cu/B.Cu, two transitions per net, not consistently coupled | 36.2 Ω single-ended; differential impedance is discontinuous and no more than approximately 72.4 Ω when uncoupled | 90 Ω differential: **not met analytically**. USB full-speed may tolerate this, but compliance is unverified. |
| RMII clock/data/control | 0.20 mm width, mixed outer layers on many nets | 36.2 Ω single-ended | Nominal 50 Ω routing: **not met analytically**. Approximately 0.095 mm width is a starting estimate; series resistors reduce source-reflection risk but do not set line impedance. |
| `IF_OUT` to J7 | 0.20 mm width, 24.866 mm total after TP11 removal, one transition, 87.5% sampled reference coverage | 36.2 Ω single-ended where referenced | 75 Ω: **not met analytically**. The approximately 0.023 mm calculated width is impractical, so change stackup/plane spacing and field-solve the route. |

Ethernet electrical mapping is correct and all four MDI pad-to-pad paths are connected entirely on F.Cu without signal vias. The former RX bottom-layer remnants and resulting RXP `RP-002` artifact are resolved. TP16–TP24 and their branch copper are removed. `REF_CLK` is now 16.344 mm total with 18/22 sampled points referenced; its four misses are at signal-via antipads rather than unsupported trace spans. Ethernet remains open for fabricator-approved 100 Ω MDI geometry and physical validation.

For a one-off non-compliance build, the impedance risk is lower than the formal release finding implies. The approximately 85 Ω Ethernet MDI pair has a first-order 1.18:1 VSWR against 100 Ω and is likely to link over ordinary cable; USB is full-speed and its short routes are likely to function; RMII routes are short, have 33 Ω source-series resistors, and no longer have test-point branches. The 950 MHz–2.15 GHz path through C40 is the material uncertainty: the approximately 36 Ω trace against a 75 Ω environment gives a first-order 2.07:1 VSWR and can introduce frequency-dependent loss/ripple. If only one impedance item is corrected before a hobby build, prioritize the LNB_OUT/IF_OUT path.

### External interfaces

- **J4 native USB-C:** 5.1 kΩ CC pulls and USBLC6 data protection are present. The closest GND via to IC5 is about 2.4 mm. VBUS power protection/inrush behavior should be checked against the intended host/device role.
- **J6 FT230 USB-C:** no dedicated external low-capacitance ESD array is present on D+/D-. Treat this as an EMC/robustness warning for a cable-facing port even if the IC's internal ratings are accepted.
- **J7 IF SMA:** D6 uses the correct nominal 0.5 pF connection and is adjacent to the launch. Pin 2's signal tap is 0.113 mm. Pin 1 reaches an off-pad 0.8/0.35 mm GND via at `(128.6, 131.0)` through 1.10 mm of 0.20 mm F.Cu. This resolves both the ESD-return inductance warning and the D6 solder-wicking warning.
- **J8 LNB SMA:** the 27 nH/220 pF feed/isolation network is present; validate insertion loss and DC-current behavior with the intended coax/load.
- **J9 Ethernet:** integrated 1:1 magnetics and chassis net are present. Earth is tied to GND by NT3; verify this hard tie is intentional for the enclosure and EMC strategy.

## USB Compliance

The analyzer records J4 data ESD as present and J6 as having no recognized external ESD array; it also flags VBUS decoupling on both ports. Its Type-C classifier incorrectly marks both USB4110-GF-A connectors as non-Type-C, so the aggregate pass/fail count is not accepted. Raw review confirms the CC terminations and J4 USBLC6 path. Resolve the J6 external-ESD decision and verify VBUS capacitance/inrush against the intended USB role.

## Bus Topology

Two I2C buses are present: STM32↔FRAM uses 2.7 kΩ pull-ups to +3V3, and STM32↔LNBH26 uses 4.7 kΩ pull-ups to +3V3. FT230 UART TX/RX is crossed correctly. RMII uses 33 Ω source-series resistors on data/control and clock, with 50 MHz `REF_CLK`. SWD/SWO is available at J5. No SPI or CAN bus is populated.

## SPICE Verification

`ngspice 44.2` was run on independently written passive models. Vendor switching models were not available, so regulator loop stability and transient response were not simulated.

| Circuit | Result |
|---|---|
| IC1 divider at 12 V | FB = 0.999908 V; consistent with 1.000 V regulation. |
| IC2 divider at 3.3 V | FB = 0.995531 V; consistent with a 3.315 V nominal setpoint. |
| LNB 27 nH / 200 nF / 220 pF network, ideal 75 Ω model | −0.012 dB at 22 kHz, approximately −88.0 dB at 950 MHz, and −109.3 dB at 2.15 GHz for the feed path. This supports DiSEqC pass-through and IF isolation in the ideal model. |
| D6 capacitance sensitivity, matched 75 Ω estimate | The corrected nominal 0.5 pF path gives approximately −0.27 dB at 2.15 GHz before package/layout parasitics; the former 1.2 pF path would have been approximately −1.37 dB. |

## PCB Layout Analysis

## EMC / Cross-Domain Analysis

### Ground, return paths, and EMC

The raw EMC score is 0/100 from 111 findings, but it is not a calibrated board score. The analyzer incorrectly classified every adjacent copper layer as a signal layer and fragmented thermal-relief/connector pads despite the 97%-filled one-region In1 GND zone and zero-unrouted result. The broad `SU-001`, `GP-001`, and plane-island counts are therefore not accepted as blockers.

Retained EMC risks:

1. FT230 D+/D- and SWD clock change layers without local return vias. USB full-speed timing is tolerant, but common-mode conversion can increase emissions.
2. The barrel input has surge/reverse protection but no dedicated conducted-EMI input filter. Test 400 kHz converter fundamentals/harmonics on the input cable under maximum load.
3. Ethernet MDI mapping, RX copper cleanup, and `REF_CLK` routing/stitching are corrected, but nominal MDI impedance is approximately 85 Ω rather than 100 Ω.
4. D6's pin mode is corrected; its narrow RF/ESD return still requires layout improvement or validation.
5. Cable-attached pre-compliance testing should cover 30 MHz–1 GHz with Ethernet, both USB ports, and representative LNB coax attached.

Positive geometry includes a near-solid In1 GND reference, 61 GND stitching vias, short crystal-local networks, complete routing, and local power islands on In2.

### Thermal

The automated thermal pass analyzed only IC1 at an assumed 13 mW and reported 25.8 °C. That is not a credible board-level worst case and is treated as a coverage gap.

- IC1 and IC2 each have four 0.35 mm drilled GND vias in exposed pad 9.
- IC6 has four 0.35 mm drilled GND vias in exposed pad 25. LNBH26 specifies the exposed pad connected to power ground and ground layer through vias; datasheet $R_{\theta JA}$ is 40 °C/W on a 2s2p board with thermal vias.
- IC7 has a 3×3 exposed-pad via pattern using footprint micro-holes; this is a good geometric pattern, subject to fabricator capability.
- The IC1/IC2/IC6 vias are open through-vias. Copper transfer is adequate, but solder wicking and voiding require filled/capped vias or a qualified stencil/paste process.

Perform thermal validation at maximum VIN, maximum +3V3 load, and maximum supported 13/18 V LNB current. Record IC1, IC2, IC6, L1–L3, diode, and connector temperatures after equilibrium.

## Gerber Verification

The extracted production ZIP contains all four copper layers, both masks, both paste layers, both silks, edge cuts, and separate PTH/NPTH drills. X2 extents align, edge cuts are closed at 90 mm × 100 mm, and 234 holes are classified. The four B.Paste flashes belong to J7/J8 and are an assembly-process choice, not missing data. Source-to-Gerber net-level equivalence cannot replace a regenerated post-fix CAM comparison.

## Manufacturing and DFM

- Production Gerbers are complete and aligned at 90 mm × 100 mm. PTH and NPTH drill files are present.
- Minimum trace is 0.12 mm, minimum drill 0.20 mm, and minimum annular ring 0.15 mm. The installed analyzer labels 0.12 mm as an advanced-process feature; obtain written fabricator acceptance.
- Four open vias occur in each LMR/LNB exposed pad. D2 and USB-C shell-pad vias also trigger solder-wicking warnings. Define via fill/cap/tent requirements explicitly in fabrication notes and quote options.
- B.Paste contains four deliberate flashes on J7/J8 bottom shell pads although all placed components are top-side. A top-only stencil will not deposit these apertures. Define whether the SMA shells are hand-soldered, selectively pasted, or require a bottom stencil/process.
- J1, J4, J6, J7, J8, and J9 courtyard overhangs are connector mechanics, not placement errors. C36, F1, SW1, TP1, and TP2 are also close to the edge; confirm panel rails, depanel clearance, and enclosure access.
- Project copper-to-edge and silk-clearance minima are 0.0 mm. Add explicit production constraints rather than relying on the fabricator's defaults.
- Surface finish is recorded as `None` in the project stackup. Specify finish, copper weight, mask color, controlled-impedance stackup, acceptance tolerance, and via treatment on the order/drawing.
- Four DRC exclusions cover the mounting-hole “extra footprint” checks only. Footprint filter/type mismatch and missing-courtyard checks are ignored globally; no critical connectivity rule is disabled.

## Sourcing and Lifecycle

InvenTree enrichment found 62 matched identity groups, two conflicts, no not-found records, no API errors, and no populated component lacking an identity. Numeric IDs make the two conflicts traceable but they should be corrected:

- R20 schematic name `R_20k_0603` versus InvenTree `R_20k_0603_1%`.
- SW1 schematic name `W_PUSH_MOMENT_SMD` versus InvenTree `SW_PUSH_MOMENT_SMD`.

The lifecycle audit queried 19 schematic MPNs and returned `unknown` for all 19. InvenTree marks matched records active, but this is internal status rather than manufacturer lifecycle proof. Recheck manufacturer lifecycle and supplier availability before purchasing. Stock fields with zero or stale supplier availability are not proof of market unavailability, but they do block an immediate internal build.

## Test Coverage

There are 12 named test points covering input/protected power, +12V, +3V3, VBUS, reset/boot, LNB fault/DiSEqC, and both LNB I2C lines; SWD is exposed at J5. No RMII, REF_CLK, Ethernet MDI, USB, or IF_OUT test branches remain. Do not add direct probes to feedback/switch nodes or other high-speed nets unless a production or certification requirement justifies the discontinuity.

## Assembly Complexity

The schematic heuristic scores assembly at 35/100 with two QFN-class hard parts, one LQFP100, dense 0603 usage, and 41 unique footprints. Hand assembly may be feasible for prototypes, but repeatable production needs stencil control for QFN/power pads, a defined process for open thermal vias, and explicit J7/J8 bottom-shell soldering.

## BOM Optimization

The analyzer finds 13 resistor values, 14 capacitor values, and 16 single-use passive values. Do not consolidate regulator, crystal, Ethernet, USB, or LNB parts solely by nominal value; voltage rating, dielectric, tolerance, ESR, DCR, saturation/ripple current, Q, and package parasitics are functional requirements. Optimization is secondary to completing the traceable MPN BOM.

## False Positives and Reviewer Overrides

- `VM-001` SDA/SCL 12 V to 3.3 V crossings: rejected; LNBH26 I2C pins are logic-domain pins.
- `XV-001` C9/C10/R10 missing from PCB: rejected; all are intentional DNP.
- `SU-001` all copper layers are adjacent signal layers: rejected; In1 is the GND plane and In2 is power distribution.
- GND/+3V3/+3.3VA multi-island blocker counts: downgraded because the graph splits pads connected through thermal reliefs and connector structures; source reports one filled GND region and zero unrouted nets. Native KiCad DRC is still required after changes.
- Connector edge-overhang findings: accepted as intentional mechanics, pending panel/enclosure verification.
- Gerber `GR-004` bottom paste ratio: not a missing-paste defect; the four flashes belong to J7/J8. It remains an assembly-process requirement.
- IC5 missing decoupling: rejected; USBLC6's VBUS pin is a protection reference, not an IC supply requiring local bypass in the cited application.
- IC1/C3/L1 “large hot loop”: rejected as selected-component misassociation; C3 is not the local IC1 input-loop capacitor.

## Not Performed / Review Limits

- **Fresh native ERC/DRC:** not run because `kicad-cli` is unavailable. Prior manual results were not accepted as fresh evidence. Rerun KiCad ERC and DRC after the IC3 and D6 pin corrections and any D6 return-path change.
- **Vendor-model switching SPICE:** not performed; no LMR36520 or LNBH26 encrypted/vendor models were available. Passive and feedback simulations were performed with ngspice.
- **Full thermal solution:** not possible without actual rail loads, ambient/enclosure conditions, copper-emissivity assumptions, and package power. The installed thermal analyzer's one-device estimate is inadequate.
- **Field-solver impedance:** not performed. Analyzer estimates are geometry checks only; fabricator impedance modeling/coupons are required.
- **Lifecycle certainty:** network audit returned unknown for every direct MPN.
- **Lab validation:** no oscilloscope, VNA/TDR, load transient, Ethernet packet, USB compliance, ESD, thermal-camera, or EMC measurements were performed.
- **Deep structured extraction:** no `datasheets/extracted/` cache exists; critical checks used direct local manufacturer PDFs.

## Release Checklist

1. Obtain a controlled-impedance stackup and field-solved geometries for 100 Ω Ethernet, 90 Ω USB, 50 Ω RMII, and 75 Ω IF; update netclasses and reroute as required.
2. Rerun native KiCad ERC/DRC, refill zones, regenerate all production outputs, and compare checksums/CAM plots; include the corrected IC3, D6, and Ethernet mappings in the review.
3. Specify surface finish, copper weight, remaining via fill/cap/tent requirements, and J7/J8 bottom-paste assembly process; preserve IC1/IC2 bottom-only thermal-via tenting in regenerated mask outputs.
4. Close InvenTree conflicts and stock shortfalls; generate an MPN/manufacturer/supplier assembly BOM.
5. Validate both bucks and LNB supply at worst-case VIN/load, including startup, ripple, load transient, and temperature.
6. Run Ethernet link/packet tests with forced and autonegotiated modes, USB functional tests, ADC accuracy tests, IF insertion/return loss, and ESD/EMC pre-compliance.

## Final Verdict

**Not ready for fabrication or assembly release.** STM32 `VREF+`, D6 pin mode/direct-plane return/via placement, IC1/IC2 bottom thermal-via tenting, Ethernet MDI direction/polarity, RX orphan-copper cleanup, and RMII `REF_CLK` routing/stitching are corrected. The current MDI geometry still estimates near 85 Ω differential rather than 100 Ω; USB, RMII, and IF geometries also miss their nominal impedance targets analytically. Obtain fabricator-solved geometries, then repeat ERC/DRC, production generation, impedance/coupon review, and physical validation before ordering.

## Analyses Run

- `analyze_schematic.py` on the root hierarchical schematic
- `analyze_pcb.py --full --proximity` on the current PCB
- `cross_analysis.py` on fresh schematic and PCB JSON
- `analyze_gerbers.py --full` on the extracted production ZIP
- `analyze_thermal.py` (coverage limitation documented)
- `analyze_emc.py --market eu --spice-enhanced` (wrapper failed to detect installed ngspice; analytical output was triaged manually)
- `ngspice 44.2` independent divider and LNB filter simulations
- `analyze_schematic.py --lifecycle`
- InvenTree exact-identity enrichment
- Raw schematic/PCB pin-pad comparison, project-rule audit, production BOM/placement cross-check, and direct PDF verification