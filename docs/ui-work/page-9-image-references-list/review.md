# Page 9 — Vehicle images list (today: the "Image in·takes" screen) — review

> Vocabulary note. The legacy pipeline term is banned from every deliverable in this folder set,
> including this review. Wherever current copy, filenames, or code identifiers must be quoted,
> the term is written `in·take` (with an interpunct) so the zero-occurrence check stays clean.
> The two captures in this folder are the empty-list capture and the single-row capture; the
> source is the list page under `src/Pegasus.Web/Pages/ImageIn·take/Index.cshtml`.

What the screen is: the list of pre-case image records keyed by a confirmed registration.
Today it is titled "Image in·takes", carries the lede "Pre-Case records for image-only material
with a confirmed registration. An Image in·take is never a Case; association keeps both
identities permanently.", offers a search panel labelled "Image In·take Reference or vehicle
registration", three filter links (All / Awaiting instruction / Associated with Case), and a
row list. It has no navigation entry.

## 1. Aesthetics

- **The eyebrow repeats the H1 verbatim.** "IMAGE IN·TAKES" in uppercase tracking sits 30px above
  "Image in·takes" in bold. Two renderings of the same two words, zero added information.
- **The lede is doctrine, not guidance.** "An Image in·take is never a Case; association keeps
  both identities permanently." is a domain-model assertion printed at every operator on every
  visit. Nothing on the page asks the operator to make the mistake this sentence warns against.
- **The primary red is spent on "Search".** A full-width `#DB0816` button — the strongest visual
  in the whole viewport — commits nothing. Red is the commitment colour; a query is not a
  commitment. The single-row capture shows the red Search button dominating a page whose actual
  content is one quiet row.
- **The search panel is arbitrarily narrow and the rest of the viewport is empty paper.** At
  1585px wide, roughly two-thirds of the screen is unused; the search card stops mid-page for no
  layout reason.
- **The filter links read as three unrelated buttons.** "All", "Awaiting instruction",
  "Associated with Case" are white pills with no active state — the capture gives no way to tell
  which filter is currently applied.
- **The row hierarchy is inverted on the right side.** Each row renders the state as a bold
  pseudo-heading ("Image in·take registered") with the received date as its caption. A state is
  a fact, not a headline; it belongs in a chip. The sentence-length state string also cannot be
  scanned down a column.

## 2. Practicality

- **The screen is orphaned.** It appears nowhere in the navigation (Operations · In·take ·
  Triage · Cases · Administration · Search in the capture) and is reachable only about four
  clicks deep via a received item's record. An operator cannot answer "what image material is
  waiting for instructions?" without already knowing the URL. For a queue whose whole purpose is
  *waiting work*, that is disqualifying.
- **The search field leads with internal grammar.** "Image In·take Reference or vehicle
  registration" (note the drifting capitalisation) asks first for a reference format the
  operator would have to learn (`SD74CXS-01`), when the thing they actually hold is a
  registration.
- **The filters carry no counts.** To learn how many records are "Awaiting instruction" the
  operator must click the filter and count rows by eye.
- **The state filter names disagree with the row state.** The filter says "Awaiting instruction";
  the row for the same record says "Image in·take registered". Two labels for one state on one
  screen.
- **The empty state is internal and ambiguous.** "No Image in·takes match this view." uses the
  banned term, uses "view" (an implementation word), and does not distinguish "nothing exists"
  from "nothing matches your search or filter" — very different operational answers.
- **No ordering or paging affordance.** Rows show a received timestamp but nothing says how the
  list is ordered or what happens beyond one page.

## 3. Performance / Design / Good practice

- **Vocabulary violates the settled standard.** The operator's own statement is that this
  pipeline vocabulary does not describe the business; §2 of `../ui-standards-and-review.md` bans
  the term from every user-facing surface, and this page uses it in the `<title>`, eyebrow, H1,
  lede, search label, filter `aria-label`, empty state, and row state.
- **The filter links have no `aria-current`** and no visual selected state, so the applied filter
  is unannounced to assistive tech as well as sighted users.
- **The row state string is produced as prose, not through a label map**, so the same state
  cannot be re-rendered as a chip here or reused on the detail page without divergence — and
  indeed the detail page already words it differently.
- **The search form and filter links each preserve the other's parameter by hidden field / route
  value**, which works, but means three near-identical URL permutations for one screen state;
  a single form with the filter as a parameter would be simpler and bookmarkable.
- **Timestamp is rendered by two separate `ToLocalTime()` conversions** for date and time of the
  same instant — harmless today, but a pattern that invites a midnight-boundary mismatch.
