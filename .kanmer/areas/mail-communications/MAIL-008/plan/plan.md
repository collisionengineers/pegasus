# Plan — MAIL-008

Delivered on `task/mail-006-inbox-message-page` with [[MAIL-006]] (the labels
exist for that page's Decision card; shipping the redesign with raw slugs was
the alternative and is banned).

1. Implement `OperatorLabels.MailClassification` with an explicit word for
   every `ReceivedMailFamily` and `SentMailFamily`; subtype words are the
   full generated table below the fold in the sign-off list. `Other`
   categories render the operator's own name.
2. Route the picker (`MailClassificationSelection.Options`) and
   `DecisionLabel` through the map — one list, every renderer inherits.
3. **Sign-off gate:** the full family/subtype label table is presented to the
   operator before the PR merges (MAIL-008 body: the wording is the
   operator's; the mockups show proposals). Any corrections are applied on
   the same branch; the merge waits for the wording.
4. Coverage test: every enum member resolves to a non-slug label; kebab-case
   family names asserted absent from Mail page HTML.
5. Move-reason wording deferred: the reason row stays off the confirmation
   dialog until worded; recording is unchanged.
