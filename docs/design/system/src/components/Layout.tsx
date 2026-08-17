import type { HTMLAttributes, ReactNode } from 'react';
import { cx } from '../cx';
import { Icon, type IconName } from './Icon';

/** `.panel` — a white card on the paper ground: 16px padding, hairline border, 6px radius, soft shadow. */
export function Panel({ className, ...rest }: HTMLAttributes<HTMLElement>) {
  return <section className={cx('panel', className)} {...rest} />;
}

/** `.dashboard-grid` — two equal columns of panels (single column under 1280px). */
export function DashboardGrid({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('dashboard-grid', className)} {...rest} />;
}

/** `.split-main` — the list leads (2fr), the form that adds to it follows (min 300px). */
export function SplitMain({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('split-main', className)} {...rest} />;
}

/** `.review-grid` — two equal columns for side-by-side review. */
export function ReviewGrid({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cx('review-grid', className)} {...rest} />;
}

export interface WorkbenchGridProps extends HTMLAttributes<HTMLDivElement> {
  /** The sticky 320px readiness rail (rendered as `<aside>`, first in the DOM). */
  aside: ReactNode;
  /** The section being worked on. */
  children: ReactNode;
}

/** `.workbench-grid` — the Engineers assessment workbench: sticky rail + main column. */
export function WorkbenchGrid({ aside, children, className, ...rest }: WorkbenchGridProps) {
  return (
    <div className={cx('workbench-grid', className)} {...rest}>
      <aside>{aside}</aside>
      <div>{children}</div>
    </div>
  );
}

export interface EyebrowProps extends HTMLAttributes<HTMLParagraphElement> {
  children: ReactNode;
}

/** `.eyebrow` — the small uppercase muted label above a heading or figure. */
export function Eyebrow({ className, ...rest }: EyebrowProps) {
  return <p className={cx('eyebrow', className)} {...rest} />;
}

export interface SectionLabelProps extends HTMLAttributes<HTMLHeadingElement> {
  /** Optional Lucide glyph before the text (`.section-label--iconed`). */
  icon?: IconName;
  children: ReactNode;
}

/** `.section-label` — an eyebrow-styled `<h2>` that names a section of a panel. */
export function SectionLabel({ icon, className, children, ...rest }: SectionLabelProps) {
  return (
    <h2 className={cx('section-label', icon && 'section-label--iconed', className)} {...rest}>
      {icon ? <Icon name={icon} /> : null}
      {children}
    </h2>
  );
}

export interface BlockheadProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  /** The block title, rendered as an uppercase muted `<h2>`. */
  title: ReactNode;
  /** Trailing controls, pushed to the right (`.blockhead-end`). */
  end?: ReactNode;
}

/** `.blockhead` — a block header inside a record body: title left, controls right. */
export function Blockhead({ title, end, className, ...rest }: BlockheadProps) {
  return (
    <div className={cx('blockhead', className)} {...rest}>
      <h2>{title}</h2>
      {end ? <div className="blockhead-end">{end}</div> : null}
    </div>
  );
}

/** `.lede` — a muted one-line intro. Design rule: screens carry no lede; use only beside a consequential control. */
export function Lede({ className, ...rest }: HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cx('lede', className)} {...rest} />;
}

/** `.empty-state` — muted business-language copy for a zero result. */
export function EmptyState({ className, ...rest }: HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cx('empty-state', className)} {...rest} />;
}

/** `.sr-only` — visually hidden text for assistive technology. */
export function SrOnly({ className, ...rest }: HTMLAttributes<HTMLSpanElement>) {
  return <span className={cx('sr-only', className)} {...rest} />;
}
