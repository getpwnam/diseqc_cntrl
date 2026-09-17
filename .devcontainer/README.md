# Cubley development container

The container provides the host tools used to build, debug, and deploy the
CUBLEY_F407_0_5 firmware and managed application.

## Pinned toolchain

| Component | Version |
| --- | --- |
| .NET SDK | 10.0.401 |
| nanoff | 2.5.163 |
| CMake | 3.31.12 |
| Arm GNU toolchain | 15.2.rel1 |
| Python | 3.14.7 |
| pyserial | 3.5 |
| Kconfiglib | 14.1.0 |
| hex2dfu release asset | 3.1.3 |

The Ubuntu and Python images are digest-pinned. ChibiOS and ChibiOS-Contrib are
fetched at exact commits. `global.json` keeps command-line and editor builds on
the same .NET SDK as the container.

## Rebuild and verify

Use **Dev Containers: Rebuild Container** in VS Code after changing any pin.
Docker can also validate the image from the repository root:

```bash
docker build --file .devcontainer/Dockerfile \
  --tag cubley-devcontainer:local .devcontainer
docker run --rm cubley-devcontainer:local bash -lc \
  'dotnet --version && nanoff --version && cmake --version && arm-none-eabi-gcc --version && python --version'
```

Run the host-side checks after rebuilding:

```bash
dotnet test software/nanoFramework/tests/DiSEqC_Control.Tests/DiSEqC_Control.Tests.csproj \
  --configuration Release -p:RestoreLockedMode=true
cd software/nanoFramework
./toolchain/build-CubleyControl.sh build \
  --project CubleyControl/CubleyControl.nfproj --configuration Debug
```

With the board connected on the wire-protocol UART, validate nanoff before a
deployment:

```bash
nanoff --listports
nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --listdevices
nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --devicedetails
```

Use the repository deployment script for deployment rather than constructing a
separate nanoff command:

```bash
software/nanoFramework/toolchain/deploy-CubleyControl.sh --reset
```

## Updating pins

1. Update the version and checksum or digest together in `Dockerfile`.
2. Keep `DOTNET_SDK_VERSION` and the root `global.json` version identical.
3. Keep `NANOFF_VERSION` on a stable release compatible with that .NET SDK.
   Do not use an unversioned `dotnet tool update`: preview packages can be
   selected from the feed.
4. Regenerate the test lock file with `dotnet restore --use-lock-file` and run
   the locked test command above.
5. Rebuild the image, run the managed build, then perform the three UART checks
   on hardware.

Apt repositories, the GitHub CLI apt package, and VS Code Marketplace
extensions remain floating. Base image digests and downloaded artifacts should
be refreshed deliberately so security updates are not silently deferred.