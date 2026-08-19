import type { HTMLAttributes } from 'react';
import { cx, type Tone } from '../cx';
import { Icon, type IconName } from './Icon';

/**
 * The single place a business or query state chooses its visual treatment —
 * ported from `Pages/Shared/_StatusChip.cshtml`. Amber is incomplete or
 * pending, navy is Review and other in-flight states, red is blocked / failed
 * / denied, green is confirmed completion only, and neutral covers absent,
 * loading, current and settled-terminal states.
 */
const STATE_TONES: Record<string, [Tone, IconName]> = {
  // Case lifecycle
  'not ready': ['amber', 'alert-triangle'],
  review: ['navy', 'info'],
  held: ['amber', 'clock'],
  completed: ['green', 'check-circle'],
  'post report completion': ['green', 'check-circle'],
  cancelled: ['neutral', 'info'],
  'provider cancellation': ['neutral', 'info'],
  archived: ['neutral', 'info'],
  'created in error': ['neutral', 'alert-circle'],
  reopened: ['navy', 'info'],
  open: ['navy', 'info'],
  // Intake and Triage
  'needs sorting': ['amber', 'alert-triangle'],
  'blocked intake': ['red', 'alert-circle'],
  blocked: ['red', 'alert-circle'],
  'draft ready': ['navy', 'file-text'],
  'instruction draft': ['navy', 'file-text'],
  'document text required': ['amber', 'file-text'],
  unsupported: ['amber', 'file-text'],
  'awaiting information': ['amber', 'clock'],
  'finding recorded': ['navy', 'check-circle'],
  'registration missing': ['amber', 'alert-triangle'],
  // Query freshness
  current: ['neutral', 'check-circle'],
  loading: ['neutral', 'refresh-cw'],
  refreshing: ['neutral', 'refresh-cw'],
  stale: ['amber', 'clock'],
  partial: ['amber', 'alert-triangle'],
  'limit reached': ['amber', 'alert-triangle'],
  unavailable: ['neutral', 'alert-circle'],
  failed: ['red', 'alert-circle'],
  'technical failure': ['red', 'alert-circle'],
  error: ['red', 'alert-circle'],
  // Access and mutation
  denied: ['red', 'lock'],
  unauthenticated: ['red', 'lock'],
  disabled: ['neutral', 'lock'],
  approved: ['navy', 'check-circle'],
  enabled: ['navy', 'check-circle'],
  conflict: ['red', 'alert-triangle'],
  'stale version': ['red', 'alert-triangle'],
  'lease lost': ['red', 'lock'],
  'lease held': ['navy', 'lock'],
  editing: ['navy', 'lock'],
  'lease expired': ['amber', 'clock'],
  locked: ['neutral', 'lock'],
  // Access review
  'due no review recorded': ['amber', 'clock'],
  due: ['amber', 'clock'],
  recorded: ['neutral', 'check-circle'],
  // External and document custody
  pending: ['amber', 'clock'],
  retry: ['amber', 'refresh-cw'],
  success: ['green', 'check-circle'],
  unknown: ['neutral', 'info'],
};

/** Mirrors the partial's key normalisation: humanise, lower-case, strip punctuation, collapse spaces. */
function keyOf(state: string): string {
  const humanised = state
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .toLowerCase();
  return humanised
    .replace(/[^a-z0-9 ]/g, '')
    .split(/\s+/)
    .filter(Boolean)
    .join(' ');
}

/** Resolves the tone and icon a state text receives. Exported so cards can share the decision. */
export function toneForState(state: string): { tone: Tone; icon: IconName } {
  const hit = STATE_TONES[keyOf(state)] ?? ['neutral', 'info'];
  return { tone: hit[0], icon: hit[1] };
}

export interface StatusChipProps extends Omit<HTMLAttributes<HTMLSpanElement>, 'children'> {
  /**
   * The exact state text to display, in its settled casing — `Not ready`,
   * `Review`, `Held`, `Unidentified`, `Blocked`, `Completed`, `Stale`… The
   * chip never rewrites the words; it only picks a tone and an icon for them.
   */
  state: string;
  /** Explicit tone override (navy | amber | red | green | neutral). */
  tone?: Tone;
  /** Explicit icon override; defaults to the state's mapped glyph. */
  icon?: IconName;
  /** Optional count appended in brackets, e.g. `Review (12)`. */
  count?: number;
}

/**
 * Pill-shaped state chip: text + Lucide icon on a tinted ground. Every chip
 * carries its label, so no state is conveyed by colour or icon alone.
 */
export function StatusChip({ state, tone, icon, count, className, ...rest }: StatusChipProps) {
  const text = state.trim() || 'Unknown';
  const mapped = toneForState(text);
  const t = tone ?? mapped.tone;
  const display = count !== undefined && !/\d/.test(text) ? `${text} (${count})` : text;
  return (
    <span className={cx('status-chip', `status-chip--${t}`, className)} {...rest}>
      <Icon name={icon ?? mapped.icon} />
      <span>{display}</span>
    </span>
  );
}
