# Research

Verified at the current shared branch: every thread link preserves `SearchTerm`, but both `OnGetAsync` and `ReloadAsync` set `OutsideListScope` only from folder/mailbox. `GetRetainedMail`/`IRetainedMailQueries.GetAsync` do not accept the active term, although the EF store already has one match-grouping path. The smallest reusable fix is to pass the optional term through the existing detail query and populate the detail summary's existing `Matches`; the page then treats a searched detail with no matches as outside the originating view. No separate membership service is needed.
