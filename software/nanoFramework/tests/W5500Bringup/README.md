# W5500Bringup

Dedicated W5500 + SPI + LED bring-up app for the Cubley controller.

This test app intentionally keeps diagnostics out of the production app startup path.

## What It Tests

1. Native W5500 network configuration (local IP/subnet/gateway/MAC).
2. Native socket open on W5500 transport.
3. Native TCP connect to probe endpoint.
4. Native send over SPI-driven socket path.
5. Native receive (or timeout warning) to confirm receive path behavior.
6. Native socket close.

## LED Result Codes (PA2)

The app drives the status LED on PA2 to show test state/result without UART.

1. Startup marker: three quick pulses after managed app launch.
2. Stage markers: N short pulses before each step (N = stage number).
3. PASS latched: one long pulse + two short pulses, then heartbeat.
4. WARN latched: one medium pulse + one short pulse (receive timeout only).
5. FAIL latched: one long separator pulse, then N slow pulses, where N is failure code.
6. Exception detail: when failure code is 8, a second burst of short pulses follows to indicate exception stage.

Failure code mapping:

1. ConfigureNetwork failed.
2. Open socket failed.
3. Connect failed.
4. IsConnected returned false.
5. Send failed or zero bytes sent.
6. Receive failed (non-timeout error).
7. Close failed.
8. Unexpected exception.

Exception stage mapping (only when failure code is 8):

1. During BringupStatus interop smoke call (`BringupStatus.NativeGet`).
2. During Open.
3. During ConfigureNetwork.
4. During Connect.
5. During IsConnected check.
6. During Send.
7. During Receive.
8. During Close.

## SWD Mailbox Status

The app writes a packed status word into native memory for off-site probing over SWD.

Format: `0xMMSSRRDD`

- `MM` magic byte: `0xD5`
- `SS` stage byte
- `RR` result byte
- `DD` detail byte

Result byte values:

- `0`: running
- `1`: pass
- `2`: warn
- `14`: fail
- `15`: exception

Read it from host:

```bash
cd software/nanoFramework/tests
chmod +x swd_read_bringup_status.sh
./swd_read_bringup_status.sh
```

## Pin/Peripheral Assumptions

Board firmware already configures these W5500 paths:

1. SPI1 enabled with DMA (`STM32_SPI_USE_SPI1 TRUE`).
2. SPI pins in AF5 mode: PA5 (SCK), PA6 (MISO), PA7 (MOSI).
3. CS pin output: PB12.
4. RESET pin output: PC6.
5. INT pin input: PC7.

## Probe Target

Defaults in `Program.cs`:

- Local IP: `192.168.1.160`
- Gateway: `192.168.1.1`
- Probe host: `192.168.1.60`
- Probe port: `1883`

Adjust these constants to match your bench network before deployment.

## Build

From `software/nanoFramework`:

```bash
./toolchain/compile-w5500-test.sh
```

## Deploy

Deploy resulting `tests/W5500Bringup/bin/Release/W5500Bringup.bin` with `nanoff` to managed region `0x080C0000` after flashing `nanoBooter.bin` and `nanoCLR.bin`.
