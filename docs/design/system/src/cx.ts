/** Joins truthy class names. */
export function cx(...parts: Array<string | false | null | undefined>): string | undefined {
  const s = parts.filter(Boolean).join(' ');
  return s.length ? s : undefined;
}

/**
 * The state channel: one vocabulary drives rails, chips, icon tints and card
 * accents through `data-state`. Values are the ones `site.css` recognises.
 */
export type StateName =
  | 'review'
  | 'not-ready'
  | 'needs-sorting'
  | 'pending'
  | 'held'
  | 'stale'
  | 'partial'
  | 'blocked'
  | 'failed'
  | 'denied'
  | 'conflict'
  | 'lease-lost'
  | 'stale-version'
  | 'completed'
  | 'unavailable'
  | 'loading'
  | 'cancelled'
  | 'created-in-error'
  | 'current'
  | 'unread';

/** Chip and card tones: navy = Review, amber = incomplete/pending, red = failure, green = confirmed completion only. */
export type Tone = 'navy' | 'amber' | 'red' | 'green' | 'neutral';
