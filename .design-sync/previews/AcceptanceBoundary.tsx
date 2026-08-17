import { AcceptanceBoundary } from '@pegasus/design-system';

/** Names what this surface does not yet prove. */
export const NotYetProved = () => (
  <div style={{ maxWidth: 640 }}>
    <AcceptanceBoundary title="What this screen does not prove">
      Figures here are counted from the Inbox and Cases lists. Whether an e-mail was received but never shown is not covered by this page.
    </AcceptanceBoundary>
  </div>
);

/** With more than one paragraph. */
export const TwoParagraphs = () => (
  <div style={{ maxWidth: 640 }}>
    <AcceptanceBoundary title="Read alongside the export">
      <p>The EVA export is prepared by hand. This total counts cases marked Completed, not cases sent.</p>
      <p>Refresh after the morning run before quoting a number to a principal.</p>
    </AcceptanceBoundary>
  </div>
);
