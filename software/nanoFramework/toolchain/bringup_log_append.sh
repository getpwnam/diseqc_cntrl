#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
LOG_FILE="${REPO_ROOT}/docs/BRINGUP_TEST_LOG.md"

RESULT=""
CONCLUSION=""
COMMANDS=""
ARTIFACT=""
BREAKPOINTS=""
NOTE=""

usage() {
  cat <<'EOF'
Usage:
  ./toolchain/bringup_log_append.sh \
    --result PASS|FAIL|INFO \
    --conclusion "one-line conclusion" \
    [--commands "command(s) run"] \
    [--artifact "artifact used"] \
    [--breakpoints "bp list"] \
    [--note "extra note"] \
    [--logfile /path/to/BRINGUP_TEST_LOG.md]

Example:
  ./toolchain/bringup_log_append.sh \
    --result FAIL \
    --commands "gdb: b *0x0802664; b *0x080267a; b *0x08035b0c" \
    --artifact "tests/BlinkBringup/bin/Release/BlinkBringup.nfmrk2.bin" \
    --breakpoints "0x0802664, 0x080267a, 0x08035b0c" \
    --conclusion "Booter handoff reached; CLR reset not hit"
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --result)
      RESULT="${2:-}"
      shift 2
      ;;
    --conclusion)
      CONCLUSION="${2:-}"
      shift 2
      ;;
    --commands)
      COMMANDS="${2:-}"
      shift 2
      ;;
    --artifact)
      ARTIFACT="${2:-}"
      shift 2
      ;;
    --breakpoints)
      BREAKPOINTS="${2:-}"
      shift 2
      ;;
    --note)
      NOTE="${2:-}"
      shift 2
      ;;
    --logfile)
      LOG_FILE="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$RESULT" || -z "$CONCLUSION" ]]; then
  echo "Error: --result and --conclusion are required." >&2
  usage
  exit 2
fi

if [[ "$RESULT" != "PASS" && "$RESULT" != "FAIL" && "$RESULT" != "INFO" ]]; then
  echo "Error: --result must be PASS, FAIL, or INFO." >&2
  exit 2
fi

if [[ ! -f "$LOG_FILE" ]]; then
  echo "Error: log file not found: $LOG_FILE" >&2
  exit 1
fi

TIMESTAMP="$(date -u +"%Y-%m-%d %H:%M:%S UTC")"
GIT_REV="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo "unknown")"

{
  echo
  echo "### ${TIMESTAMP} [${RESULT}]"
  echo "- Git rev: ${GIT_REV}"
  if [[ -n "$COMMANDS" ]]; then
    echo "- Command(s): ${COMMANDS}"
  fi
  if [[ -n "$ARTIFACT" ]]; then
    echo "- Artifact: ${ARTIFACT}"
  fi
  if [[ -n "$BREAKPOINTS" ]]; then
    echo "- Breakpoints: ${BREAKPOINTS}"
  fi
  echo "- Conclusion: ${CONCLUSION}"
  if [[ -n "$NOTE" ]]; then
    echo "- Note: ${NOTE}"
  fi
} >> "$LOG_FILE"

echo "Appended test entry to ${LOG_FILE}"
