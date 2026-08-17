import type { AnchorHTMLAttributes, ButtonHTMLAttributes, HTMLAttributes, ReactNode } from 'react';
import { cx } from '../cx';
import { Icon, type IconName } from './Icon';

type ButtonBase = {
  /** Optional Lucide glyph drawn before the label at the compact .875rem size. */
  icon?: IconName;
  /** Render as a link (`href`) instead of a `<button>`. */
  href?: string;
  children?: ReactNode;
  className?: string;
};

export interface ButtonProps
  extends ButtonBase,
    Omit<ButtonHTMLAttributes<HTMLButtonElement> & AnchorHTMLAttributes<HTMLAnchorElement>, 'children' | 'className' | 'type' | 'href'> {
  /**
   * `default` is the compact hairline action-bar button; `dark` (charcoal) is
   * the bar's committed action; `primary` spends Collision red and is reserved
   * for the one primary action on a screen; `light` sits on the dark record band.
   */
  variant?: 'default' | 'dark' | 'primary' | 'light';
  /** Icon-only padding (`.btn--icon`). Give the button an `aria-label`. */
  iconOnly?: boolean;
  /** Native disabled for buttons; links keep their `href` (so the condition stays keyboard-reachable) and get `.is-disabled` + `aria-disabled` with navigation suppressed. */
  disabled?: boolean;
  /**
   * The condition that unlocks a disabled action, e.g. `Available in Review`.
   * Wraps the control in `.gated` so the condition appears as a tooltip on
   * hover and keyboard focus — a disabled action states its condition rather
   * than disappearing.
   */
  condition?: string;
  type?: 'button' | 'submit' | 'reset';
}

/**
 * `.btn` — the compact action-bar button used in record bars, table rows and
 * filter bars. Page-level form submits use `PrimaryAction`/`SecondaryAction`.
 */
export function Button({
  variant = 'default',
  icon,
  iconOnly,
  disabled,
  condition,
  href,
  type = 'button',
  className,
  children,
  ...rest
}: ButtonProps) {
  const cls = cx(
    'btn',
    variant !== 'default' && `btn--${variant}`,
    iconOnly && 'btn--icon',
    href && disabled && 'is-disabled',
    className,
  );
  const inner = (
    <>
      {icon ? <Icon name={icon} /> : null}
      {children}
    </>
  );
  const anchorRest = rest as AnchorHTMLAttributes<HTMLAnchorElement>;
  const control = href ? (
    <a
      className={cls}
      href={href}
      aria-disabled={disabled || undefined}
      {...anchorRest}
      onClick={(e) => {
        if (disabled) e.preventDefault();
        else anchorRest.onClick?.(e);
      }}
    >
      {inner}
    </a>
  ) : (
    <button className={cls} type={type} disabled={disabled} {...(rest as ButtonHTMLAttributes<HTMLButtonElement>)}>
      {inner}
    </button>
  );
  return condition && disabled ? (
    <span className="gated" data-condition={condition}>
      {control}
    </span>
  ) : (
    control
  );
}

export interface ActionProps
  extends ButtonBase,
    Omit<ButtonHTMLAttributes<HTMLButtonElement> & AnchorHTMLAttributes<HTMLAnchorElement>, 'children' | 'className' | 'type' | 'href'> {
  type?: 'button' | 'submit' | 'reset';
  disabled?: boolean;
}

function pageAction(kind: 'primary-action' | 'secondary-action') {
  return function PageAction({ icon, href, type = 'submit', className, children, ...rest }: ActionProps) {
    const cls = cx(kind, className);
    const inner = (
      <>
        {icon ? <Icon name={icon} /> : null}
        {children}
      </>
    );
    return href ? (
      <a className={cls} href={href} {...(rest as AnchorHTMLAttributes<HTMLAnchorElement>)}>
        {inner}
      </a>
    ) : (
      <button className={cls} type={type} {...(rest as ButtonHTMLAttributes<HTMLButtonElement>)}>
        {inner}
      </button>
    );
  };
}

/**
 * `.primary-action` — the page-level form submit in Collision red. One per
 * screen: red is reserved for the primary action, active navigation, focus and
 * urgent emphasis.
 */
export const PrimaryAction = pageAction('primary-action');

/** `.secondary-action` — the page-level hairline companion to `PrimaryAction` (Cancel, Back, alternative). */
export const SecondaryAction = pageAction('secondary-action');

export interface ButtonRowProps extends HTMLAttributes<HTMLDivElement> {
  /** Right-align the actions (`.button-row--end`), as in dialog footers. */
  end?: boolean;
  /** Adds `.section-gap` above (used under a form section). */
  sectionGap?: boolean;
}

/** `.button-row` — a wrapping flex row of actions with the 10px gap. */
export function ButtonRow({ end, sectionGap, className, ...rest }: ButtonRowProps) {
  return <div className={cx('button-row', end && 'button-row--end', sectionGap && 'section-gap', className)} {...rest} />;
}

export interface BackLinkProps extends AnchorHTMLAttributes<HTMLAnchorElement> {
  href: string;
  children: ReactNode;
}

/** `.back-link` — muted return link with the arrow rotated to point back. */
export function BackLink({ className, children, ...rest }: BackLinkProps) {
  return (
    <a className={cx('back-link', className)} {...rest}>
      <Icon name="arrow-right" />
      {children}
    </a>
  );
}

export interface GatedProps extends HTMLAttributes<HTMLSpanElement> {
  /** The condition shown as a tooltip on hover / focus-within (`data-condition`). */
  condition: string;
  children: ReactNode;
}

/** `.gated` — wraps a disabled control so its unlocking condition shows as a one-line tooltip. */
export function Gated({ condition, className, ...rest }: GatedProps) {
  return <span className={cx('gated', className)} data-condition={condition} {...rest} />;
}

export interface SendToClaudeButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  children?: ReactNode;
}

/**
 * `.send-action` — the one recorded divergence from the palette: the Engineer
 * assessment surface's "Send to Claude" control carries the provider's own
 * terracotta identity so it is never mistaken for a Collision Engineers action.
 */
export function SendToClaudeButton({ className, children = 'Send to Claude', type = 'button', ...rest }: SendToClaudeButtonProps) {
  return (
    <button className={cx('secondary-action', 'send-action', className)} type={type} {...rest}>
      <svg className="send-action__sparkle" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M12 1.5c.6 4.6 1.9 7.6 4.2 9.9 2.3 2.3 5.3 3.6 9.9 4.2-4.6.6-7.6 1.9-9.9 4.2-2.3 2.3-3.6 5.3-4.2 9.9-.6-4.6-1.9-7.6-4.2-9.9C5.5 17.5 2.5 16.2-2 15.6c4.6-.6 7.6-1.9 9.9-4.2C10.1 9.1 11.4 6.1 12 1.5Z" fill="currentColor" />
      </svg>
      <span>{children}</span>
    </button>
  );
}
