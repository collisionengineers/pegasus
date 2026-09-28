"""Visual walk of the v30 Upload E surfaces on the synthetic auto-Administrator host (5240).

Writes PNGs to artifacts/ui-baseline-review/upload-e-live/ at 1580x1000, 1440x900, 760x1000
and a walk.json with the behaviours checked.
"""
import base64
import json
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
OUT = HERE / "upload-e-live"
OUT.mkdir(exist_ok=True)
SIZES = [(1580, 1000), (1440, 900), (760, 1000)]
HOST = json.loads((HERE / "visual" / "host.json").read_text(encoding="utf-8"))
URL = HOST["url"]
TINY_PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")
record = {"checks": [], "shots": []}


def check(name, ok, detail=""):
    record["checks"].append({"name": name, "ok": bool(ok), "detail": detail})
    print(("PASS " if ok else "FAIL ") + name + (" · " + detail if detail else ""))


def shoot(page, name, width, height):
    page.set_viewport_size({"width": width, "height": height})
    page.wait_for_timeout(250)
    path = OUT / f"{name}-{width}.png"
    page.screenshot(path=str(path))
    record["shots"].append(path.name)
    overflow = page.evaluate("document.documentElement.scrollWidth > document.documentElement.clientWidth")
    check(f"{name}-{width} no horizontal spill", not overflow)


def shoot_all(page, name):
    for width, height in SIZES:
        shoot(page, name, width, height)
    page.set_viewport_size({"width": 1580, "height": 1000})


