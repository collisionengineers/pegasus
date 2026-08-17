import type { AnchorHTMLAttributes, HTMLAttributes, ReactNode } from 'react';
import { cx, type StateName } from '../cx';

export interface RecordProps extends HTMLAttributes<HTMLElement> {
  /** The record's stage on the state channel; colours the 3px accent under the head band. */
  state?: StateName;
  children: ReactNode;
}

/**
 * `.record` — THE RECORD CONTAINER. A screen about one record is one
 * container with three parts and only three: `RecordHead` (dark band with the
 * reference, identity and state chip), `RecordBar` (every action valid for the
 * current state), and either `Tabs` + `RecordBody` or a plain `RecordBody`.
 */
export function Record({ state, className, children, ...rest }: RecordProps) {
  return (
    <article className={cx('record', className)} data-state={state} {...rest}>
      {children}
    </article>
  );
}

export interface RecordHeadProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** The record reference, rendered as the screen's `<h1>` (tabular numerals). */
  reference: ReactNode;
  /**
   * Identity facts beside the reference — principal (bold), registration,
   * claimant, case type. Each item renders as its own `<span>`; wrap the
   * emphasised one in `<b>`.
   */
  identity?: ReactNode[];
  /** Right-aligned slot, normally the stage `StatusChip`. */
  end?: ReactNode;
  /** Optional muted note under the band (`.record__note`). */
  note?: ReactNode;
  /** Whether to draw the 3px state accent under the band (default true). */
  accent?: boolean;
}

/** `.record__head` — the dark header band: reference, identity facts, state chip; then the stage accent. */
export function RecordHead({ reference, identity, end, note, accent = true, className, ...rest }: RecordHeadProps) {
  return (
    <>
      <header className={cx('record__head', className)} {...rest}>
        <h1>{reference}</h1>
        {identity && identity.length ? (
          <span className="record__identity">
            {identity.map((item, i) => (
              <span key={i}>{item}</span>
            ))}
          </span>
        ) : null}
        {end ? <span className="record__head-end">{end}</span> : null}
      </header>
      {note ? <div className="record__note">{note}</div> : null}
      {accent ? <div className="record__accent" /> : null}
    </>
  );
}

export interface RecordBarProps extends HTMLAttributes<HTMLDivElement> {
  /** Actions for the current state (compact `Button`s). */
  children?: ReactNode;
  /** The record-level commitment, right-aligned behind a divider rule. */
  end?: ReactNode;
}

/** `.record__bar` — the sticky action bar: state actions left, the committed action right behind a hairline rule. */
export function RecordBar({ end, className, children, ...rest }: RecordBarProps) {
  return (
    <div className={cx('record__bar', className)} {...rest}>
      {children}
      {end ? (
        <span className="record__bar-end">
          <span className="record__bar-rule" aria-hidden="true" />
          {end}
        </span>
      ) : null}
    </div>
  );
}

/** `.record__body` — the container's content area (16/20px padding). */
export function RecordBody({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('record__body', className)} {...rest} />;
}

export interface TabItem {
  label: ReactNode;
  href?: string;
  /** Optional count pill after the label. */
  count?: number;
  current?: boolean;
  onClick?: () => void;
}

export interface TabsProps extends HTMLAttributes<HTMLElement> {
  tabs: TabItem[];
  /** Accessible name of the tab set, e.g. `Case sections`. */
  label: string;
}

/**
 * `.tabs` — the record container's tab row on the paper ground: red underline
 * marks the current section, `.count` pills carry totals. Tabs appear when the
 * sections are alternatives; a reading order gets a body and no tab row.
 */
export function Tabs({ tabs, label, className, ...rest }: TabsProps) {
  return (
    <nav className={cx('tabs', className)} aria-label={label} {...rest}>
      {tabs.map((t, i) =>
        t.href ? (
          <a key={i} href={t.href} aria-current={t.current ? 'page' : undefined}>
            {t.label}
            {t.count !== undefined ? <span className="count">{t.count}</span> : null}
          </a>
        ) : (
          <button key={i} type="button" aria-selected={t.current || undefined} onClick={t.onClick}>
            {t.label}
            {t.count !== undefined ? <span className="count">{t.count}</span> : null}
          </button>
        ),
      )}
    </nav>
  );
}

