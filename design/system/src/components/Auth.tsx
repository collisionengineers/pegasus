import type { HTMLAttributes, ReactNode } from 'react';
import { cx } from '../cx';
import { Icon } from './Icon';

/** `.auth-shell` — the navless, centred, full-height paper ground for sign-in and status cards. */
export function AuthShell({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cx('auth-shell', className)} {...rest}>
      <main id="main-content" tabIndex={-1}>
        {children}
      </main>
    </div>
  );
}

export interface AuthCardProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  /** The card's H1, e.g. `Sign in to Pegasus`. */
  title: ReactNode;
  /** Renders the title with a green tick (`.auth-card__done`) — confirmed completion such as `You are signed out`. */
  done?: boolean;
  /** Small red uppercase mark above the title (default `COLLISION ENGINEERS`). */
  mark?: ReactNode;
  /** Wider (34rem) card for forms with more fields. */
  wide?: boolean;
  /** Red left rail for a fault: error, not found, access denied. */
  fault?: boolean;
  /** Body: a paragraph, a form (inputs nested inside labels), `.auth-card__actions`. */
  children?: ReactNode;
  /** Footer line under a hairline (`.auth-card__foot`), e.g. the support reference. */
  foot?: ReactNode;
}

/**
 * `.auth-card` — the single centred card family: sign in, signed out, password
 * change, access denied, error, not found.
 */
export function AuthCard({ title, done, mark = 'COLLISION ENGINEERS', wide, fault, foot, className, children, ...rest }: AuthCardProps) {
  return (
    <div className={cx('auth-card', wide && 'auth-card--wide', fault && 'auth-card--fault', className)} role={fault ? 'alert' : undefined} {...rest}>
      {mark ? <p className="auth-card__mark">{mark}</p> : null}
      {done ? (
        <h1 className="auth-card__done">
          <Icon name="check-circle" size="lg" />
          <span>{title}</span>
        </h1>
      ) : (
        <h1>{title}</h1>
      )}
      {children}
      {foot ? <p className="auth-card__foot">{foot}</p> : null}
    </div>
  );
}

/** `.auth-card__actions` — stacked full-width actions inside an `AuthCard`. */
export function AuthCardActions({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('auth-card__actions', className)} {...rest} />;
}

export interface SupportReferenceProps extends HTMLAttributes<HTMLSpanElement> {
  /** The request id shown in `<code>`. */
  reference: string;
  onCopy?: () => void;
}

/** `.auth-card__reference` — the support reference code with a Copy `.btn`; use as `AuthCard.foot` content after "Support reference". */
export function SupportReference({ reference, onCopy, className, ...rest }: SupportReferenceProps) {
  return (
    <span className={cx('auth-card__reference', className)} {...rest}>
      <code>{reference}</code>
      <button type="button" className="btn" onClick={onCopy}>
        Copy
      </button>
    </span>
  );
}