with sync_playwright() as p:
    browser = p.chromium.launch()
    context = browser.new_context(ignore_https_errors=True, viewport={"width": 1580, "height": 1000})
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))

    # --- Choosing files -------------------------------------------------------
    page.goto(URL + "/Upload")
    check("select: picker enhanced", page.locator("[data-upload-select].is-enhanced").count() == 1)
    check("select: no aside, files hidden", page.locator("[data-select-aside], .up-empty-aside").count() == 0 and not page.locator("[data-select-files]").is_visible())
    check("select: actions hidden until files", not page.locator("[data-select-actions]").is_visible())
    shoot_all(page, "upload-select")
    files = [{"name": f"WhatsApp Image 2026-09-17 at 12.57.4{i} PM.png", "mimeType": "image/png", "buffer": TINY_PNG} for i in range(3)]
    page.set_input_files("input[type=file]", files)
    page.wait_for_timeout(200)
    check("chosen: three rows", page.locator("[data-select-list] .up-file").count() == 3)
    check("chosen: heading Ready", page.locator("[data-select-heading]").inner_text() == "Ready to upload")
    check("chosen: submit label", page.locator("[data-select-submit-label]").inner_text() == "Upload 3 files")
    check("chosen: thumbnails are object URLs", page.locator("[data-select-list] img[src^='blob:']").count() == 3)
    page.set_input_files("input[type=file]", [{"name": "instruction.pdf", "mimeType": "application/pdf", "buffer": b"%PDF-1.4 tiny"}])
    page.wait_for_timeout(200)
    check("chosen: adding accumulates", page.locator("[data-select-list] .up-file").count() == 4)
    page.set_input_files("input[type=file]", [{"name": "notes.txt", "mimeType": "text/plain", "buffer": b"x"}])
    page.wait_for_timeout(200)
    check("chosen: unsupported type refused with message", page.locator("[data-select-errors]").is_visible() and "not supported" in page.locator("[data-select-error-list]").inner_text() and page.locator("[data-select-list] .up-file").count() == 4)
    shoot_all(page, "upload-chosen")
    page.locator("[data-remove]").last.click()
    page.wait_for_timeout(150)
    check("chosen: remove one", page.locator("[data-select-list] .up-file").count() == 3 and not page.locator("[data-select-errors]").is_visible())
    page.click("button[type=reset]")
    page.wait_for_timeout(200)
    check("chosen: clear empties", page.locator("[data-select-list] .up-file").count() == 0 and not page.locator("[data-select-files]").is_visible())
    page.set_input_files("input[type=file]", files[:2])
    page.wait_for_timeout(200)
    page.click("[data-select-submit]")
    page.wait_for_url("**/Upload/Group/**", timeout=20000)
    check("upload posts and lands on the review", "/Upload/Group/" in page.url)
    review_url = page.url
    check("review: pending phase", page.locator("[data-upload-phase=pending]").count() == 1)
    check("review: two files inspected", page.locator("[data-inspect-count]").inner_text() == "1 of 2")
    check("review: auto refresh set", page.locator("[data-auto-refresh]").get_attribute("data-auto-refresh") not in (None, ""))
    shoot_all(page, "upload-review-pending")
    page.click("[data-upload-inspect='1']")
    page.wait_for_timeout(150)
    check("review: filmstrip switches in place", page.locator("[data-inspect-count]").inner_text() == "2 of 2" and page.url == review_url)

    # --- The fixture's processed image group ----------------------------------
    page.goto(URL + HOST["manualImageGroupPath"])
    phase = page.locator("[data-upload-phase]").get_attribute("data-upload-phase")
    check("fixture group renders a phase", phase in ("decision", "report", "attached", "pending"), phase)
    shoot_all(page, f"upload-review-{phase}")
    if page.locator("[data-upload-preview]").count() > 0:
        page.locator("[data-inspect-open]").click()
        page.wait_for_timeout(200)
        preview = page.locator("#upload-preview")
        check("preview dialog opens in place", preview.is_visible() and page.url.startswith(URL + HOST["manualImageGroupPath"]))
        shoot(page, f"upload-preview", 1580, 1000)
        page.click("[data-preview-next]")
        page.wait_for_timeout(100)
        check("preview next cycles", "File" in page.locator("[data-preview-title]").inner_text())
        page.keyboard.press("Escape")
        page.wait_for_timeout(150)
        check("preview Escape closes and returns focus", not preview.is_visible() and page.evaluate("document.activeElement && document.activeElement.hasAttribute('data-inspect-open')"))
    if phase == "decision":
        check("decision: no candidate shows no Review slot", page.locator(".up-case").count() > 0 or not page.locator("[data-upload-review-button]").is_visible())
        if page.locator(".up-case").count() == 0:
            page.fill("[data-upload-search]", "QDOS")
            page.press("[data-upload-search]", "Enter")
            page.wait_for_selector("[data-upload-phase]")
            check("find: results render as cards", page.locator(".up-case").count() > 0, str(page.locator(".up-case").count()))
            shoot_all(page, "upload-review-search")
        if page.locator(".up-case").count() > 0:
            check("decision: nothing preselected", page.locator(".up-case input:checked").count() == 0)
            check("decision: review hidden until chosen", not page.locator("[data-upload-review-button]").is_visible())
            page.locator(".up-case").first.click()
            page.wait_for_timeout(100)
            check("decision: review appears", page.locator("[data-upload-review-button]").is_visible())
            page.click("[data-upload-review-button]")
            page.wait_for_timeout(200)
            confirm = page.locator("#upload-confirm")
            ref = page.locator(".up-case input:checked").get_attribute("data-reference")
            check("confirm dialog repeats the target", confirm.is_visible() and page.locator("[data-confirm-ref]").inner_text() == ref and ref in page.locator("[data-confirm-submit]").inner_text())
            shoot(page, "upload-confirm", 1580, 1000)
            page.keyboard.press("Escape")
            page.wait_for_timeout(150)
            check("confirm Escape closes", not confirm.is_visible())
        page.fill("[data-upload-search]", "zzz-no-such-case")
        page.press("[data-upload-search]", "Enter")
        page.wait_for_selector("[data-upload-phase]")
        check("find: no match sentence", "No Cases or Triage items match" in page.content() and "q=zzz" in page.url)
        shoot_all(page, "upload-review-no-match")
        if page.locator("[data-upload-dialog=upload-discard]").count() > 0:
            page.click("[data-upload-dialog=upload-discard]")
            page.wait_for_timeout(200)
            discard = page.locator("#upload-discard")
            check("discard dialog opens, checkbox focused", discard.is_visible() and page.evaluate("document.activeElement && document.activeElement.id") == "upload-discard-check")
            page.click("#upload-discard-form ~ .dialog-foot .btn--danger")
            page.wait_for_timeout(100)
            check("discard blocked without acknowledgement", discard.is_visible() and page.url.endswith("q=zzz-no-such-case"))
            shoot(page, "upload-discard", 1580, 1000)
            page.keyboard.press("Escape")
            page.wait_for_timeout(150)
            check("discard Escape closes", not discard.is_visible())

    check("no console errors on Upload surfaces", not errors, "; ".join(errors))
    context.close()
    browser.close()

(OUT / "walk.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
failed = [c for c in record["checks"] if not c["ok"]]
print(f"RESULT checks={len(record['checks'])} failed={len(failed)} shots={len(record['shots'])}")
sys.exit(1 if failed else 0)
