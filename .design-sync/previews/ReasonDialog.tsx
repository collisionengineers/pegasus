import { ReasonDialog } from '@pegasus/design-system';

/** The modal that collects a business reason before a consequential action; the consequence is an amber notice. Rendered `inline` for the preview. */
export const CloseAsCreatedInError = () => (
  <ReasonDialog
    inline
    id="close-in-error"
    title="Close this case as Created in error?"
    consequence="The case closes and its reference is never reused. Name the replacement case in the reason."
    confirmLabel="Close case"
  />
);

/** A reason without a stated consequence — the title alone carries the question. */
export const ReopenCase = () => <ReasonDialog inline id="reopen-case" title="Reopen case CE-2026-01432?" confirmLabel="Reopen case" />;
