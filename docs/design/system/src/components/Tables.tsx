import type { HTMLAttributes, ReactNode, TableHTMLAttributes } from 'react';
import { cx } from '../cx';

export interface TableColumn<Row> {
  /** Column header text (rendered uppercase, muted, `scope="col"`). */
  header: ReactNode;
  /** Renders the cell for a row. */
  cell: (row: Row, index: number) => ReactNode;
  /** Adds `.tabular` to numeric cells. */
  tabular?: boolean;
}

export interface DataTableProps<Row> extends Omit<TableHTMLAttributes<HTMLTableElement>, 'children'> {
  columns: TableColumn<Row>[];
  rows: Row[];
  /** Visually hidden caption naming the table for assistive technology (`.vh`), or a visible one with `captionVisible`. */
  caption?: ReactNode;
  captionVisible?: boolean;
  /** Optional footer cells (a calculated total). Same length as `columns`. */
  footer?: ReactNode[];
  /** Repair-specification layout (`.line-grid`): fixed columns, wrapping headers. */
  lineGrid?: boolean;
  /** Muted empty-state text rendered instead of the table when `rows` is empty. */
  empty?: ReactNode;
  rowKey?: (row: Row, index: number) => string | number;
}

/**
 * `.table-wrap` > `<table>` — the operational table: 32px rows, uppercase
 * muted headers on paper, hairline row rules, tabular numerals, hover wash.
 * Links in cells are navy and bold.
 */
export function DataTable<Row>({ columns, rows, caption, captionVisible, footer, lineGrid, empty, rowKey, className, ...rest }: DataTableProps<Row>) {
  if (!rows.length && empty !== undefined) {
    return <p className="empty-state">{empty}</p>;
  }
  return (
    <div className="table-wrap">
      <table className={cx(lineGrid && 'line-grid', className)} {...rest}>
        {caption ? <caption className={captionVisible ? undefined : 'vh'}>{caption}</caption> : null}
        <thead>
          <tr>
            {columns.map((c, i) => (
              <th key={i} scope="col">
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, ri) => (
            <tr key={rowKey ? rowKey(r, ri) : ri}>
              {columns.map((c, ci) => (
                <td key={ci} className={c.tabular ? 'tabular' : undefined}>
                  {c.cell(r, ri)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
        {footer ? (
          <tfoot>
            <tr>
              {footer.map((f, i) => (
                <td key={i}>{f}</td>
              ))}
            </tr>
          </tfoot>
        ) : null}
      </table>
    </div>
  );
}

/** `.table-wrap` — the bordered, horizontally scrolling wrapper for a hand-written `<table>`. */
export function TableWrap({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('table-wrap', className)} {...rest} />;
}

export interface PagerProps extends HTMLAttributes<HTMLElement> {
  /** Previous page URL; omitted entirely on the first page — never rendered disabled. */
  previousHref?: string;
  /** Next page URL; omitted on the last page. */
  nextHref?: string;
  /** Context text, e.g. `Page 2 of 7` or `Page 1 · showing 25`. */
  context: ReactNode;
  /** Accessible name (default `Pages`). */
  label?: string;
  onPrevious?: () => void;
  onNext?: () => void;
}

/** `.pager` — accessible Previous / context / Next pagination; never infinite scroll. */
export function Pager({ previousHref, nextHref, context, label = 'Pages', onPrevious, onNext, className, ...rest }: PagerProps) {
  return (
    <nav className={cx('pager', className)} aria-label={label} {...rest}>
      {previousHref || onPrevious ? (
        <a href={previousHref ?? '#'} onClick={onPrevious}>
          Previous
        </a>
      ) : null}
      <span className="pager__context">{context}</span>
      {nextHref || onNext ? (
        <a href={nextHref ?? '#'} onClick={onNext}>
          Next
        </a>
      ) : null}
    </nav>
  );
}

export interface FilterBarProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** Visually hidden heading naming the filter (`Filter cases`). */
  title: string;
  /** The one-line filter controls: inputs, selects, then `Button`s (Search dark, Clear plain). */
  children: ReactNode;
  /** Rarely used fields behind a `More filters` disclosure — a `FormGrid`. */
  more?: ReactNode;
  moreLabel?: ReactNode;
  moreOpen?: boolean;
  onSubmit?: (e: React.FormEvent<HTMLFormElement>) => void;
}

/**
 * `.panel.filterbar` — one line of common filters with the rarely used fields
 * behind a disclosure. Eleven inputs stacked above the results is a form, not
 * a filter.
 */
export function FilterBar({ title, children, more, moreLabel = 'More filters', moreOpen, onSubmit, className, ...rest }: FilterBarProps) {
  return (
    <section className={cx('panel', 'filterbar', className)} aria-label={title} {...rest}>
      <h2 className="vh">{title}</h2>
      <form
        method="get"
        onSubmit={(e) => {
          if (onSubmit) {
            e.preventDefault();
            onSubmit(e);
          }
        }}
      >
        <div className="filterbar__line">{children}</div>
        {more ? (
          <details className="filterbar__more" open={moreOpen}>
            <summary>{moreLabel}</summary>
            {more}
          </details>
        ) : null}
      </form>
    </section>
  );
}

/** `.plain-list` — a simple bulleted list with the 8px rhythm. */
export function PlainList({ className, ...rest }: HTMLAttributes<HTMLUListElement>) {
  return <ul className={cx('plain-list', className)} {...rest} />;
}

/** `.action-list` — a wrapping row of inline actions or facts. */
export function ActionList({ className, ...rest }: HTMLAttributes<HTMLUListElement>) {
  return <ul className={cx('action-list', className)} style={{ listStyle: 'none', margin: 0, padding: 0 }} {...rest} />;
}
