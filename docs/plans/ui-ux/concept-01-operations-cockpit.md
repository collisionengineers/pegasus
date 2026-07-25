# Concept 1: operations cockpit

![Operations cockpit](mockups/concept-01-operations-cockpit.png)

## Intent

Answer the start-of-day question in a few seconds: where is work accumulating, what is due, and what needs intervention?

## Keep

- Queue hierarchy remains close to the operator-supplied dashboard reference.
- Left navigation makes the application feel like one workspace rather than a collection of pages.
- Due and chaser lists reveal actual work, not only counts.
- Recent activity helps a small office coordinate without a separate monitoring screen.

## Change before implementation

- Replace all invented sample references/providers with approved local fixtures.
- Navigation must reflect actual first-MVP scope; remove Reports, Insights, or other items until a real caller exists.
- Implement the settled queue split: Review is complete work awaiting approval; Held is a reasoned manual pause that blocks progression and chasers while keeping the due date visible.
- Add the manual Blocked intake inbox filter with its reason, warning, and retry path; it must never look like a created case.
- Keep `Triage` as a separate business workflow rather than an inbox category.
- Add `In today` for cases created since Europe/London midnight, kept distinct from `Due today`. Replace ambiguous submitted/cleared activity with `Sent to Engineer` and `Reports sent`, each showing today and this-week totals at Europe/London midnight and Monday boundaries. Explain that first-MVP `Sent to Engineer` means first successful EVA export generation and not receipt; `Reports sent` requires exact approved-mailbox Sent-item evidence.
- Add separate Triage navigation and a count/link to its own workflow without making it an inbox category or giving it due/chaser treatment.
- Design unavailable/stale counts and refresh failure, not just populated success.
- Avoid a notification centre unless real in-product notifications are in scope.

## Deferred-capability impact

The [UI planning impact register](README.md#deferred-capability-impact) applies. This concept constrains future additions to named role-aware Core queries and truthful evidence-backed counts; it does not authorise analytics, automatic email/WhatsApp, AI prioritisation, finance tiles, external-user views or dormant navigation. Later activity tiles require a stable source event, exact time boundary, caller and operator acceptance.
