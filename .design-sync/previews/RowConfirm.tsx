import { RowConfirm } from '@pegasus/design-system';

/** Open: the reason row is visible beneath the disclosure, confirm in dark. */
export const Open = () => (
  <div style={{ maxWidth: 480 }}>
    <RowConfirm summary="Withdraw link" reasonId="withdraw-reason-1" confirm="Withdraw link" open />
  </div>
);

/** Closed: only the `.btn` summary shows until the operator decides to act. */
export const Closed = () => <RowConfirm summary="Withdraw link" reasonId="withdraw-reason-2" confirm="Withdraw link" />;
