import type { AnchorHTMLAttributes, HTMLAttributes, ReactNode } from 'react';
import { cx, type StateName } from '../cx';
import { Icon, type IconName } from './Icon';

export interface MetricStripProps extends HTMLAttributes<HTMLDivElement> {
  /** Column count: 7 (operations, default), 5 (`secondary`), or 3 (dashboard). */
  columns?: 7 | 5 | 3;
}

/** `.metric-strip` — a single row of compact `Metric` tiles; reflows to 4/2/1 columns on narrow viewports. */
export function MetricStrip({ columns = 7, className, ...rest }: MetricStripProps) {
  return (
    <div className={cx('metric-strip', columns === 5 && 'metric-strip--secondary', columns === 3 && 'metric-strip--3', className)} {...rest} />
  );
}

export interface MetricProps extends Omit<HTMLAttributes<HTMLElement>, 'children'> {
  /** The exact queue or figure label, e.g. `Not ready`, `Received today`. */
  label: ReactNode;
  /** Lucide glyph in the label; tinted by the state channel. */
  icon?: IconName;
  /** The count. A composed query that returns zero renders `0`. */
  value?: number | string;
  /**
   * When the datum is absent, the state that replaces the value — never a
   * dash pretending to be a number. Renders in place of the value.
   */
  absent?: ReactNode;
  /** State channel: `not-ready`, `review`, `held`, `needs-sorting`, `blocked`… drives the top rail and icon tint. */
  state?: StateName;
  /** Makes the tile a link to the exact filtered list behind it. */
  href?: string;
}

/**
 * `.metric` — one compact tile: label with icon, count at the bottom, a 3px
 * state rail on top. Every metric opens the exact filtered list behind it.
 */
export function Metric({ label, icon, value = 0, absent, state, href, className, ...rest }: MetricProps) {
  const body = (
    <>
      <span className="metric__label">
        {icon ? <Icon name={icon} size="sm" /> : null}
        {label}
      </span>
      {absent ? <span className="metric__absent">{absent}</span> : <strong className="metric__value">{value}</strong>}
    </>
  );
  const cls = cx('metric', className);
  return href ? (
    <a className={cls} data-state={state} href={href} {...(rest as AnchorHTMLAttributes<HTMLAnchorElement>)}>
      {body}
    </a>
  ) : (
    <span className={cls} data-state={state} {...rest}>
      {body}
    </span>
  );
}

/** `.queue-grid` — auto-fit grid of `QueueCard`s (min 220px each). */
export function QueueGrid({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('queue-grid', className)} {...rest} />;
}

export interface QueueCardProps extends Omit<HTMLAttributes<HTMLElement>, 'children'> {
  /** The queue name, e.g. `Unidentified`. */
  label: ReactNode;
  /** The count. */
  value?: number | string;
  /** Datum absent: shows a quiet em dash instead of a count. */
  unavailable?: boolean;
  /** Small muted line under the count (e.g. `Oldest 3 days`). */
  detail?: ReactNode;
  /** Optional Lucide glyph in a tinted 34px square at the left. */
  icon?: IconName;
  /** Theme modifier (`queue-card--amber` etc.). Prefer `state` when the card stands for a state. */
  theme?: 'blue' | 'amber' | 'red' | 'green';
  /** State channel value (`data-state`) — the tone the queue stands for. */
  state?: StateName;
  /** Opens the queue. Adds the trailing chevron and hover fill. */
  href?: string;
  /** Slot rendered inside the text column after the value (e.g. a `StatusChip`). */
  children?: ReactNode;
}

/**
 * `.queue-card` — a queue tile: optional icon square, label, big tabular
 * count, trailing chevron. The 3px top rail takes the state colour.
 */
export function QueueCard({ label, value = 0, unavailable, detail, icon, theme, state, href, className, children, ...rest }: QueueCardProps) {
  const cls = cx('queue-card', theme && `queue-card--${theme}`, unavailable && 'queue-card--unavailable', className);
  const body = (
    <>
      {icon ? (
        <span className="queue-icon">
          <Icon name={icon} />
        </span>
      ) : null}
      <div>
        <span>{label}</span>
        {unavailable ? <strong className="queue-card__value--absent">—</strong> : <strong>{value}</strong>}
        {children}
        {detail ? <small>{detail}</small> : null}
      </div>
      {href ? <Icon name="chevron-right" className="queue-card__chevron" /> : null}
    </>
  );
  return href ? (
    <a className={cls} data-state={state} data-queue-state={unavailable ? 'unavailable' : undefined} href={href} {...(rest as AnchorHTMLAttributes<HTMLAnchorElement>)}>
      {body}
    </a>
  ) : (
    <article className={cls} data-state={state} data-queue-state={unavailable ? 'unavailable' : undefined} {...rest}>
      {body}
    </article>
  );
}

