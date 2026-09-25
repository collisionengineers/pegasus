"""Focused offline mockup evidence; uses the workstation's existing Playwright.

Run: python design/planning-and-old-designs/v30_planning/current/check-signin-designs.py
No Pegasus service, build, account, or dependency installation is needed.
"""
import json
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import sync_playwright

current = Path(__file__).resolve().parent
shots = current / "v30-signin-shots"
shots.mkdir(exist_ok=True)
errors = []
external_requests = []

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, args=["--allow-file-access-from-files"])
    context = browser.new_context(viewport={"width": 1580, "height": 1000}, device_scale_factor=1)

    def reject_external(route):
        external_requests.append(route.request.url)
        route.abort()

    context.route("http://**", reject_external)
    context.route("https://**", reject_external)
    page = context.new_page()
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: errors.append(message.text) if message.type == "error" else None)
    page.goto((current / "v30-signin-selfcheck.html").as_uri())
    page.wait_for_function("window.selfcheckResult !== undefined", timeout=60000)
    result = page.evaluate("window.selfcheckResult")
    print("RESULT " + json.dumps(result), flush=True)
    if result["fail"]:
        raise SystemExit("Mockup self-check failed; no screenshot set claimed.")

    count = 0
    for state_index, state in enumerate(["default", "validation", "error", "signed-out"]):
        for option_index, option in enumerate(["a", "b", "c"]):
            number = state_index * 3 + option_index + 1
            for width, height in [(1580, 1000), (1440, 900), (760, 1000)]:
                page.set_viewport_size({"width": width, "height": height})
                url = (current / f"pegasus_signin_{option}_v30.html").as_uri()
                page.goto(f"{url}?state={state}&embed=1")
                page.evaluate("document.fonts.ready")
                page.screenshot(path=str(shots / f"{number:02}-{option}-{state}-{width}.png"))
                count += 1
            print(f"Captured {option.upper()} / {state}", flush=True)

    # Actual keyboard traversal supplements the DOM state checks.
    for option in ["a", "b", "c"]:
        page.goto((current / f"pegasus_signin_{option}_v30.html").as_uri())
        page.locator("#UserName").focus()
        page.keyboard.press("Tab")
        assert page.locator("#Password").evaluate("el => el === document.activeElement")
        page.keyboard.press("Tab")
        assert page.get_by_role("button", name="Show password").evaluate("el => el === document.activeElement")
        page.keyboard.press("Space")
        assert page.locator("#Password").get_attribute("type") == "text"
        page.keyboard.press("Tab")
        assert page.get_by_role("button", name="Sign in", exact=True).evaluate("el => el === document.activeElement")
        page.locator(".mockup-controls summary").click()
        page.locator("#state-control").select_option("error")
        page.wait_for_url("**state=error")
        assert page.locator("#credential-error").is_visible()

    # Verify the keyboard focus treatment also survives forced colours.
    page.emulate_media(forced_colors="active", reduced_motion="reduce")
    page.locator("#UserName").focus()
    assert page.locator("#UserName").evaluate("el => getComputedStyle(el).outlineStyle") != "none"
    browser.close()

evidence = {
    "recordedUtc": datetime.now(timezone.utc).isoformat(),
    "scope": "Offline sign-in mockups only; not application evidence",
    "selfcheck": result,
    "screenshots": count,
    "keyboardDesigns": ["a", "b", "c"],
    "consoleErrors": errors,
    "externalRequests": external_requests,
}
(shots / "verification.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
assert not errors, errors
assert not external_requests, external_requests
print(f"Saved {count} screenshots. Keyboard checks passed; no console errors or external requests.")
