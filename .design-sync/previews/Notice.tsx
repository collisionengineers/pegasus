import { Notice } from '@pegasus/design-system';

/** One consequence the operator must understand before acting. */
export const Consequence = () => (
  <div style={{ maxWidth: 640 }}>
    <Notice>Closing this case as Created in error retires the reference CE-2026-01432. It will not be reused.</Notice>
  </div>
);

/** Above a form: what the action will and will not do. */
export const AboveForm = () => (
  <div style={{ maxWidth: 640 }}>
    <Notice>
      <p style={{ margin: 0 }}>
        Reopening needs a reason. The case returns to <b>Review</b> and the previous completion stays in History.
      </p>
    </Notice>
    <label style={{ display: 'grid', gap: 4, fontSize: '.875rem', fontWeight: 700 }}>
      Reason
      <textarea rows={3} style={{ font: 'inherit', fontWeight: 400, padding: 6 }} defaultValue="Repairer supplied a revised estimate on 14 Aug." />
    </label>
  </div>
);
