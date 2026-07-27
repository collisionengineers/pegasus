"""Shared CLI helper for skill validator scripts.

Provides load_json() and run_validator_main() so each validate_*.py only needs
to supply its domain logic — arg parsing, JSON loading, error/warning formatting,
--json output, and exit codes are handled here.

Usage pattern in a validator::

    from validate_cli import load_json, run_validator_main

    def my_validator(payload):
        errors, warnings = [], []
        # ... domain checks ...
        return errors, warnings

    if __name__ == "__main__":
        raise SystemExit(
            run_validator_main(
                validator=my_validator,
                description="Validate a my-skill payload",
                usage_suffix="<payload.json>",
            )
        )

See run_validator_main() for the full parameter reference.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Callable


def load_json(path: str | Path) -> Any:
    """Load and JSON-parse *path*; raise ValueError with a clear message on failure."""
    try:
        with Path(path).open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except FileNotFoundError as exc:
        raise ValueError(f"File not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ValueError(f"Invalid JSON in {path}: {exc}") from exc


def _emit_text(errors: list[str], warnings: list[str], ok_message: str) -> None:
    for warning in warnings:
        print(f"warning: {warning}", file=sys.stderr)
    for error in errors:
        print(f"error: {error}", file=sys.stderr)
    if not errors:
        print(ok_message)


def _emit_json(errors: list[str], warnings: list[str]) -> None:
    print(json.dumps({"ok": not errors, "errors": errors, "warnings": warnings}, indent=2))


def run_validator_main(
    *,
    validator: Callable[[Any], tuple[list[str], list[str]]],
    description: str,
    usage_suffix: str = "<payload.json>",
    ok_message: str = "OK",
    list_flag: str | None = None,
    list_callback: Callable[[], None] | None = None,
    argv: list[str] | None = None,
) -> int:
    """Parse CLI args, load the JSON input file, run *validator*, and report results.

    Parameters
    ----------
    validator:
        Callable that accepts the parsed JSON payload and returns
        ``(errors, warnings)`` — both lists of strings.
    description:
        Short description string shown in ``--help``.
    usage_suffix:
        Text appended after the script name in usage (default: ``<payload.json>``).
    ok_message:
        Message printed to stdout when there are no errors (default: ``"OK"``).
    list_flag:
        If provided, a ``--<list_flag>`` boolean argument is registered.
        When the user passes it, *list_callback* is called and the script exits 0.
    list_callback:
        Required when *list_flag* is set.  Called (with no arguments) when the
        user passes the list flag; should print human-readable output to stdout.
    argv:
        Argument list to parse; defaults to ``sys.argv[1:]`` when ``None``.
    """

    parser = argparse.ArgumentParser(description=description)
    parser.add_argument(
        "input_json",
        nargs="?" if list_flag else None,
        type=Path,
        metavar=usage_suffix,
        help="Path to the JSON file to validate.",
    )
    if list_flag:
        parser.add_argument(
            f"--{list_flag}",
            action="store_true",
            help="List available items and exit.",
        )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit machine-readable JSON output ({ok, errors, warnings}).",
    )

    args = parser.parse_args(argv if argv is not None else sys.argv[1:])

    if list_flag and getattr(args, list_flag.replace("-", "_")):
        if list_callback is not None:
            list_callback()
        return 0

    if args.input_json is None:
        parser.error("input_json is required")

    try:
        payload = load_json(args.input_json)
        errors, warnings = validator(payload)
    except Exception as exc:  # noqa: BLE001
        errors, warnings = [str(exc)], []

    if args.json:
        _emit_json(errors, warnings)
    else:
        _emit_text(errors, warnings, ok_message)

    return 1 if errors else 0