/** `.tile-grid` — a two-column grid of `MetricTile`s that share hairline borders. */
export function TileGrid({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('tile-grid', className)} {...rest} />;
}

export interface MetricTileProps extends Omit<HTMLAttributes<HTMLElement>, 'children'> {
  label: ReactNode;
  value: number | string;
  icon?: IconName;
  /** Amber rail and icon tint for a figure that needs attention. */
  attention?: boolean;
  /** Spans the full grid width. */
  span?: boolean;
  href?: string;
}

/** `.metric-tile` — a bordered tile in a `TileGrid`: icon square, big count, label. */
export function MetricTile({ label, value, icon, attention, span, href, className, ...rest }: MetricTileProps) {
  const cls = cx('metric-tile', attention && 'metric-tile--attention', span && 'metric-tile--span', className);
  const body = (
    <>
      {icon ? (
        <span>
          <Icon name={icon} />
        </span>
      ) : null}
      <div>
        <strong>{value}</strong>
        <small>{label}</small>
      </div>
    </>
  );
  return href ? (
    <a className={cls} href={href} {...(rest as AnchorHTMLAttributes<HTMLAnchorElement>)}>
      {body}
    </a>
  ) : (
    <div className={cls} {...rest}>
      {body}
    </div>
  );
}

export interface QueueFilter {
  label: ReactNode;
  href: string;
  /** Marks the active filter with `aria-current="page"`. */
  current?: boolean;
}

export interface QueueFiltersProps extends HTMLAttributes<HTMLElement> {
  filters: QueueFilter[];
}

/** `.queue-filters` — a row of hairline filter links above a queue list. */
export function QueueFilters({ filters, className, ...rest }: QueueFiltersProps) {
  return (
    <nav className={cx('queue-filters', className)} aria-label="Filter" {...rest}>
      {filters.map((f, i) => (
        <a key={i} href={f.href} aria-current={f.current ? 'page' : undefined}>
          {f.label}
        </a>
      ))}
    </nav>
  );
}

/** `.panel.queue-list` — a panel whose rows (`QueueListRow`) are each one full-row target. Already a panel: do not nest it in `Panel`. */
export function QueueList({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('panel', 'queue-list', className)} {...rest} />;
}

export interface QueueListRowProps extends Omit<HTMLAttributes<HTMLElement>, 'children' | 'title'> {
  /** Row destination; a linked row gets the trailing › affordance and hover rail. */
  href?: string;
  /** Left column: the reference or subject (bold) with an optional muted line. */
  title: ReactNode;
  subtitle?: ReactNode;
  /** Optional middle column (mail rows: subject + excerpt), same strong/small shape. */
  middle?: ReactNode;
  /** Right column: usually a `StatusChip` plus a muted line (`<small>`), or a `<time>`. */
  end?: ReactNode;
  /** `unread` renders the title at weight 800 (mail workspace). */
  state?: StateName;
  /** Visually hidden word appended to the title for assistive technology (the mail list adds `Unread`, since weight alone is not a state). */
  srState?: string;
}

/** One row of a `QueueList`: identity left, state right, full-row link. */
export function QueueListRow({ href, title, subtitle, middle, end, state, srState, className, ...rest }: QueueListRowProps) {
  const body = (
    <>
      <span>
        <strong>
          {title}
          {srState ? <span className="vh"> {srState}</span> : null}
        </strong>
        {subtitle ? <small>{subtitle}</small> : null}
      </span>
      {middle ? <span>{middle}</span> : null}
      <span>{end}</span>
    </>
  );
  return href ? (
    <a href={href} className={className} data-state={state} {...(rest as AnchorHTMLAttributes<HTMLAnchorElement>)}>
      {body}
    </a>
  ) : (
    <article className={className} data-state={state} {...rest}>
      {body}
    </article>
  );
}

/** `.admin-workspaces` — auto-fit grid (min 300px) of `AdminCard`s. */
export function AdminWorkspaces({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('admin-workspaces', className)} {...rest} />;
}

export interface AdminCardProps extends Omit<HTMLAttributes<HTMLElement>, 'title' | 'children'> {
  icon: IconName;
  title: ReactNode;
  href: string;
  children?: ReactNode;
}

/** `.admin-card` — an administration workspace entry: icon square, linked title (whole card is the target), one-line description. */
export function AdminCard({ icon, title, href, className, children, ...rest }: AdminCardProps) {
  return (
    <section className={cx('admin-card', className)} {...rest}>
      <span className="admin-card__icon" aria-hidden="true">
        <Icon name={icon} size="lg" />
      </span>
      <div>
        <h2>
          <a href={href}>{title}</a>
        </h2>
        {children ? <p>{children}</p> : null}
      </div>
    </section>
  );
}
