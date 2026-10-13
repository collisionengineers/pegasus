"""Self-check run and screenshots for the v40 Contacts mockup.

1. Opens v40-selfcheck.html in headless Chromium (file access allowed) and reads
   its RESULT line: every state, dialog, variable and query preset asserted.
2. Takes the design authority's three captures (1580x1000, 1440x900, 760x1000)
   of every shot the notes cite, plus a whole-page capture at 1580, recording
   console errors on every load.
Writes v40-shots/verification.json and prints RESULT {"fail":[],"okCount":N}.

Run from the repository root with the user-site Playwright (plain `python`):
  python design/planning-and-old-designs/v40_planning/current/check-v40.py
"""
from __future__ import annotations

import glob
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
SHOTS = HERE / "v40-shots"
MOCK = (HERE / "pegasus_contacts_v40.html").resolve().as_uri()
# The user-site Playwright may expect a browser build other than the one
# installed; fall back to any installed Chromium.
CHROME = next(iter(sorted(glob.glob(os.path.expandvars(r"%LOCALAPPDATA%\ms-playwright\chromium-*\chrome-win64\chrome.exe")), reverse=True)), None)
VIEWPORTS = [(1580, 1000), (1440, 900), (760, 1000)]

# (name, query, element scrolled into view or None)
SHOTLIST = [
    ("list", "state=list", None),
    ("list-live", "state=list-live", None),
    ("list-single", "state=list&opt=list:single", None),
    ("list-search", "state=list&q=riverside", None),
    ("list-empty", "state=list&q=zzzz", None),
    ("dialog-type", "state=list&dialog=type", None),
    ("dialog-new-principal", "state=list&dialog=create&create=Principal", None),
    ("dialog-matches", "state=list&dialog=matches&create=Repairer", None),
    ("principal", "state=principal", None),
    ("principal-notes", "state=principal", "[data-notes-on-every-case]"),
    ("principal-notes-rows", "state=principal&opt=notes:rows", "[data-notes-on-every-case]"),
    ("principal-tabs", "state=principal&opt=layout:tabs", None),
    ("principal-tabs-sending", "state=principal&opt=layout:tabs&tab=Report%20sending", None),
    ("principal-sending", "state=principal", "[aria-labelledby=principal-sending-title]"),
    ("principal-saved", "state=principal-saved", None),
    ("principal-refused", "state=principal-refused", None),
    ("principal-inactive", "state=principal-inactive", None),
    ("principal-live", "state=principal-live", None),
    ("claim-source", "state=claim-source", None),
    ("repairer", "state=repairer", None),
    ("repairer-links-list", "state=repairer&opt=links:list", None),
    ("storage", "state=storage", None),
    ("tpe", "state=tpe", None),
]


def main() -> None:
    SHOTS.mkdir(exist_ok=True)
    for old in SHOTS.glob("*.png"):
        old.unlink()
    fails: list[str] = []
    ok = 0
    shots: list[dict[str, str]] = []
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(args=["--allow-file-access-from-files"], executable_path=CHROME)
        page = browser.new_page(viewport={"width": 1700, "height": 1100})
        page.goto((HERE / "v40-selfcheck.html").resolve().as_uri())
        page.wait_for_function("document.getElementById('out').textContent.startsWith('RESULT')", timeout=120000)
        selfcheck = json.loads(page.locator("#out").text_content().removeprefix("RESULT "))
        page.close()
        fails += [f"selfcheck: {f}" for f in selfcheck["fail"]]
        ok += selfcheck["okCount"]
        for number, (name, query, focus) in enumerate(SHOTLIST, start=1):
            for width, height in VIEWPORTS:
                page = browser.new_page(viewport={"width": width, "height": height}, device_scale_factor=1)
                errors: list[str] = []
                page.on("console", lambda m: errors.append(m.text) if m.type == "error" else None)
                page.on("pageerror", lambda e: errors.append(str(e)))
                page.goto(f"{MOCK}?{query}&strip=0")
                page.wait_for_function("window.__mockReady === true")
                page.wait_for_timeout(250)
                if errors:
                    fails.append(f"{name} {width}: console {errors[:2]}")
                else:
                    ok += 1
                if focus:
                    page.evaluate("s => window.scrollTo(0, document.querySelector(s).getBoundingClientRect().top + scrollY - 64)", focus)
                    page.wait_for_timeout(100)
                shot = SHOTS / f"{number:02d}-{name}-{width}.png"
                page.screenshot(path=str(shot))
                shots.append({"shot": shot.name, "query": query, "viewport": f"{width}x{height}"})
                if width == 1580 and not query.count("dialog="):
                    full = SHOTS / f"{number:02d}-{name}-full.png"
                    page.screenshot(path=str(full), full_page=True)
                    shots.append({"shot": full.name, "query": query, "viewport": "1580 whole page"})
                page.close()
        browser.close()
    result = {"fail": fails, "okCount": ok}
    (SHOTS / "verification.json").write_text(
        json.dumps({"checkedAtUtc": datetime.now(timezone.utc).isoformat(), "selfcheck": selfcheck, "result": result, "shots": shots}, indent=2),
        encoding="utf-8",
    )
    print("RESULT " + json.dumps(result))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
