import { FieldCard } from '@pegasus/design-system';

/** One extracted field: uppercase title, the value, and a small detail line. */
export const Plain = () => (
  <div style={{ maxWidth: 320, border: '1px solid var(--line)' }}>
    <FieldCard title="Registration" detail="Extracted · 12 Aug 09:14">
      LM19 KXR
    </FieldCard>
  </div>
);

/** A conflicting value: the amber left rail, with the detail naming the other source. */
export const Conflict = () => (
  <div style={{ maxWidth: 320, border: '1px solid var(--line)' }}>
    <FieldCard title="Accident date" detail="E-mail says 4 Aug 2026" conflict>
      6 Aug 2026
    </FieldCard>
  </div>
);

/** Title and value only, no detail line. */
export const NoDetail = () => (
  <div style={{ maxWidth: 320, border: '1px solid var(--line)' }}>
    <FieldCard title="Claimant">J. Okafor</FieldCard>
  </div>
);
