"""Build the v34 offline mockups from the captured as-built pages.

Each mockup is one self-contained HTML file: the live shell (CSS, fonts, scripts
and the one image inlined from src/Pegasus.Web/wwwroot) with one captured page
per state in a <template>, a runtime that places the chosen state before the
page scripts run, a stubbed fetch, and the "Mockup controls" strip (demo
control, not product UI). States are reachable by ?state=<key>.

Run from the repository root:  python design/planning-and-old-designs/v34_planning/current/build-v34.py
"""
from __future__ import annotations

import base64
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
CAPTURED = HERE / "captured"
WEB = ROOT / "src" / "Pegasus.Web" / "wwwroot"
VERSION = "v34"

# surface -> (title, [(state key, label, captured file, transform)])
SURFACES = {
    "pegasus_case_report_delivery_v34.html": (
        "Case record · Report · Send report",
        [
            ("plain", "Default rules, nothing asked", "delivery-plain.html", None),
            ("rules", "Rules: questions, possible stop, hold, after sending", "delivery-rules.html", None),
            ("stop", "A rule Stop applies: override reason required", "delivery-stop.html", None),
            ("missing", "Required companion not generated: Send withheld", "delivery-missing-companion.html", None),
        ],
    ),
    "pegasus_case_tasks_v34.html": (
        "Case record · Tasks",
        [
            ("edit", "Edit session: add, assign, complete, cancel", "tasks-edit.html", None),
            ("read", "Read: the list without its actions", "tasks-edit.html", "tasks-read"),
        ],
    ),
    "pegasus_case_next_action_blocker_v34.html": (
        "Case record · Next action after the send",
        [
            ("next", "Open tasks listed; Mark completed greyed", "blocker-next-action.html", None),
            ("archive", "A closed Case with an open task: Archive greyed", "blocker-archive.html", None),
        ],
    ),
    "pegasus_work_centre_tasks_v34.html": (
        "Work Centre · Open tasks row",
        [("row", "One row per Case with open tasks", "work-centre-tasks-row.html", None)],
    ),
    "pegasus_contact_report_sending_v34.html": (
        "Contacts · Principal · Report sending",
        [
            ("kerr-edit", "KERR, editing (default rules)", "contact-kerr-edit.html", None),
            ("dfd", "DFD: three Claim Source rules, one a Stop", "contact-dfd-read.html", None),
            ("ax", "AX: fixed To, Bodyshop mentions rule", "contact-ax-read.html", None),
            ("mp", "MP: required companions, no report images", "contact-mp-read.html", None),
            ("pch", "PCH: send to only, Audatex, attachment name", "contact-pch-read.html", None),
            ("rjs", "RJS: Instruction mentions and Claim Source", "contact-rjs-read.html", None),
            ("qdos", "QDOS: Cc and garage figures", "contact-qdos-read.html", None),
        ],
    ),
}

FINGERPRINT = re.compile(r"\.[a-z0-9]{10}\.(css|js|png|woff2)")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def asset_bytes(url: str) -> bytes:
    relative = FINGERPRINT.sub(r".\1", url.lstrip("/"))
    return (WEB / relative).read_bytes()


def css_with_fonts(name: str) -> str:
    text = asset_bytes(f"/css/{name}").decode("utf-8")

    def font(match: re.Match[str]) -> str:
        relative = FINGERPRINT.sub(r".\1", match.group(1))
        data = (WEB / "css" / relative).resolve().read_bytes()
        return f"url(data:font/woff2;base64,{base64.b64encode(data).decode('ascii')})"

    return re.sub(r"url\((\.\./fonts/[^)]+)\)", font, text)


def inline_images(html: str) -> str:
    def image(match: re.Match[str]) -> str:
        data = asset_bytes(match.group(1))
        return f'src="data:image/png;base64,{base64.b64encode(data).decode("ascii")}"'

    return re.sub(r'src="(/images/[^"]+\.png)"', image, html)


def split(html: str) -> tuple[list[str], list[str], str]:
    """The stylesheet names, the script names and the body inner HTML of a captured page."""
    styles = re.findall(r'<link rel="stylesheet" href="/css/([^"]+)"', html)
    scripts = re.findall(r'<script src="/js/([^"]+)"></script>', html)
    body = re.search(r"<body[^>]*>(.*)</body>", html, re.S)
    if body is None:
        raise SystemExit("no body")
    inner = re.sub(r'<script src="/js/[^"]+"></script>\s*', "", body.group(1))
    return styles, scripts, inner


def tasks_read(inner: str) -> str:
    fragment = read(CAPTURED / "tasks-fragment-read.html")
    return re.sub(
        r'<section class="record-section panel[^"]*" id="section-tasks".*?</section>',
        lambda _: fragment,
        inner,
        count=1,
        flags=re.S,
    )


TRANSFORMS = {"tasks-read": tasks_read}

