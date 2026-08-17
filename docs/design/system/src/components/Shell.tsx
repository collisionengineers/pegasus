import type { HTMLAttributes, ReactNode } from 'react';
import { cx } from '../cx';
import logo from '../logo.png';

/** The Collision Engineers logo as a data URI, sized for the 104×32 brand slot. */
export const BRAND_LOGO = logo;

export interface NavItem {
  label: ReactNode;
  href: string;
  /** Marks the route the operator is on (`aria-current="page"`): red, bold, underlined. */
  current?: boolean;
}

export interface AppNavProps extends HTMLAttributes<HTMLElement> {
  /**
   * Primary routes in the settled order: Dashboard, Inbox, Upload, Queues,
   * Cases, Operations, Administration (administrators only). A capability
   * that is not composed in a deployment is absent — never a disabled item.
   */
  items: NavItem[];
  /** Signed-in user name; renders the user menu with Change password / Sign out. */
  userName?: string;
  /** Called when Sign out is pressed. */
  onSignOut?: () => void;
  /** Destination of the brand link (default `/`). */
  homeHref?: string;
  /** Product name beside the logo (default `Pegasus`). Omit with `brandOnly` for the public upload shell. */
  productName?: ReactNode;
  /** External/public shell: logo only, no links or user menu. */
  brandOnly?: boolean;
}

/**
 * `.app-nav` — the white top bar: brand (logo + product name), primary
 * routes, and the user group behind a hairline. Active route is red + weight +
 * underline, never colour alone.
 */
export function AppNav({ items, userName, onSignOut, homeHref = '/', productName = 'Pegasus', brandOnly, className, ...rest }: AppNavProps) {
  return (
    <header className={cx('app-nav', className)} {...rest}>
      <div className="nav-inner">
        {brandOnly ? (
          <span className="navbar-brand">
            <img src={logo} alt="Collision Engineers" />
          </span>
        ) : (
          <a className="navbar-brand" href={homeHref} aria-label="Pegasus dashboard">
            <img src={logo} alt="Collision Engineers" />
            <span>{productName}</span>
          </a>
        )}
        {brandOnly ? null : (
          <nav className="nav-links" aria-label="Primary">
            {items.map((it, i) => (
              <a key={i} className="nav-link" href={it.href} aria-current={it.current ? 'page' : undefined}>
                {it.label}
              </a>
            ))}
            {userName ? (
              <span className="user-menu" role="group" aria-label="User">
                <span className="user-name">{userName}</span>
                <a className="nav-link" href="/Account/PasswordChange">
                  Change password
                </a>
                <form
                  method="post"
                  onSubmit={(e) => {
                    e.preventDefault();
                    onSignOut?.();
                  }}
                >
                  <button className="nav-link" type="submit">
                    Sign out
                  </button>
                </form>
              </span>
            ) : (
              <a className="nav-link" href="/Account/SignIn">
                Sign in
              </a>
            )}
          </nav>
        )}
      </div>
    </header>
  );
}

export interface AppShellProps extends HTMLAttributes<HTMLDivElement> {
  /** The `AppNav` (or nothing for a navless surface). */
  nav?: ReactNode;
  /** Footer text (default `Pegasus · Collision Engineers case management`). Pass `null` to omit. */
  footer?: ReactNode | null;
  children: ReactNode;
}

/**
 * The authenticated page frame: skip link, `AppNav`, `.app-shell` > `<main>`,
 * `.footer`. Screens render inside `main` at 1440px max width on the paper
 * ground.
 */
export function AppShell({ nav, footer = 'Pegasus · Collision Engineers case management', className, children, ...rest }: AppShellProps) {
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      {nav}
      <div className={cx('app-shell', className)} {...rest}>
        <main id="main-content" tabIndex={-1}>
          {children}
        </main>
      </div>
      {footer === null ? null : (
        <footer className="footer">
          <div className="footer-inner">{footer}</div>
        </footer>
      )}
    </>
  );
}

export interface PageHeadingProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** The one H1. Screens carry no lede or subtitle. */
  title: ReactNode;
  /** Optional small uppercase label above the title. */
  eyebrow?: ReactNode;
  /** Right slot: a `Refresh` element, or `.page-heading-actions` content. */
  actions?: ReactNode;
  /** Set when `actions` holds a `Refresh` (it right-aligns itself); otherwise actions wrap in `.page-heading-actions`. */
  refresh?: ReactNode;
}

/** `.page-heading` — H1 (with optional eyebrow) and the screen's safe primary action or freshness element, above a hairline. */
export function PageHeading({ title, eyebrow, actions, refresh, className, ...rest }: PageHeadingProps) {
  return (
    <header className={cx('page-heading', className)} {...rest}>
      {eyebrow ? (
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h1>{title}</h1>
        </div>
      ) : (
        <h1>{title}</h1>
      )}
      {actions ? <div className="page-heading-actions">{actions}</div> : null}
      {refresh}
    </header>
  );
}

