import type { HTMLAttributes, ReactNode } from 'react';
import { cx, type StateName } from '../cx';
import { Icon, type IconName } from './Icon';
import { StatusChip } from './StatusChip';

export interface StatusCardProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /**
   * `info` (navy, default) for in-flight explanation; `attention` (amber) for
   * incomplete/pending; `error` (red) for failure; `done` (green tick, ink
   * text) confirms an action completed on another page.
   */
  variant?: 'info' | 'attention' | 'error' | 'done';
  /** Optional heading rendered as `<h2>` (not used by `done`). */
  title?: ReactNode;
  children: ReactNode;
}

/**
 * `.status-card` — a left-railed feedback card. Every state also carries text
 * (and for `done`, an icon), so nothing is conveyed by colour alone.
 */
export function StatusCard({ variant = 'info', title, className, children, role, ...rest }: StatusCardProps) {
  if (variant === 'done') {
    return (
      <p className={cx('status-card', 'status-card--done', className)} role={role ?? 'status'} {...rest}>
        <Icon name="check-circle" />
        <span>{children}</span>
      </p>
    );
  }
  return (
    <section className={cx('status-card', `status-card--${variant}`, className)} role={role} {...rest}>
      {title ? <h2>{title}</h2> : null}
      {typeof children === 'string' ? <p>{children}</p> : children}
    </section>
  );
}

/** `.notice` — an amber note above a form or list: one consequence the operator must understand. */
export function Notice({ className, children, ...rest }: HTMLAttributes<HTMLElement> & { children: ReactNode }) {
  return (
    <aside className={cx('notice', className)} {...rest}>
      {typeof children === 'string' ? <p style={{ margin: 0 }}>{children}</p> : <div style={{ display: 'grid', gap: 'var(--sp-2)' }}>{children}</div>}
    </aside>
  );
}

export interface AcceptanceBoundaryProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  title: ReactNode;
  children: ReactNode;
}

/** `.acceptance-boundary` — an amber block naming what this surface does not yet prove. */
export function AcceptanceBoundary({ title, className, children, ...rest }: AcceptanceBoundaryProps) {
  return (
    <section className={cx('acceptance-boundary', className)} {...rest}>
      <h2>{title}</h2>
      {typeof children === 'string' ? <p>{children}</p> : children}
    </section>
  );
}

export type RefreshStatus = 'current' | 'loading' | 'stale' | 'partial' | 'unavailable' | 'failed';

const REFRESH_LABEL: Record<RefreshStatus, string | null> = {
  current: null,
  loading: 'Refreshing',
  stale: 'Stale',
  partial: 'Partial',
  unavailable: 'Unavailable',
  failed: 'Failed',
};

export interface RefreshProps extends HTMLAttributes<HTMLDivElement> {
  /** Last successful load, already formatted for the operator, e.g. `14 Aug 09:32`. Omit for "Never updated". */
  updatedAt?: string;
  /** Zone label after the time; `London` unless the platform cannot resolve Europe/London. */
  zone?: string;
  /** Query freshness. Only states that are not `current` earn a chip. */
  status?: RefreshStatus;
  /** Called when the refresh control is pressed. */
  onRefresh?: () => void;
}

/**
 * `.refresh` — the compact corner element: last-good time, a chip only when
 * the query is not current, and the manual refresh button. Ported from
 * `_FreshnessBanner.cshtml`.
 */
export function Refresh({ updatedAt, zone = 'London', status = 'current', onRefresh, className, ...rest }: RefreshProps) {
  const chip = REFRESH_LABEL[status];
  return (
    <div className={cx('refresh', status === 'loading' && 'is-refreshing', className)} role="status" aria-live="polite" {...rest}>
      {updatedAt ? (
        <span>
          Updated <time>{updatedAt}</time> {zone}
        </span>
      ) : (
        <span>Never updated</span>
      )}
      {chip ? <StatusChip state={chip} /> : null}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onRefresh?.();
        }}
      >
        <button type="submit" title="Refresh" aria-label="Refresh">
          <Icon name="refresh-cw" spin />
          <span className="vh">Refresh</span>
        </button>
      </form>
    </div>
  );
}

