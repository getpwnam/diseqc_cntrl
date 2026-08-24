# Power Rail Measurements - 2026-08-24

## 12 V Rail

Rise time: 6.2 ms

SCPI automatic 10-90% rise time: 5.70 ms
Top: 12.0 V
Maximum: 12.1 V
Startup shape: monotonic; no visible ringing, droop, or restart

### Steady-State Ripple (Unloaded/Bench Bring-Up State)

Probe: CH2, 10X, AC coupled at C16 pin 1 with a very short nearby GND connection
Scope: 20 MHz bandwidth limit, 20 mV/div

- DC verification before AC coupling: 12.0 V mean and RMS, 12.1 V top and maximum
- 1 us/div after AC-coupling settling: 32.9 mVpp to 56.5 mVpp; approximately 2.0 mVrms to 7.5 mVrms
- 100 us/div: 94.6 mVpp to 101.7 mVpp; approximately 9 mVrms
- Wide capture shows low-load burst/PFM packets at approximately 65 kHz repetition
- Worst displayed envelope is approximately 0.85% of the 12 V rail
- No oscillatory runaway, rail collapse, or slower instability was observed

## 3.3 V Rail

Rise time: 2.96 ms

SCPI automatic 10-90% rise time: 3.28 ms
Top: 3.30 V
Maximum: 3.35 V
Startup shape: monotonic; no visible ringing, droop, or restart

### Steady-State Ripple (Unloaded/Bench Bring-Up State)

Probe: CH2, 10X, AC coupled across C12 with a very short nearby GND connection
Scope: 20 MHz bandwidth limit, 20 mV/div

- 1 us/div: 39.5 mVpp to 59.8 mVpp over eight acquisitions; 5.3 mVrms to 5.9 mVrms
- 100 us/div: 56.9 mVpp, 5.32 mVrms, maximum +30.6 mV, minimum -37.5 mV
- One acquisition measured the expected switching frequency at 399 kHz
- Waveform shows low-load burst/PFM packets; automatic MHz frequency readings are false edge counts on switching spikes
- No slower ripple excursion or instability was observed at 100 us/div

## Setup Observation

With ST-Link/SWD and USB-UART attached while VIN was off, the 3.3 V rail was pre-biased to about 0.95 V. With both cables disconnected, the repeat startup capture began at 6.25 mV. Treat powered debug/USB connections as back-power paths during rail testing.

## Remaining

- Repeat both ripple measurements under representative and worst-case rail loads.
- Measure rail response to load steps, including undershoot, overshoot, settling time, and cross-rail coupling.
