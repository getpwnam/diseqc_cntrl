#!/usr/bin/env bash
# firmware/bootstrap.sh
#
# Wires the Cubley target definition (checked in at
# firmware/targets-local/CUBLEY_F407_0_5) into nf-interpreter's expected
# board-search path
# (firmware/nf-interpreter/targets-community/ChibiOS/CUBLEY_F407_0_5).
#
# Design:
#   * Our targets live under `firmware/targets-local/` as plain directories in
#     this repo. No fork of nanoframework/nf-Community-Targets is required.
#   * nf-interpreter is pinned as an outer submodule; its own nested
#     `targets-community` submodule stays deinit'd.
#   * We create a relative symlink so the mirror always reflects the outer
#     repo's working tree without any copying.
#   * The link is inside a submodule's working tree, so it is untracked from
#     git's point of view — nothing to commit, nothing to keep in sync.
#
# Run this after cloning diseqc_cntrl (with --recursive) and before running
# cmake. The script is idempotent: repeated runs simply replace the symlink.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"          # firmware/
TARGET_NAME="CUBLEY_F407_0_5"
TARGET_SRC="${SCRIPT_DIR}/targets-local/${TARGET_NAME}"
LINK_DIR="${SCRIPT_DIR}/nf-interpreter/targets-community/ChibiOS"
LINK_PATH="${LINK_DIR}/${TARGET_NAME}"

if [[ ! -d "${TARGET_SRC}" ]]; then
    echo "ERROR: target source not found at ${TARGET_SRC}" >&2
    exit 1
fi

if [[ ! -e "${SCRIPT_DIR}/nf-interpreter/.git" ]]; then
    REPO_ROOT="${SCRIPT_DIR}/.."
    echo "INFO: nf-interpreter submodule not initialized; attempting auto-init..."
    if ! git -C "${REPO_ROOT}" submodule update --init firmware/nf-interpreter; then
        echo "ERROR: failed to initialize nf-interpreter submodule at ${SCRIPT_DIR}/nf-interpreter" >&2
        echo "Try manually: git -C ${REPO_ROOT} submodule update --init firmware/nf-interpreter" >&2
        exit 1
    fi
fi

mkdir -p "${LINK_DIR}"

# Remove any prior symlink or leftover directory at the link path.
if [[ -L "${LINK_PATH}" || -e "${LINK_PATH}" ]]; then
    rm -rf "${LINK_PATH}"
fi

# `ln -sr` (GNU coreutils) creates a symlink whose target is expressed
# relative to the link's location, so the workspace stays portable.
ln -sr "${TARGET_SRC}" "${LINK_PATH}"

echo "OK: linked ${LINK_PATH#${SCRIPT_DIR}/../} -> $(readlink "${LINK_PATH}")"

# ── Generate developer-local config files nf-interpreter expects ────────────
# nf-interpreter ships these as `.TEMPLATE` files under config/. They are
# .gitignored inside the submodule (per-developer local overrides). Regenerate
# them idempotently so `cmake --preset` can load the preset chain.
NFI_DIR="${SCRIPT_DIR}/nf-interpreter"

# 1. config/user-tools-repos.json — minimal preset. Our devcontainer relies on
#    nf-interpreter's built-in FetchContent for source dependencies, so we
#    supply an empty (hidden) preset with the required inherit name.
cat > "${NFI_DIR}/config/user-tools-repos.json" <<'JSON'
{
    "version": 4,
    "configurePresets": [
        {
            "name": "user-tools-repos",
            "description": "Cubley devcontainer: rely on FetchContent for source deps.",
            "hidden": true
        }
    ]
}
JSON

# 2. config/user-prefs.json — copy TEMPLATE verbatim (its preset is already
#    named `user-prefs`).
cp "${NFI_DIR}/config/user-prefs.TEMPLATE.json" \
   "${NFI_DIR}/config/user-prefs.json"

# 3. config/user-kconfig.conf — copy TEMPLATE verbatim (defaults are fine for
#    Phase 0 bringup; edit later to override symbols locally).
cp "${NFI_DIR}/config/user-kconfig.conf.TEMPLATE" \
   "${NFI_DIR}/config/user-kconfig.conf"

# 4. targets-community/CMakePresets.json — nf-interpreter's root
#    CMakePresets.json unconditionally includes this file. Emit a minimal
#    aggregator that pulls in our (symlinked) board preset.
cat > "${LINK_DIR}/../CMakePresets.json" <<'JSON'
{
    "version": 4,
    "include": [
        "ChibiOS/CUBLEY_F407_0_5/CMakePresets.json"
    ]
}
JSON

echo "OK: wrote nf-interpreter developer-local config (user-tools-repos.json, user-prefs.json, user-kconfig.conf, targets-community/CMakePresets.json)"
