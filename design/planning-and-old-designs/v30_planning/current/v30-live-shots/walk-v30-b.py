"""Visual walk of the v30 B sign-in frame and Work Centre on the synthetic hosts.

Usage: python walk-v30-b.py  (hosts on 5240 auto-Administrator and 5241 --auth)
Writes PNGs to artifacts/ui-baseline-review/v30-b-live/ at 1580x1000, 1440x900, 760x1000
and a walk.json with the behaviours checked.
"""
import json
import os
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
OUT = HERE / "v30-b-live"
OUT.mkdir(exist_ok=True)
SIZES = [(1580, 1000), (1440, 900), (760, 1000)]
HOST = "https://localhost:5240"
AUTH = json.loads((HERE / "visual" / "auth-host.json").read_text(encoding="utf-8"))
AUTH_URL = AUTH["url"]
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

    # --- Sign-in family on the --auth host -----------------------------------
    context = browser.new_context(ignore_https_errors=True, viewport={"width": 1580, "height": 1000})
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(AUTH_URL + "/Account/SignIn")
    check("sign-in heading", page.locator("h1").inner_text().strip() == "Sign in")
    check("identity panel", page.locator(".auth-identity .brand-copy strong").inner_text().strip() == "PEGASUS")
    check("company caption", "Collision Engineers" in page.locator(".auth-company").inner_text())
    reveal = page.locator("[data-password-reveal]")
    check("reveal shown by script", reveal.is_visible())
    shoot_all(page, "signin-default")
    page.fill("#Password", "secret")
    reveal.click()
    check("reveal shows password", page.get_attribute("#Password", "type") == "text" and reveal.inner_text() == "Hide" and reveal.get_attribute("aria-pressed") == "true")
    reveal.click()
    check("reveal hides again", page.get_attribute("#Password", "type") == "password")
    page.fill("#Password", "")
    page.click("button[type=submit]")
    page.wait_for_load_state()
    check("required messages", "Enter your username." in page.content() and "Enter your password." in page.content())
    shoot_all(page, "signin-validation")
    page.fill("#UserName", AUTH["credentials"]["userName"])
    page.fill("#Password", "wrong-password")
    page.click("button[type=submit]")
    page.wait_for_load_state()
    check("refusal notice", page.locator(".auth-notice[role=alert]").count() == 1 and "incorrect" in page.locator(".auth-notice").inner_text())
    check("username kept, password cleared", page.input_value("#UserName") == AUTH["credentials"]["userName"] and page.input_value("#Password") == "")
    shoot_all(page, "signin-error")
    page.goto(AUTH_URL + "/Account/SignIn?signedOut=true")
    check("signed out heading", "You are signed out" in page.locator("h1").inner_text())
    shoot_all(page, "signin-signed-out")
    page.goto(AUTH_URL + "/status/404")
    shoot_all(page, "status-404")
    page.goto(AUTH_URL + "/Account/AccessDenied?ReturnUrl=%2FAdministration")
    shoot_all(page, "access-denied")
    check("no console errors on auth family", not errors, "; ".join(errors))
    context.close()

    # --- Work Centre on the auto-Administrator host ---------------------------
    context = browser.new_context(ignore_https_errors=True, viewport={"width": 1580, "height": 1000})
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(HOST + "/")
    page.wait_for_selector("[data-work-centre]")
    check("no New case in utility bar", page.locator(".utility-actions a.btn").count() == 0)
    check("Updated in head", page.locator("[data-wc-refresh-outcome-label]").inner_text().startswith("Updated "))
    check("five metrics", page.locator(".wc-metrics .metric").count() == 5)
    tabs = page.locator("[data-wc-tab-link]")
    check("tabs rendered", tabs.count() >= 1, str(tabs.count()))
    check("no row open by itself", page.locator(".wc-inline-detail").count() == 0)
    check("no empty group heading", page.locator(".wc-group-row").count() == page.evaluate("[...document.querySelectorAll('.wc-group-row')].filter(r=>r.nextElementSibling&&r.nextElementSibling.hasAttribute('data-wc-row')).length"))
    shoot_all(page, "work-centre-default")

    rows = page.locator("tr[data-wc-row] .wc-task-link")
    if rows.count() > 0:
        rows.first.click()
        page.wait_for_selector(".wc-inline-detail")
        check("row opens in place", page.locator("tr[aria-selected=true] + .wc-inline-detail").count() == 1)
        check("open row facts", page.locator(".wc-inline-detail .fact").count() == 6)
        shoot_all(page, "work-centre-open-row")
        page.locator("tr[aria-selected=true] .wc-task-link").click()
        page.wait_for_selector("[data-work-centre]")
        check("second click closes", page.locator(".wc-inline-detail").count() == 0)

    page.click("[data-wc-scope-link=mine]")
    page.wait_for_selector("[data-work-centre]")
    check("Mine selected", page.get_attribute("[data-work-centre]", "data-wc-scope") == "mine")
    shoot_all(page, "work-centre-mine")
    page.click("[data-wc-scope-link=office]")
    page.wait_for_selector("[data-work-centre]")

    first_kind = page.locator("[data-wc-kind]").first
    first_kind.click()
    page.wait_for_selector("[data-work-centre]")
    page.fill("[data-wc-search]", "zzz-no-such-record")
    page.press("[data-wc-search]", "Enter")
    page.wait_for_selector("[data-work-centre]")
    check("no-match message", "No work matches these filters." in page.content())
    check("clear filters shown", page.locator("[data-wc-clear]").count() == 1)
    check("term kept in field", page.input_value("[data-wc-search]") == "zzz-no-such-record")
    shoot_all(page, "work-centre-no-match")
    page.click("[data-wc-clear]")
    page.wait_for_selector("[data-work-centre]")
    check("clear filters clears both", page.locator(".chip.on").count() == 0 and page.input_value("[data-wc-search]") == "")

    for tab in ["new-cases", "ai-jobs"]:
        link = page.locator(f"[data-wc-tab-link={tab}]")
        if link.count() == 0:
            check(f"tab {tab} omitted (section empty)", True)
            continue
        link.click()
        page.wait_for_timeout(200)
        check(f"tab {tab} switches in place", page.locator(f"#wc-panel-{tab}").is_visible() and not page.locator("#wc-panel-attention").is_visible() and f"tab={tab}" in page.url)
        shoot_all(page, f"work-centre-{tab}")
        page.keyboard.press("Home")
        page.wait_for_timeout(150)
        check(f"Home key returns to attention from {tab}", page.locator("#wc-panel-attention").is_visible())

    assign = page.locator("[data-wc-dialog=wc-assign-dialog]")
    unassigned = page.locator("tr[data-wc-row-kind=unassigned] .wc-task-link")
    if unassigned.count() > 0:
        unassigned.first.click()
        page.wait_for_selector("[data-wc-dialog=wc-assign-dialog]")
        page.click("[data-wc-dialog=wc-assign-dialog]")
        page.wait_for_selector("#wc-assign-dialog:not([hidden])")
        check("assign dialog focus", page.evaluate("document.activeElement && document.activeElement.id") == "wc-assign-engineer")
        shoot_all(page, "work-centre-assign")
        page.keyboard.press("Escape")
        page.wait_for_timeout(150)
        check("Escape closes and returns focus", page.locator("#wc-assign-dialog[hidden]").count() == 1 and page.evaluate("document.activeElement && document.activeElement.hasAttribute('data-wc-dialog')"))
    else:
        check("assign dialog (no Unassigned fixture row)", True, "skipped")

    check("no console errors on Work Centre", not errors, "; ".join(errors))
    context.close()
    browser.close()

(OUT / "walk.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
failed = [c for c in record["checks"] if not c["ok"]]
print(f"RESULT checks={len(record['checks'])} failed={len(failed)} shots={len(record['shots'])}")
sys.exit(1 if failed else 0)