export interface SubtabsProps extends HTMLAttributes<HTMLElement> {
  tabs: TabItem[];
  label: string;
  /** Slot pushed to the right (`.subtabs-end`). */
  end?: ReactNode;
  /** Adds `.section-gap` above. */
  sectionGap?: boolean;
}

/** `.subtabs` — pill sub-navigation for a nested level (folder, sub-state); the current pill is filled charcoal. */
export function Subtabs({ tabs, label, end, sectionGap, className, ...rest }: SubtabsProps) {
  return (
    <nav className={cx('subtabs', sectionGap && 'section-gap', className)} aria-label={label} {...rest}>
      {tabs.map((t, i) => (
        <a key={i} href={t.href ?? '#'} aria-current={t.current ? 'page' : undefined} onClick={t.onClick}>
          {t.label}
          {t.count !== undefined ? <span className="n">{t.count}</span> : null}
        </a>
      ))}
      {end ? <span className="subtabs-end">{end}</span> : null}
    </nav>
  );
}

export interface SectionTabsProps extends HTMLAttributes<HTMLElement> {
  tabs: TabItem[];
  label: string;
  sectionGap?: boolean;
}

/** `.section-tabs` — page-level section navigation (assessment sections), mirroring the shell's active-route underline. */
export function SectionTabs({ tabs, label, sectionGap, className, ...rest }: SectionTabsProps) {
  return (
    <nav className={cx('section-tabs', sectionGap && 'section-gap', className)} aria-label={label} {...rest}>
      {tabs.map((t, i) => (
        <a key={i} href={t.href ?? '#'} aria-current={t.current ? 'page' : undefined} onClick={t.onClick}>
          {t.label}
        </a>
      ))}
    </nav>
  );
}

export interface CrumbProps extends HTMLAttributes<HTMLElement> {
  /** Parent links; the current item is plain text after them. */
  parents: Array<{ label: ReactNode; href: string }>;
  current: ReactNode;
}

/** `.crumb` — a one-line breadcrumb above a record: `Cases / CE-2026-01432`. */
export function Crumb({ parents, current, className, ...rest }: CrumbProps) {
  return (
    <nav className={cx('crumb', className)} aria-label="Breadcrumb" {...rest}>
      {parents.map((p, i) => (
        <span key={i}>
          <a href={p.href}>{p.label}</a> /{' '}
        </span>
      ))}
      {current}
    </nav>
  );
}

export interface FactGroup {
  /** Uppercase muted title of the column. */
  title: ReactNode;
  items: Array<{ term: ReactNode; value: ReactNode; quiet?: boolean }>;
}

export interface FactsProps extends HTMLAttributes<HTMLDivElement> {
  groups: FactGroup[];
}

