# Research — density scope

## Question

Should density auto-fit apply to every integrated report body?

## Findings

1. The workspace engine already models density as a per-template descriptor, not a global rule. Only `market-valuation-evidence` declares `FitToPages`; every other current template resolves Auto to Normal and flows naturally.
2. Workspace ADR-0007 intentionally limits the Normal→Compact→Ultra ladder to templates with an accepted page target. Applying it universally would add up to three Chromium renders and silently change type density without template-specific acceptance.
3. The approved initial activation excludes the market-valuation/advert family and activates only rendererref1 assessment plus fee note. Rendererref1 specifies fixed house styling and sample outputs but no universal page target; photos have no fixed layout rule and the report must render what the system supplies.
4. Exact visual/sample parity is stronger than a speculative global shrink-to-fit policy. Overflow should produce clean additional pages, never clipping or automatic density changes absent an accepted target.
5. Density remains useful engine mechanics for a future governed template that explicitly owns a page target; it is not a caller option in the automatic Pegasus workflow.

## Implications

- Initial assessment/fee-note templates render at their accepted Normal/default styling and flow across pages.
- Preserve density mechanics only as internal per-template capability; do not expose a user/API/MCP density parameter.
- Auto-fit activates only when a future accepted template descriptor specifies a tested page target.
- Stress-test maximum repair lists/photos and compare against approved rendererref1 samples.
