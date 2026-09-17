"""Assemble the v27 baseline mockup from the live static assets on this
checkout plus the hand-authored source. Run from the repository root:

    python design/planning-and-old-designs/v27_planning/current/v27-build/build.py

Inputs (read verbatim): wwwroot/css/site.css, wwwroot/css/case-workspace.css,
Pages/Shared/_LucideSprite.cshtml, wwwroot/fonts/inter/InterVariable.woff2,
wwwroot/images/marks/pegasus-lockup.png. Output: ../pegasus_case_record_v27.html.
"""
from __future__ import annotations

import base64
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[5]
WEB = ROOT / "src" / "Pegasus.Web"
HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent / "pegasus_case_record_v27.html"

REASON_DIALOG = """<div id="{id}" class="dialog-backdrop" data-dialog="{id}" data-reason-dialog hidden>
  <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="{id}_title">
    <div class="dialog-head"><h2 id="{id}_title" tabindex="-1">{title}</h2><button type="button" class="dialog-close" data-dialog-close aria-label="Close dialog"><svg class="icon" aria-hidden="true"><use href="#icon-x" /></svg></button></div>
    <form method="post" data-mock-action="{action}">
      <div class="dialog-body stack">
        <div class="field"><label class="req" for="{id}_reason">Reason for action</label><textarea id="{id}_reason" name="Reason" rows="3" required maxlength="500"></textarea></div>
      </div>
      <div class="dialog-foot"><button type="button" class="btn" data-dialog-close data-dialog-dismiss>Cancel</button><button type="submit" class="btn btn--primary">Confirm Action</button></div>
    </form>
  </section>
</div>"""

REASON_ACTIONS = {
    "case-release-hold-dialog": "release-hold",
    "case-complete-dialog": "mark-completed",
    "case-return-review-dialog": "return-to-review",
    "case-return-engineer-dialog": "return-to-engineer",
    "case-archive-dialog": "archive",
    "case-unlink-evidence-dialog": "unlink-evidence",
}


def data_uri(path: pathlib.Path, mime: str) -> str:
    return f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii")


def main() -> None:
    head = subprocess.run(["git", "rev-parse", "--short=9", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
    src = (HERE / "case-record.src.html").read_text(encoding="utf-8")
    site_css = (WEB / "wwwroot" / "css" / "site.css").read_text(encoding="utf-8")
    case_css = (WEB / "wwwroot" / "css" / "case-workspace.css").read_text(encoding="utf-8")
    font = data_uri(WEB / "wwwroot" / "fonts" / "inter" / "InterVariable.woff2", "font/woff2")
    # The upright face inlined; the italic face dropped (no italic on the record).
    site_css = site_css.replace("url(../fonts/inter/InterVariable.woff2)", f"url({font})")
    site_css = re.sub(r"@font-face\{font-family:Inter;font-style:italic;[^}]*\}\n?", "", site_css)
    sprite = (WEB / "Pages" / "Shared" / "_LucideSprite.cshtml").read_text(encoding="utf-8")
    sprite = re.sub(r"@\*.*?\*@\s*", "", sprite, flags=re.S)
    brand = data_uri(WEB / "wwwroot" / "images" / "marks" / "pegasus-lockup.png", "image/png")
    # v27 proposal: the refined mark (operator, 16 September), cropped and resized by
    # the build folder's one-off Pillow step to 128px beside the live 128px lockup.
    refined = data_uri(HERE.parent.parent / "logo" / "pegasus-mark-refined-128.png", "image/png")
    script = (HERE / "case-record.js").read_text(encoding="utf-8").replace("/*@@PROPOSALS@@*/", (HERE / "proposals.js").read_text(encoding="utf-8") + "\n" + (HERE / "wording.js").read_text(encoding="utf-8"))

    out = src
    out = out.replace("/*@@FONT@@*/", "/* Inter Variable is inlined into site.css below (OFL 1.1). */")
    out = out.replace("5765a527a", head)
    out = out.replace("/*@@SITE_CSS@@*/", site_css)
    out = out.replace("/*@@CASE_CSS@@*/", case_css)
    out = out.replace("<!--@@SPRITE@@-->", sprite.strip())
    out = out.replace("/*@@BRAND@@*/", brand).replace("/*@@BRAND_REFINED@@*/", refined)
    out = out.replace("/*@@SCRIPT@@*/", script)

    def reason(match: re.Match[str]) -> str:
        dialog_id, title = match.group(1), match.group(2)
        return REASON_DIALOG.format(id=dialog_id, title=title, action=REASON_ACTIONS.get(dialog_id, "reason-" + dialog_id))

    out = re.sub(r"<!--@@REASON:([^|]+)\|([^@]+)@@-->", reason, out)
    leftovers = re.findall(r"@@[A-Z_]+@@", out)
    if leftovers:
        raise SystemExit(f"unreplaced placeholders: {leftovers}")
    OUT.write_text(out, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size:,} bytes) from {head}")


if __name__ == "__main__":
    main()
