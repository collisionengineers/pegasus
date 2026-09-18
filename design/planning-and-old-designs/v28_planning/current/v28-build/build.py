"""Assemble each v28 baseline surface from the live static assets on this
checkout plus a hand-authored page body. Run from the repository root:

    python design/planning-and-old-designs/v28_planning/current/v28-build/build.py [page ...]

With no arguments, builds every page listed in PAGES. Each page reads
`pages/<name>.body.html` (the page's <main> content, static HTML transcribed
from the live .cshtml with representative fixture data) and `pages/<name>.js`
(local interaction script: mockup-strip state switching, dialog wiring using
the shared mock-engine, tab/section toggles). The shell chrome (rail, utility
bar, working-set strip, shell dialogs), site.css, the page's own live
stylesheet(s), the Lucide sprite, the brand mark and the mock engine are
inlined so the output is one offline file with no server, build or network
dependency.

Inputs (read verbatim from src/Pegasus.Web): wwwroot/css/site.css, any
per-page css named in PAGES, Pages/Shared/_LucideSprite.cshtml,
wwwroot/fonts/inter/InterVariable.woff2, wwwroot/images/marks/pegasus-lockup.png.
Output: ../pegasus_<name>_v28.html
"""
from __future__ import annotations

import base64
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[5]
WEB = ROOT / "src" / "Pegasus.Web"
HERE = pathlib.Path(__file__).resolve().parent
OUTDIR = HERE.parent

# name -> (title, current nav key, extra live css files beyond site.css, navless)
PAGES: dict[str, tuple[str, str, list[str], bool]] = {
    "work_centre": ("Work Centre", "workcentre", ["work-centre.css"], False),
    "cases_index": ("Cases", "cases", ["cases-index.css"], False),
    "case_record": ("Case workspace", "cases", ["case-workspace.css"], False),
    "triage_unidentified": ("Triage & Unidentified", "cases", ["cases-index.css", "triage.css", "unidentified.css"], False),
    "image_intake": ("Vehicle images", "cases", ["pre-case-images.css", "image-record.css"], False),
    "mail_upload": ("Inbox & Upload", "mail", ["inbox.css"], False),
    "search_operations": ("Search & Operations", "search", [], False),
    "administration": ("Administration", "administration", ["admin.css", "logs.css"], False),
    # Navless family: _LayoutAuth's external card frame, not the app shell
    # (docs/design/README.md "_LayoutAuth remains the navless frame").
    "account_shell": ("Account", "none", [], True),
}


def data_uri(path: pathlib.Path, mime: str) -> str:
    return f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def main(names: list[str]) -> None:
    head = subprocess.run(
        ["git", "rev-parse", "--short=9", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True
    ).stdout.strip()
    site_css = (WEB / "wwwroot" / "css" / "site.css").read_text(encoding="utf-8")
    font = data_uri(WEB / "wwwroot" / "fonts" / "inter" / "InterVariable.woff2", "font/woff2")
    site_css = site_css.replace("url(../fonts/inter/InterVariable.woff2)", f"url({font})")
    site_css = re.sub(r"@font-face\{font-family:Inter;font-style:italic;[^}]*\}\n?", "", site_css)
    sprite = (WEB / "Pages" / "Shared" / "_LucideSprite.cshtml").read_text(encoding="utf-8")
    sprite = re.sub(r"@\*.*?\*@\s*", "", sprite, flags=re.S)
    brand = data_uri(WEB / "wwwroot" / "images" / "marks" / "pegasus-lockup.png", "image/png")
    logo = data_uri(WEB / "wwwroot" / "images" / "logo_no_margin.png", "image/png")
    shell = (HERE / "shell-chrome.html").read_text(encoding="utf-8")
    navless = (HERE / "navless-chrome.html").read_text(encoding="utf-8")
    engine = (HERE / "mock-engine.js").read_text(encoding="utf-8")

    targets = names or list(PAGES.keys())
    for name in targets:
        if name not in PAGES:
            raise SystemExit(f"unknown page {name!r}; choices: {sorted(PAGES)}")
        title, nav, extra_css, is_navless = PAGES[name]
        body_path = HERE / "pages" / f"{name}.body.html"
        js_path = HERE / "pages" / f"{name}.js"
        strip_path = HERE / "pages" / f"{name}.strip.html"
        if not body_path.exists():
            print(f"skip {name}: {body_path.relative_to(ROOT)} not written yet")
            continue
        page_css = "\n".join(
            (WEB / "wwwroot" / "css" / css_name).read_text(encoding="utf-8") for css_name in extra_css
        )
        body = body_path.read_text(encoding="utf-8")
        page_js = js_path.read_text(encoding="utf-8") if js_path.exists() else ""
        strip = strip_path.read_text(encoding="utf-8") if strip_path.exists() else "<!-- no per-route strip rows yet -->"

        out = navless if is_navless else shell
        out = out.replace("/*@@SITE_CSS@@*/", site_css)
        out = out.replace("/*@@PAGE_CSS@@*/", page_css)
        out = out.replace("<!--@@SPRITE@@-->", sprite.strip())
        out = out.replace("/*@@BRAND@@*/", brand)
        out = out.replace("/*@@LOGO@@*/", logo)
        out = out.replace("@@TITLE@@", title)
        out = out.replace("@@NAV@@", nav)
        out = out.replace("@@HEAD@@", head)
        out = out.replace("<!--@@BODY@@-->", body)
        out = out.replace("<!--@@STRIP@@-->", strip)
        out = out.replace("/*@@ENGINE@@*/", engine)
        out = out.replace("/*@@PAGE_JS@@*/", page_js)

        leftovers = re.findall(r"@@[A-Z_]+@@", out)
        if leftovers:
            raise SystemExit(f"{name}: unreplaced placeholders: {leftovers}")
        out_path = OUTDIR / f"pegasus_{name}_v28.html"
        out_path.write_text(out, encoding="utf-8")
        print(f"wrote {out_path.relative_to(ROOT)} ({out_path.stat().st_size:,} bytes) from {head}")


if __name__ == "__main__":
    main(sys.argv[1:])
