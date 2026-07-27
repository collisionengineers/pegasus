#!/usr/bin/env python3
"""Cross-engine parity gate for the renderer convergence (Wave 2).

Renders the SAME valuation case through both engines and compares the outputs
structurally (never by sha256 --different engines never byte-match):

  * OLD: the canonical Python vehicle-valuation skill  (Jinja2 -> WeasyPrint)
         scripts/render_report.py + scripts/render_evidence_pack.py
  * NEW: the .NET CollisionRenderer.Mcp host           (Scriban -> Chromium)
         render_valuation_outputs over stdio MCP

The gate asserts:
  1. Validation parity     --both engines accept a valid payload.
  2. No dropped fields      --every subject/advert value the Python report shows
                              also appears in the .NET report (the core risk).
  3. Page-count parity      --report and pack page counts within tolerance.
  4. Capture-append parity  --every captured-advert marker appears in BOTH packs,
                              and both packs carry one appended page per capture.

Requires (all confirmed present on this machine): Python weasyprint, jinja2,
pypdf, reportlab; a built .NET host exe; Playwright Chromium installed.

Usage:  python tests/parity/parity_check.py [--exe <path>] [--keep]
Exit code 0 = all cases pass, 1 = any failure.
"""

from __future__ import annotations

import argparse
import base64
import copy
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import unquote, urlparse

HERE = Path(__file__).resolve().parent
COLLISIONRENDERER_ROOT = HERE.parents[1]          # active/collisionrenderer
REPO_ROOT = HERE.parents[3]                        # collisionsuite
SCRIPTS = REPO_ROOT / "collision-agent-skills" / "vehicle-valuation" / "scripts"
CONTRACTS = REPO_ROOT / "connectors" / "valuation-adverts-connector" / "contracts" / "generated" / "python"
DEFAULT_EXE = COLLISIONRENDERER_ROOT / "src" / "CollisionRenderer.Mcp" / "bin" / "Release" / "net8.0" / "collisionrenderer-mcp.exe"
CASES_DIR = HERE / "cases"

PAGE_TOLERANCE = 2  # different layout engines; allow a small page-count delta


# --------------------------------------------------------------------------- #
# helpers
# --------------------------------------------------------------------------- #

def norm(text: str) -> str:
    """Lowercase and keep only [a-z0-9] so formatting/spacing/punctuation
    differences between WeasyPrint and Chromium don't cause false mismatches."""
    return re.sub(r"[^a-z0-9]", "", text.lower())


def pdf_text_and_pages(path: Path) -> tuple[str, int]:
    from pypdf import PdfReader

    reader = PdfReader(str(path))
    text = "\n".join((page.extract_text() or "") for page in reader.pages)
    return text, len(reader.pages)


def make_capture_pdf(path: Path, marker: str) -> None:
    from reportlab.lib.pagesizes import A4
    from reportlab.pdfgen import canvas

    c = canvas.Canvas(str(path), pagesize=A4)
    c.setFont("Helvetica", 14)
    c.drawString(72, 780, f"CAPTURE MARKER {marker}")
    c.drawString(72, 760, "Parity harness synthetic advert capture.")
    c.showPage()
    c.save()


def run_python(script: str, work: Path) -> str:
    env = {**os.environ, "PYTHONPATH": str(CONTRACTS), "VALUATION_OUTPUT_ROOT": str(work)}
    proc = subprocess.run(
        [sys.executable, str(SCRIPTS / script), "payload.json"],
        cwd=str(work), env=env, capture_output=True, text=True,
    )
    if proc.returncode != 0:
        raise RuntimeError(f"{script} failed (exit {proc.returncode}):\n{proc.stderr}")
    # The PDF path is the last non-empty stdout line.
    lines = [ln for ln in proc.stdout.splitlines() if ln.strip()]
    if not lines:
        raise RuntimeError(f"{script} produced no output path.\nstderr:\n{proc.stderr}")
    return lines[-1].strip()


