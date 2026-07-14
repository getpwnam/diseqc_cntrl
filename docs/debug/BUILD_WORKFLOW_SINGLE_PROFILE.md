# Build Workflow: Single Profile

This project now enforces an explicit single-profile workflow to avoid confusion about active source paths.

## Recommended Command

Run:

```bash
bash software/nanoFramework/toolchain/build-native.sh build
```

This is now the canonical native build command.

## Active Source Map

Every native build now emits a source map block and writes:

- `software/nanoFramework/build/ACTIVE_SOURCE_MAP.txt`

This file is the build-time source of truth for:
- active overrides directory
- active `board.c`
- active `target_common.c`
- active `nanoCLR/main.c`
- active `nanoBooter/main.c`

If there is ever doubt about which startup file is in use, check this file first.

## Direct Invocation

`build-native.sh` now rejects any profile other than `cubley-base` by design.

```bash
bash software/nanoFramework/toolchain/build-native.sh build --profile cubley-base
```

If another profile is requested, the build exits with an error.

## Static Target Files

`build-native.sh` now consumes static target assets from `software/nanoFramework/nf-native/target-overrides` and fails fast if required files are missing.

This removes dynamic per-build generation for:

- `defconfig`
- `common/CMakeLists.txt`
- `nanoCLR/CMakeLists.txt`
- `nanoBooter/CMakeLists.txt`

The static-file model keeps active source ownership explicit and reviewable in git.
