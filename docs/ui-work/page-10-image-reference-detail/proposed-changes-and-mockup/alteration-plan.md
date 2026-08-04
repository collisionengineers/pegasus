# Page 10 — Image reference detail — alteration plan

> Vocabulary note: the legacy term is written `in·take` where current identifiers must be named.

## Review summary

The current detail page gives its best real estate to four rows of GUIDs, tokens, and hashes,
renders its state as a two-clause lede sentence, shows case candidates the operator cannot act
on, and pushes the page's only meaningful action ("link these images to a case") through a
detour to the origin receipt identified by a raw GUID. The reading-results panel prints engine
keys and enum dispositions instead of the one fact that matters.

## Changes

1. **Title and heading**: eyebrow "IMAGE IN·TAKE" + H1 "SD74CXS-01" + lede → single H1
   **"Image reference AB12CDE-01"** with a state chip beside it (**Awaiting instruction** /
   **Linked to Case 26001**). `<title>` follows. Back link → **"Vehicle images"**.
2. **Record panel** → **record facts** only: Registration, Received (date and time), Linked
   case (reference link, or "Not linked"). The reference itself lives in the H1 and is not
   repeated as a row.
3. **"Preserved origin" panel** → a single **origin line with an action**: "From a manual
   upload received 4 Aug 2026 14:01 — **View original upload**". Receipt GUID, receipt token,
   source hash, and revision GUID are removed from the markup entirely (they remain queryable
   internally). The channel string passes through a label map ("Manual upload", "E-mail").
4. **Doctrine copy relocated**: "Association, reversal and re-linking are reasoned actions…" →
   deleted; one consequence sentence sits inline on the link action only: "Linking keeps this
   image reference permanently; it can be reversed before the report is sent."
5. **"Eligible case candidates"** → **actionable rows**: each candidate shows case reference,
   principal, registration, and two buttons — **Open case** and **Link to this case** (the link
   action opens a confirm dialog asking for the required reason). No "· version N" suffixes.
6. **"Registration reading results"** → **plain confidence rows**: "AB12 CDE — high confidence
   — 4 Aug 2026" / "No registration could be read from one image — 4 Aug 2026". Engine key,
   engine version, and raw disposition strings are removed; a settled label map covers the
   outcome wording.
7. **State wording unified** with page 9's chips: exactly "Awaiting instruction" and "Linked to
   Case NNNNN" everywhere; the three current variants collapse to one.
8. **Empty/failure states designed**: no candidates — "No open case matches this registration
   yet."; no readings — "No registration readings have been recorded."

## Dependencies

- Change 5 is the significant one: today association is performed only on the origin receipt.
  Making "Link to this case" work here requires either (a) the detail page posting to the
  existing origin-receipt handler on the operator's behalf, or (b) a new handler on this page
  calling the same Core operation. Either way the Core policy is unchanged — this is a
  presentation-route decision that needs an engineering decision recorded before build.
- Shared state-label map with page 9 (one source of truth for chip wording).
- Channel and reading-outcome label maps.
- Case candidates query already exists (`AssociationCandidates`); it must additionally expose
  principal for the row rendering.

## Open questions

- May "Link to this case" execute directly from this page (with reason dialog), or does the
  operator process genuinely require seeing the original upload first? If the latter, the
  button becomes "Review and link" and lands on the upload with the case pre-selected.
- Unlinking: offered here as a secondary action on the linked state, or only from the original
  upload? Proposed: only from the linked state here, same confirm-with-reason dialog.
- Should reading confidence be worded (high/low) or numeric? Proposed: worded; numbers imply a
  precision the reader model does not promise.
