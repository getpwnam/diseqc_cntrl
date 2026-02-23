# Software Domain

Software is intentionally split into two layers:

1. `nanoFramework/`
   - Project-owned code and build orchestration.
   - Contains managed code (`DiseqC/`), native integration (`nf-native/`), and Docker/CMake build scripts.

2. `nf-interpreter` (external)
   - Upstream nanoFramework/ChibiOS firmware base.
   - Pulled during build; not maintained as a top-level source directory in this repository.

## Documentation Ownership

- Keep runtime/build/user docs under `software/nanoFramework/docs/`.
- Keep hardware docs under `hardware/`.
- Keep root-level docs limited to repository overview and cross-domain navigation.
