"""Self-check and screenshots for the v34 mockups.

Loads every mockup state in headless Chromium, records console errors and page
exceptions, asserts the markers each state must show, and takes the design
authority's three captures (1580x1000, 1440x900, 760x1000) with the surface
scrolled into view, plus one whole-page shot per state at 1580. Writes
v34-shots/verification.json and prints RESULT {"fail":[],"okCount":N}.

Run from the repository root with the user-site Playwright (plain `python`):
  python design/planning-and-old-designs/v34_planning/current/check-v34.py
"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
SHOTS = HERE / "v34-shots"
VIEWPORTS = [(1580, 1000), (1440, 900), (760, 1000)]

# file -> state -> (focus selector, [(name, selector or text)])
CHECKS = {
    "pegasus_case_report_delivery_v34.html": {
        "plain": ("#section-report", [
            ("send form", "form[data-send-report]"),
            ("fingerprint posted", "input[name=dispatchFingerprint]"),
            ("one Send report", "text=Send report"),
        ]),
        "rules": ("#section-report", [
            ("from line", "[data-report-from]"),
            ("two questions", "[data-report-question] >> nth=1"),
            ("override offered for a possible stop", "[data-report-stop-override]"),
            ("cc hint", "[data-report-cc-hint]"),
            ("after sending", "[data-report-after-sending]"),
            ("hold tick", "[data-report-hold]"),
            ("already sent", "[data-report-already-sent]"),
        ]),
        "stop": ("#section-report", [
            ("stop notice", "[data-report-stop]"),
            ("override required", "[data-report-stop-override][required]"),
        ]),
        "missing": ("#section-report", [
            ("missing companion notice", "[data-report-missing-companion]"),
        ]),
    },
    "pegasus_case_tasks_v34.html": {
        "edit": ("#section-tasks", [
            ("task rows", "[data-case-tasks]"),
            ("add task", "form[data-case-task-form]"),
            ("assign", "text=Assign"),
            ("complete", "text=Complete"),
        ]),
        "read": ("#section-tasks", [("task rows", "[data-case-tasks]")]),
    },
    "pegasus_case_next_action_blocker_v34.html": {
        "next": (".case-aside, aside", [
            ("open tasks listed", "[data-open-tasks]"),
            ("mark completed greyed", "[data-next-mark-completed][disabled]"),
        ]),
        "archive": ("main", [("archive greyed", "[data-archive-case][disabled]")]),
    },
    "pegasus_work_centre_tasks_v34.html": {
        "row": ("[data-wc-row-kind=tasks]", [
            ("tasks row", "[data-wc-row-kind=tasks]"),
            ("tasks chip", "[data-wc-kind=tasks]"),
        ]),
    },
    "pegasus_contact_report_sending_v34.html": {
        key: ("[data-report-sending-editor]", [
            ("report sending panel", "[data-report-sending-editor]"),
            ("rules", "[data-rs-rules]"),
        ])
        for key in ["kerr-edit", "dfd", "ax", "mp", "pch", "rjs", "qdos"]
    },
}


def main() -> None:
    SHOTS.mkdir(exist_ok=True)
    fails: list[str] = []
    ok = 0
    shots: list[dict[str, str]] = []
    number = 0
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        for file, states in CHECKS.items():
            for state, (focus, checks) in states.items():
                for width, height in VIEWPORTS:
                    page = browser.new_page(viewport={"width": width, "height": height}, device_scale_factor=1)
                    errors: list[str] = []
                    page.on("console", lambda message: errors.append(message.text) if message.type == "error" else None)
                    page.on("pageerror", lambda error: errors.append(str(error)))
                    url = (HERE / file).resolve().as_uri() + f"?state={state}&strip=0"
                    page.goto(url)
                    page.wait_for_timeout(400)
                    if width == 1580:
                        if errors:
                            fails.append(f"{file} {state}: console {errors[:2]}")
                        else:
                            ok += 1
                        for name, selector in checks:
                            try:
                                if page.locator(selector).first.count() > 0:
                                    ok += 1
                                else:
                                    fails.append(f"{file} {state}: missing {name} ({selector})")
                            except Exception as error:  # noqa: BLE001
                                fails.append(f"{file} {state}: {name}: {error}")
                        number += 1
                        full = SHOTS / f"{number:02d}-{Path(file).stem.replace('pegasus_', '').replace('_v34', '')}-{state}-full.png"
                        page.screenshot(path=str(full), full_page=True)
                        shots.append({"shot": full.name, "file": file, "state": state, "viewport": "1580 whole page"})
                        # The surface alone, cut from the whole-page capture, so a tall
                        # form is readable in one image.
                        box = page.evaluate(
                            "s => { const e = document.querySelector(s); if (!e) return null; const r = e.getBoundingClientRect();"
                            " return [r.left + scrollX, r.top + scrollY, r.right + scrollX, r.bottom + scrollY]; }",
                            focus,
                        )
                        if box:
                            from PIL import Image

                            number += 1
                            crop = SHOTS / f"{number:02d}-{Path(file).stem.replace('pegasus_', '').replace('_v34', '')}-{state}-surface.png"
                            with Image.open(full) as image:
                                left, top, right, bottom = (int(v) for v in box)
                                image.crop((max(0, left - 12), max(0, top - 12), min(image.width, right + 12), min(image.height, bottom + 12))).save(crop)
                            shots.append({"shot": crop.name, "file": file, "state": state, "viewport": "1580 surface"})
                    target = page.locator(focus).first
                    if target.count() > 0:
                        target.scroll_into_view_if_needed()
                        page.wait_for_timeout(150)
                    number += 1
                    shot = SHOTS / f"{number:02d}-{Path(file).stem.replace('pegasus_', '').replace('_v34', '')}-{state}-{width}.png"
                    page.screenshot(path=str(shot))
                    shots.append({"shot": shot.name, "file": file, "state": state, "viewport": f"{width}x{height}"})
                    page.close()
        browser.close()
    result = {"fail": fails, "okCount": ok}
    (SHOTS / "verification.json").write_text(
        json.dumps({"checkedAtUtc": datetime.now(timezone.utc).isoformat(), "result": result, "shots": shots}, indent=2),
        encoding="utf-8",
    )
    print("RESULT " + json.dumps(result))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