STRIP_CSS = """
#mock-strip{position:fixed;left:12px;bottom:12px;z-index:9999;background:#1d2229;color:#e6e9ee;font:12.5px/1.4 system-ui,sans-serif;border-radius:8px;box-shadow:0 6px 24px rgba(0,0,0,.35);max-width:360px}
#mock-strip summary{cursor:pointer;padding:8px 12px;font-weight:650;letter-spacing:.02em}
#mock-strip .mock-body{padding:0 12px 12px;display:grid;gap:8px}
#mock-strip select{width:100%;font:inherit;padding:4px 6px;border-radius:4px;border:1px solid #3a424d;background:#2a313a;color:inherit}
#mock-strip small{color:#9aa4b2}
#mock-strip a{color:#9ec5ff}
@media print{#mock-strip{display:none}}
"""

RUNTIME = """
(function () {
  var states = STATES;
  var params = new URLSearchParams(location.search);
  var key = params.get('state');
  var known = states.some(function (s) { return s.key === key; });
  if (!known) { key = states[0].key; }
  var template = document.getElementById('mock-state-' + key);
  var root = document.getElementById('mock-root');
  root.innerHTML = template.innerHTML;
  document.title = template.getAttribute('data-title') + ' (' + VERSION + ' mockup)';
  document.documentElement.setAttribute('data-mock-state', key);
  var strip = document.getElementById('mock-strip');
  var select = strip.querySelector('select');
  states.forEach(function (s) {
    var option = document.createElement('option');
    option.value = s.key; option.textContent = s.label; option.selected = s.key === key;
    select.appendChild(option);
  });
  select.addEventListener('change', function () {
    params.set('state', select.value);
    location.search = params.toString();
  });
  if (params.get('strip') === '0') { strip.removeAttribute('open'); }
  // No server behind a mockup: posts and application links stay on the page.
  document.addEventListener('submit', function (event) { event.preventDefault(); }, true);
  document.addEventListener('click', function (event) {
    var anchor = event.target.closest && event.target.closest('a[href]');
    if (!anchor || anchor.closest('#mock-strip')) { return; }
    var href = anchor.getAttribute('href') || '';
    if (href.charAt(0) === '/' || /^https?:/.test(href)) { event.preventDefault(); }
  }, true);
  window.fetch = function (url) {
    // A deferred section's body lives on the server; the mockup has none, so
    // its placeholder keeps its loading state (a known limit of the file).
    if (String(url).indexOf('/Section?') >= 0) { return new Promise(function () {}); }
    return Promise.resolve(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
  };
  window.__mockState = key;
})();
"""


def build(name: str, title: str, states: list[tuple[str, str, str, str | None]]) -> None:
    style_names: list[str] = []
    script_names: list[str] = []
    templates: list[str] = []
    for key, label, file, transform in states:
        html = read(CAPTURED / file)
        styles, scripts, inner = split(html)
        for s in styles:
            if s not in style_names:
                style_names.append(s)
        for s in scripts:
            if s not in script_names:
                script_names.append(s)
        if transform:
            inner = TRANSFORMS[transform](inner)
        inner = inline_images(inner)
        page_title = re.search(r"<title>(.*?)</title>", html, re.S)
        templates.append(
            f'<template id="mock-state-{key}" data-title="{(page_title.group(1) if page_title else title).strip()}">{inner}</template>'
        )
    css = "\n".join(css_with_fonts(s) for s in style_names)
    scripts_inline = "\n".join(
        "<script>" + asset_bytes(f"/js/{s}").decode("utf-8").replace("</script", "<\\/script") + "</script>"
        for s in script_names
    )
    state_json = json.dumps([{"key": k, "label": l} for k, l, _, _ in states])
    runtime = RUNTIME.replace("STATES", state_json).replace("VERSION", json.dumps(VERSION))
    strip = (
        '<details id="mock-strip" open><summary>Mockup controls</summary><div class="mock-body">'
        f"<small>{VERSION} · {title}. Demo control, not product UI. Captured from the as-built pages.</small>"
        '<label>State <select aria-label="Mockup state"></select></label>'
        "<small>Add <code>?state=&lt;key&gt;</code> to the address for a screenshot; <code>&amp;strip=0</code> folds this.</small>"
        "</div></details>"
    )
    document = (
        '<!DOCTYPE html>\n<html lang="en" data-busy-label="Working&#x2026;" data-busy-still="Still">\n<head>\n'
        '<meta charset="utf-8" />\n<meta name="viewport" content="width=device-width, initial-scale=1.0" />\n'
        f"<title>{title} ({VERSION} mockup)</title>\n<style>{css}</style>\n<style>{STRIP_CSS}</style>\n</head>\n<body>\n"
        '<div id="mock-root"></div>\n'
        + "\n".join(templates)
        + f"\n{strip}\n<script>{runtime}</script>\n{scripts_inline}\n</body>\n</html>\n"
    )
    (HERE / name).write_text(document, encoding="utf-8")
    print(f"{name}: {len(states)} states, {len(document) // 1024} KB")


def main() -> None:
    if not CAPTURED.exists():
        sys.exit(f"missing {CAPTURED}")
    for name, (title, states) in SURFACES.items():
        build(name, title, states)


if __name__ == "__main__":
    main()
