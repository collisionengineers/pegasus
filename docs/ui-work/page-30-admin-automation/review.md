# Page 30 — Administration / Automation: review

Source: `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml`.
Screenshot: `automation.png`. Governing standards: `../ui-standards-and-review.md`
(§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

- The lede — *"One named Automation actor may call approved ordinary case, intake, and
  document actions through its own authenticated machine ingress. It is not a staff
  account, holds no administration authority, and every action it takes is permanently
  recorded. Client secrets are never shown here."* — packs three banned or dev-speak terms
  into two sentences ("intake", "ingress", "actor") and closes with self-negating
  narration ("Client secrets are never shown here" — a page should not disclaim its
  contents). "Approved ordinary case … actions" is specification language, not English.
- The inert-state copy filling the Client registration panel — *"The Automation ingress is
  not composed in this deployment: the configuration gate is off, no endpoint or token
  route exists, and no automation action is possible."* — is architecture narrated at the
  operator: "ingress", "composed", "configuration gate", "token route" in a single
  sentence. The screenshot shows this is the page's entire content today.
- The Activity panel repeats the lede's audit promise a third time — *"Every Automation
  action and every denied automation request is recorded in permanent history with a
  correlation identifier."* — using the banned term "correlation identifier" (standards
  §2: show "Reference").

## 2. Practicality

- **The page is permanently inert in every current deployment.** The composition gate is
  off everywhere Pegasus runs today, so every administrator who opens this page reads a
  paragraph explaining why nothing is here. Standards §4.9 is explicit: capabilities that
  are not composed in a deployment are **absent, not narrated**. The card on the
  Administration index and this page itself should simply not render when the feature is
  off — the current design ships a whole page whose only job is to say it does not exist.
- When the feature *is* on, the panel is serviceable (identity, state chip, scopes,
  enable/disable with reason) but the scopes render as a raw comma join of
  `registration.GrantedScopes` — machine scope strings on an operator screen, the same
  raw-value failure as the Mailboxes route column.
- The disable consequence text — *"Disabling refuses new tokens immediately and rejects
  requests that present an already-issued token within seconds. Both changes are recorded
  as attributable administrator actions."* — is the right fact in token-plumbing
  vocabulary, positioned *below* the button it explains rather than above it.
- "Client identifier" displays a machine id; useful for matching against external
  configuration, but it outranks the human display name in the current order.

## 3. Performance / Design / Good practice

- The enable/disable toggle posts `TargetEnabled` computed from current state — a
  last-writer race if two administrators act simultaneously; there is no expected-version
  field on this form, unlike its sibling Administration forms.
- `OperationKey` idempotency present — good.
- The status chip partial receives literal `"Enabled"`/`"Disabled"` strings — acceptable,
  consistent with the labelling rule.
- The Activity link panel is an entire section for one hyperlink plus a repeated audit
  sentence; a link row on the registration panel does the same job in one line.
- Heading stack: eyebrow "ADMINISTRATION" + H1 "Automation" + uppercase section labels —
  more chrome than content on a page with one panel (standards §4.7).
