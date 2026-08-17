import type { HTMLAttributes, ReactNode } from 'react';
import { cx } from '../cx';
import { Icon } from './Icon';
import { ButtonRow, PrimaryAction, SecondaryAction } from './Actions';

export interface ReasonDialogProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  /** Whether the dialog is shown (`hidden` otherwise). */
  open?: boolean;
  /** Dialog title, e.g. `Close this case as Created in error?` */
  title: ReactNode;
  /** The consequence the operator must understand — one sentence, rendered as an amber `.notice`. */
  consequence?: ReactNode;
  /** `id` prefix for title / reason / hint ids. */
  id?: string;
  /** Confirm button text (default `Confirm Action`). */
  confirmLabel?: ReactNode;
  cancelLabel?: ReactNode;
  onCancel?: () => void;
  onConfirm?: (reason: string) => void;
  /** Extra fields rendered above the button row (inside the `.form-grid`). */
  children?: ReactNode;
  /** Renders the dialog in flow (no fixed backdrop) — for previews and embedded confirmations. */
  inline?: boolean;
}

/**
 * `.reason-dialog-backdrop` > `.reason-dialog` — the modal that collects a
 * business reason before a consequential action. Title with warning glyph, the
 * consequence notice, a required Reason textarea, Cancel + Confirm.
 */
export function ReasonDialog({
  open = true,
  title,
  consequence,
  id = 'reason-dialog',
  confirmLabel = 'Confirm Action',
  cancelLabel = 'Cancel',
  onCancel,
  onConfirm,
  children,
  inline,
  className,
  style,
  ...rest
}: ReasonDialogProps) {
  return (
    <div
      id={id}
      className={cx('reason-dialog-backdrop', className)}
      role="dialog"
      aria-modal="true"
      aria-labelledby={`${id}_title`}
      hidden={!open}
      style={inline ? { position: 'static', padding: 0, background: 'transparent', ...style } : style}
      {...rest}
    >
      <div className="reason-dialog">
        <h2 id={`${id}_title`} className="reason-dialog__title">
          <Icon name="alert-triangle" />
          <span>{title}</span>
        </h2>
        {consequence ? <p className="notice">{consequence}</p> : null}
        <form
          method="post"
          className="form-grid"
          onSubmit={(e) => {
            e.preventDefault();
            const reason = (e.currentTarget.elements.namedItem('Reason') as HTMLTextAreaElement | null)?.value ?? '';
            onConfirm?.(reason);
          }}
        >
          <div className="field-wide">
            <label htmlFor={`${id}_reason`}>
              <span>
                Reason for action{' '}
                <span className="field-validation" aria-hidden="true">
                  *
                </span>
              </span>
            </label>
            <textarea id={`${id}_reason`} name="Reason" rows={3} required aria-describedby={`${id}_reason_hint`} placeholder="Enter clear business reason…" />
            <small id={`${id}_reason_hint`}>Required.</small>
          </div>
          {children}
          <div className="field-wide">
            <ButtonRow end>
              <SecondaryAction type="button" onClick={onCancel}>
                {cancelLabel}
              </SecondaryAction>
              <PrimaryAction type="submit">{confirmLabel}</PrimaryAction>
            </ButtonRow>
          </div>
        </form>
      </div>
    </div>
  );
}
