# Quick Start Guide

## Build & Flash Steps

1. Build firmware: `cd nf-native && make`
2. Flash: `st-flash write nanoCLR.bin 0x08000000`
3. Test: `mosquitto_pub -t diseqc/command/halt -m ''`

See [TESTING_GUIDE.md](docs/guides/TESTING_GUIDE.md) for complete testing procedures.