/** `.facts` — compact fact columns inside a record body: titled `<dl>`s, 28px rows, tabular numerals. */
export function Facts({ groups, className, ...rest }: FactsProps) {
  return (
    <div className={cx('facts', className)} {...rest}>
      {groups.map((g, i) => (
        <section key={i}>
          <h2>{g.title}</h2>
          <dl>
            {g.items.map((it, j) => (
              <div key={j}>
                <dt>{it.term}</dt>
                <dd className={it.quiet ? 'quiet' : undefined}>{it.value}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </div>
  );
}

export interface DataRowProps extends HTMLAttributes<HTMLDivElement> {
  field: ReactNode;
  /** The recorded value; omit for `Not recorded` in the quiet style. */
  value?: ReactNode;
  /** A suggestion shown beside an unrecorded value (`Suggested …`). */
  suggested?: ReactNode;
  /** Right slot — provenance icon, action button. */
  end?: ReactNode;
}

/** `.datarow` — one field/value line with provenance or an action at the end. */
export function DataRow({ field, value, suggested, end, className, ...rest }: DataRowProps) {
  return (
    <div className={cx('datarow', className)} {...rest}>
      <span className="datarow__field">{field}</span>
      {value !== undefined && value !== null ? <span className="datarow__value">{value}</span> : <span className="datarow__value quiet">Not recorded</span>}
      {suggested && (value === undefined || value === null) ? <span className="datarow__sug">Suggested {suggested}</span> : null}
      {end ? <span className="datarow__end">{end}</span> : null}
    </div>
  );
}

export interface DetailListProps extends HTMLAttributes<HTMLDListElement> {
  items: Array<{ term: ReactNode; value: ReactNode }>;
}

/** `.detail-list` — a two-column `<dl>` (10rem term column, bold values, hairline rows). */
export function DetailList({ items, className, ...rest }: DetailListProps) {
  return (
    <dl className={cx('detail-list', className)} {...rest}>
      {items.map((it, i) => (
        <div key={i}>
          <dt>{it.term}</dt>
          <dd>{it.value}</dd>
        </div>
      ))}
    </dl>
  );
}

export interface EvidenceListProps extends HTMLAttributes<HTMLUListElement> {
  items: Array<{ term: ReactNode; value: ReactNode }>;
}

/** `.evidence-list` — a bulleted two-column list of evidence facts. */
export function EvidenceList({ items, className, ...rest }: EvidenceListProps) {
  return (
    <ul className={cx('evidence-list', className)} {...rest}>
      {items.map((it, i) => (
        <li key={i}>
          <strong>{it.term}</strong>
          <span>{it.value}</span>
        </li>
      ))}
    </ul>
  );
}

export interface EvidenceFigureProps extends HTMLAttributes<HTMLDivElement> {
  label: ReactNode;
  value: ReactNode;
  /** Where the figure came from, e.g. `Glass's guide, 12 Aug`. */
  source?: ReactNode;
}

/** `.evidence-figure` — a read-only guide figure on the recessed ground (label, value, source stacked). */
export function EvidenceFigure({ label, value, source, className, ...rest }: EvidenceFigureProps) {
  return (
    <div className={cx('evidence-figure', className)} style={{ display: 'grid', gap: 2 }} {...rest}>
      <span className="evidence-figure__label">{label}</span>
      <span className="evidence-figure__value">{value}</span>
      {source ? <span className="evidence-figure__source">{source}</span> : null}
    </div>
  );
}

export interface ProposalDiffProps extends HTMLAttributes<HTMLDivElement> {
  recorded: { title?: ReactNode; children: ReactNode };
  proposed: { title?: ReactNode; children: ReactNode };
}

/** `.proposal-diff` — recorded value beside proposed value, equal weight. */
export function ProposalDiff({ recorded, proposed, className, ...rest }: ProposalDiffProps) {
  return (
    <div className={cx('proposal-diff', className)} {...rest}>
      <div>
        <h3>{recorded.title ?? 'Recorded'}</h3>
        {typeof recorded.children === 'string' ? <p>{recorded.children}</p> : recorded.children}
      </div>
      <div>
        <h3>{proposed.title ?? 'Proposed'}</h3>
        {typeof proposed.children === 'string' ? <p>{proposed.children}</p> : proposed.children}
      </div>
    </div>
  );
}

/** `.field-grid` — hairline-separated grid of `FieldCard`s (min 260px). */
export function FieldGrid({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('field-grid', className)} {...rest} />;
}

export interface FieldCardProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** Uppercase muted title. */
  title: ReactNode;
  /** The value. */
  children: ReactNode;
  /** Muted small line under the value (source, time). */
  detail?: ReactNode;
  /** Amber left rail marking a conflicting value. */
  conflict?: boolean;
}

/** `.field-card` — one extracted field: title, value, small detail; `conflict` adds the amber rail. */
export function FieldCard({ title, detail, conflict, className, children, ...rest }: FieldCardProps) {
  return (
    <article className={cx('field-card', conflict && 'field-card--conflict', className)} {...rest}>
      <h3>{title}</h3>
      <strong>{children}</strong>
      {detail ? <small>{detail}</small> : null}
    </article>
  );
}

