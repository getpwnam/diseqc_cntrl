#!/usr/bin/env python3
"""Small command-line client for the CubleyControl Device API v2."""

from __future__ import annotations

import argparse
import json
import os
import ssl
import sys
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen
from uuid import uuid4

DEFAULT_URL = "https://172.17.129.54"
DEFAULT_TIMEOUT_SECONDS = 5.0


def build_ssl_context(base_url: str, ca_file: str | None, insecure: bool) -> ssl.SSLContext | None:
    parsed = urlsplit(base_url)
    if parsed.scheme not in ("http", "https") or not parsed.netloc:
        raise ValueError("URL must be an http:// or https:// device origin")
    if parsed.path not in ("", "/") or parsed.query or parsed.fragment:
        raise ValueError("URL must not include a path, query, or fragment")
    if parsed.username is not None or parsed.password is not None:
        raise ValueError("URL must not contain credentials")
    if parsed.scheme == "http":
        if ca_file or insecure:
            raise ValueError("--ca-file and --insecure require an https:// URL")
        return None
    if ca_file and insecure:
        raise ValueError("--ca-file and --insecure cannot be used together")

    context = ssl.create_default_context(cafile=ca_file)
    context.minimum_version = ssl.TLSVersion.TLSv1_3
    if insecure:
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE
    return context


def request(
    base_url: str,
    path: str,
    token: str | None,
    ssl_context: ssl.SSLContext | None,
    timeout: float,
    payload: dict[str, Any] | None = None,
) -> Any:
    body = None
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if payload is not None:
        body = json.dumps(payload, separators=(",", ":")).encode("ascii")
        headers["Content-Type"] = "application/json"

    req = Request(
        base_url.rstrip("/") + path,
        data=body,
        headers=headers,
        method="POST" if body is not None else "GET",
    )
    try:
        with urlopen(req, timeout=timeout, context=ssl_context) as response:
            return json.load(response)
    except HTTPError as exc:
        try:
            error = json.load(exc)
        except (json.JSONDecodeError, UnicodeDecodeError):
            error = exc.reason
        raise RuntimeError(f"HTTP {exc.code}: {error}") from exc
    except ssl.SSLCertVerificationError as exc:
        raise RuntimeError(
            f"TLS certificate verification failed: {exc.verify_message}; "
            "use --ca-file with the device certificate"
        ) from exc
    except ssl.SSLError as exc:
        raise RuntimeError(f"TLS failure: {exc}") from exc
    except URLError as exc:
        if isinstance(exc.reason, ssl.SSLCertVerificationError):
            raise RuntimeError(
                f"TLS certificate verification failed: {exc.reason.verify_message}; "
                "use --ca-file with the device certificate"
            ) from exc
        if isinstance(exc.reason, ssl.SSLError):
            raise RuntimeError(f"TLS failure: {exc.reason}") from exc
        raise RuntimeError(f"request failed: {exc.reason}") from exc
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(str(exc)) from exc


def parse_value(value: str) -> Any:
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError:
        return value
    return value if isinstance(parsed, float) else parsed


def parse_fields(items: list[str]) -> dict[str, Any]:
    fields: dict[str, Any] = {}
    for item in items:
        key, separator, value = item.partition("=")
        if not separator or not key:
            raise ValueError(f"expected key=value, got {item!r}")
        fields[key] = parse_value(value)
    return fields


def build_parser() -> argparse.ArgumentParser:
    prog = os.path.basename(sys.argv[0])
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=f"""common commands:
  {prog} health
  {prog} lnb
  {prog} positioner
  {prog} job 1
  {prog} command lnb.enable channel=a
  {prog} command lnb.polarization channel=a value=horizontal
  {prog} command lnb.band channel=a value=low
  {prog} command positioner.goto position=12
  {prog} command positioner.goto_angle direction=east angle=34.5 --id bench-goto-001
  {prog} command positioner.step direction=west count=1
  {prog} command positioner.halt
  {prog} command positioner.complete job=1 verification=rf_verified

connection examples:
  CUBLEY_TOKEN=<token> {prog} --ca-file cubley.crt health
  {prog} --insecure health  # isolated bench diagnosis only
  {prog} --url http://172.17.129.54 health  # legacy firmware only
""",
    )
    parser.add_argument(
        "--url",
        default=os.environ.get("CUBLEY_URL", DEFAULT_URL),
        help="device origin (default: CUBLEY_URL or %(default)s)",
    )
    parser.add_argument(
        "--token",
        default=os.environ.get("CUBLEY_TOKEN"),
        help="bearer token (default: CUBLEY_TOKEN; environment use is recommended)",
    )
    tls_group = parser.add_mutually_exclusive_group()
    tls_group.add_argument(
        "--ca-file",
        default=os.environ.get("CUBLEY_CA_FILE"),
        help="trusted device CA or certificate PEM (default: CUBLEY_CA_FILE)",
    )
    tls_group.add_argument(
        "--insecure",
        action="store_true",
        help="disable server certificate validation for isolated bench diagnosis",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=DEFAULT_TIMEOUT_SECONDS,
        help="connection and response timeout in seconds (default: %(default)s)",
    )
    subparsers = parser.add_subparsers(dest="action", required=True)
    subparsers.add_parser("health", help="show liveness and firmware version")
    subparsers.add_parser("lnb", help="show LNB state and faults")
    subparsers.add_parser("positioner", help="show motor and position state")

    job = subparsers.add_parser("job", help="show a retained positioner job")
    job.add_argument("job_id", type=int)

    command = subparsers.add_parser("command", help="send an API operation")
    command.add_argument("operation", help="for example lnb.enable or positioner.halt")
    command.add_argument("fields", nargs="*", metavar="key=value")
    command.add_argument("--id", dest="command_id", help="idempotency ID (default: random)")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.timeout <= 0:
        print("cubleyctl: --timeout must be greater than zero", file=sys.stderr)
        return 1

    paths = {
        "health": "/api/v2/health",
        "lnb": "/api/v2/state/lnb",
        "positioner": "/api/v2/state/positioner",
    }
    try:
        ssl_context = build_ssl_context(args.url, args.ca_file, args.insecure)
        if args.insecure:
            print(
                "cubleyctl: warning: TLS certificate verification is disabled",
                file=sys.stderr,
            )

        if args.action in paths:
            result = request(
                args.url,
                paths[args.action],
                args.token,
                ssl_context,
                args.timeout,
            )
        elif args.action == "job":
            result = request(
                args.url,
                f"/api/v2/jobs/{args.job_id}",
                args.token,
                ssl_context,
                args.timeout,
            )
        else:
            payload = {
                "v": 2,
                "id": args.command_id or uuid4().hex,
                "op": args.operation,
                **parse_fields(args.fields),
            }
            result = request(
                args.url,
                "/api/v2/commands",
                args.token,
                ssl_context,
                args.timeout,
                payload,
            )
    except (RuntimeError, ValueError) as exc:
        print(f"cubleyctl: {exc}", file=sys.stderr)
        return 1

    json.dump(result, sys.stdout, indent=2)
    print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