def net_render_valuation_outputs(exe: Path, payload: dict, captures: list[dict]) -> dict:
    """Drive the stdio MCP host and return the render_valuation_outputs envelope."""
    proc = subprocess.Popen(
        [str(exe)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL, text=True, bufsize=1,
    )

    def send(obj: dict) -> None:
        assert proc.stdin is not None
        proc.stdin.write(json.dumps(obj) + "\n")
        proc.stdin.flush()

    send({"jsonrpc": "2.0", "id": 1, "method": "initialize",
          "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                     "clientInfo": {"name": "parity", "version": "1.0"}}})
    send({"jsonrpc": "2.0", "method": "notifications/initialized"})
    send({"jsonrpc": "2.0", "id": 2, "method": "tools/call",
          "params": {"name": "render_valuation_outputs",
                     "arguments": {"payload": payload, "captures": captures, "includeBase64": False}}})

    envelope = None
    assert proc.stdout is not None
    for line in proc.stdout:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            continue
        if msg.get("id") == 2:
            result = msg.get("result") or {}
            # The host returns the JSON envelope as content[0].text.
            text = result["content"][0]["text"]
            envelope = json.loads(text)
            break

    try:
        if proc.stdin:
            proc.stdin.close()
        proc.wait(timeout=10)
    except Exception:
        proc.kill()

    if envelope is None:
        raise RuntimeError("no render_valuation_outputs response from .NET host")
    return envelope


def uri_to_path(uri: str) -> Path:
    return Path(unquote(urlparse(uri).path.lstrip("/")))


def check(results: list, label: str, ok: bool, detail: str = "") -> None:
    results.append((label, ok, detail))


# --------------------------------------------------------------------------- #
# per-case comparison
# --------------------------------------------------------------------------- #

def field_checks(case: dict) -> list[tuple[str, str]]:
    """(label, expected-value) pairs that must survive into the .NET report."""
    checks: list[tuple[str, str]] = []
    s = case["subject_vehicle"]
    for key in ["registration", "make", "model", "derivative", "body_type", "fuel",
                "transmission", "engine", "first_registered", "mileage", "colour", "vin"]:
        if s.get(key):
            checks.append((f"subject.{key}", str(s[key])))
    if case.get("assessed_retail_value"):
        checks.append(("assessed_retail_value", str(case["assessed_retail_value"])))
    if case.get("valuation_mode") == "guide_supported" and case.get("guide_value"):
        checks.append(("guide_value", str(case["guide_value"])))
    for i, a in enumerate(case["adverts"]):
        for key in ["price", "source", "seller_type", "location", "registration_year"]:
            if a.get(key):
                checks.append((f"adverts[{i}].{key}", str(a[key])))
    return checks


def run_case(case_path: Path, exe: Path, keep: bool) -> bool:
    name = case_path.stem
    case = json.loads(case_path.read_text(encoding="utf-8"))
    results: list[tuple[str, bool, str]] = []

    work = Path(tempfile.mkdtemp(prefix=f"parity-{name}-"))
    try:
        # 1. synthetic capture PDFs (one per advert), markered by advert_id/index.
        markers = []
        py_payload = copy.deepcopy(case)
        net_captures = []
        for i, advert in enumerate(py_payload["adverts"]):
            marker = str(advert.get("advert_id") or f"ADVERT-{i}")
            markers.append(marker)
            cap = work / f"cap_{i}.pdf"
            make_capture_pdf(cap, marker)
            advert["captured_pdf_path"] = cap.name           # Python resolves relative to payload dir
            net_captures.append({"url": advert["url"], "status": "success",
                                 "pdf_base64": base64.b64encode(cap.read_bytes()).decode("ascii")})

        (work / "payload.json").write_text(json.dumps(py_payload), encoding="utf-8")

        # 2. OLD engine (Python / WeasyPrint)
        py_report = Path(run_python("render_report.py", work))
        py_pack = Path(run_python("render_evidence_pack.py", work))

        # 3. NEW engine (.NET / Chromium) --payload WITHOUT captured_pdf_path; captures carry bytes.
        net_payload = copy.deepcopy(case)
        envelope = net_render_valuation_outputs(exe, net_payload, net_captures)
        net_ok = bool(envelope.get("validation", {}).get("ok"))
        check(results, "validation.ok (.NET)", net_ok,
              "" if net_ok else json.dumps(envelope.get("validation", {})))
        if not net_ok:
            return report_case(name, results)

        artifacts = {a["kind"]: a for a in envelope["artifacts"]}
        net_report = uri_to_path(artifacts["valuation_report"]["uri"])
        net_pack = uri_to_path(artifacts["valuation_evidence_pack"]["uri"])

        # 4. extract text + pages
        py_rep_txt, py_rep_pg = pdf_text_and_pages(py_report)
        net_rep_txt, net_rep_pg = pdf_text_and_pages(net_report)
        py_pack_txt, py_pack_pg = pdf_text_and_pages(py_pack)
        net_pack_txt, net_pack_pg = pdf_text_and_pages(net_pack)
        py_rep_n, net_rep_n = norm(py_rep_txt), norm(net_rep_txt)
        py_pack_n, net_pack_n = norm(py_pack_txt), norm(net_pack_txt)

        # 5a. no-dropped-fields: anything in the Python report must be in the .NET report.
        for label, value in field_checks(case):
            nv = norm(value)
            in_py = nv in py_rep_n
            in_net = nv in net_rep_n
            if in_py and not in_net:
                check(results, f"field {label}", False, f"present in Python report, DROPPED from .NET ('{value}')")
            elif not in_py and not in_net:
                check(results, f"field {label}", True, f"(neither engine renders '{value}' --not a drop)")
            else:
                check(results, f"field {label}", in_net, "" if in_net else f"missing '{value}'")

        # 5b. page-count parity
        check(results, "report pages", abs(py_rep_pg - net_rep_pg) <= PAGE_TOLERANCE,
              f"python={py_rep_pg} dotnet={net_rep_pg}")
        check(results, "pack pages", abs(py_pack_pg - net_pack_pg) <= PAGE_TOLERANCE,
              f"python={py_pack_pg} dotnet={net_pack_pg}")

        # 5c. capture-append parity: each marker present in BOTH packs.
        for marker in markers:
            mk = norm(f"CAPTURE MARKER {marker}")
            check(results, f"capture {marker} in Python pack", mk in py_pack_n)
            check(results, f"capture {marker} in .NET pack", mk in net_pack_n)

        # 5d. both packs grew by the appended captures (pages > table-only).
        check(results, "pack appended pages (.NET)", net_pack_pg >= len(markers) + 1,
              f"pages={net_pack_pg} captures={len(markers)}")
        check(results, "pack appended pages (Python)", py_pack_pg >= len(markers) + 1,
              f"pages={py_pack_pg} captures={len(markers)}")

        return report_case(name, results)
    finally:
        if not keep:
            import shutil
            shutil.rmtree(work, ignore_errors=True)
        else:
            print(f"  [kept work dir: {work}]")


def report_case(name: str, results: list[tuple[str, bool, str]]) -> bool:
    passed = all(ok for _, ok, _ in results)
    print(f"\n=== CASE: {name} --{'PASS' if passed else 'FAIL'} ===")
    for label, ok, detail in results:
        mark = "ok " if ok else "XX "
        line = f"  [{mark}] {label}"
        if detail:
            line += f"  --{detail}"
        print(line)
    return passed


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--keep", action="store_true", help="keep temp work dirs")
    args = ap.parse_args()

    exe = Path(args.exe)
    if not exe.exists():
        print(f"ERROR: .NET host exe not found: {exe}\nBuild it: dotnet build src/CollisionRenderer.Mcp -c Release", file=sys.stderr)
        return 2

    cases = sorted(CASES_DIR.glob("*.json"))
    if not cases:
        print(f"ERROR: no cases under {CASES_DIR}", file=sys.stderr)
        return 2

    all_pass = True
    for case in cases:
        try:
            all_pass &= run_case(case, exe, args.keep)
        except Exception as exc:
            all_pass = False
            print(f"\n=== CASE: {case.stem} --ERROR ===\n  {type(exc).__name__}: {exc}")

    print(f"\n{'=' * 48}\nPARITY GATE: {'PASS' if all_pass else 'FAIL'}")
    return 0 if all_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