export interface FreshnessBannerProps extends HTMLAttributes<HTMLDivElement> {
  /** `stale` amber, `loading`/`refreshing` navy, `failed` red; default hairline. */
  status?: 'current' | 'loading' | 'stale' | 'failed';
  /** Left content, usually "Updated <time> London" plus a chip. */
  children: ReactNode;
  /** Right-hand control, typically a refresh button. */
  action?: ReactNode;
}

/** `.freshness-banner` — the full-width freshness strip (older screens; new screens use `Refresh`). */
export function FreshnessBanner({ status = 'current', action, className, children, ...rest }: FreshnessBannerProps) {
  return (
    <div className={cx('freshness-banner', status !== 'current' && `freshness-banner--${status}`, status === 'loading' && 'is-refreshing', className)} role="status" {...rest}>
      <div className="updated">{children}</div>
      {action}
    </div>
  );
}

export interface ValidationSummaryProps extends HTMLAttributes<HTMLDivElement> {
  /** Heading above the list; the real summary reads "Please correct the following". */
  heading?: ReactNode;
  errors: ReactNode[];
}

/** `.validation-summary-errors` — the red-railed form error summary the tag helper emits. */
export function ValidationSummary({ heading, errors, className, ...rest }: ValidationSummaryProps) {
  if (!errors.length) return null;
  return (
    <div className={cx('validation-summary-errors', className)} role="alert" {...rest}>
      {heading ? (
        <p className="validation-summary__heading">
          <Icon name="alert-circle" />
          {heading}
        </p>
      ) : null}
      <ul className="validation-summary__list">
        {errors.map((e, i) => (
          <li key={i}>{e}</li>
        ))}
      </ul>
    </div>
  );
}

/** `.failure-detail` — the red-railed detail block under a failed action. */
export function FailureDetail({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('failure-detail', className)} {...rest} />;
}

export interface BlockerProps extends Omit<HTMLAttributes<HTMLLIElement>, 'title'> {
  /** State channel value driving the rail tone; unmet requirements are usually `not-ready`. */
  state?: StateName;
  /** The requirement, e.g. `Vehicle registration`. */
  title: ReactNode;
  /** What resolves it, e.g. `Enter the registration on the Vehicle tab.` */
  children?: ReactNode;
}

/** `.blocker` — one unmet requirement naming its own field and resolution. Render inside `BlockerList`. */
export function Blocker({ state = 'not-ready', title, className, children, ...rest }: BlockerProps) {
  return (
    <li className={cx('blocker', className)} data-state={state} {...rest}>
      <strong>{title}</strong>
      {children ? <small>{children}</small> : null}
    </li>
  );
}

/** `.blocker-list` — the readiness rail's list of `Blocker`s. */
export function BlockerList({ className, ...rest }: HTMLAttributes<HTMLUListElement>) {
  return <ul className={cx('blocker-list', className)} {...rest} />;
}

export interface ProvenanceProps extends HTMLAttributes<HTMLSpanElement> {
  /** Exactly one word: Staff · Extracted · AI · E-mail · Lookup · Principal · Automatic. */
  word: 'Staff' | 'Extracted' | 'AI' | 'E-mail' | 'Lookup' | 'Principal' | 'Automatic' | (string & {});
  /** Glyph for the source; defaults per word. */
  icon?: IconName;
}

const PROV_ICON: Record<string, IconName> = {
  Staff: 'user',
  Extracted: 'file-text',
  AI: 'filter',
  'E-mail': 'arrow-right',
  Lookup: 'search',
  Principal: 'shield',
  Automatic: 'refresh-cw',
};

/**
 * `.prov` — where a value came from: a small icon whose tooltip, on hover and
 * keyboard focus, is exactly one word. Always supplementary; the row must make
 * sense with it ignored.
 */
export function Provenance({ word, icon, className, ...rest }: ProvenanceProps) {
  return (
    <span className={cx('prov', className)} data-word={word} tabIndex={0} role="img" aria-label={word} {...rest}>
      <Icon name={icon ?? PROV_ICON[word] ?? 'info'} />
    </span>
  );
}
