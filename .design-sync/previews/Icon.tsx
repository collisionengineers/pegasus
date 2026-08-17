import { Icon, ICON_PATHS, type IconName } from '@pegasus/design-system';

const names = Object.keys(ICON_PATHS) as IconName[];

/** Every glyph in the Pegasus sprite — the only icon set the interface uses. */
export const AllGlyphs = () => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16 }}>
    {names.map((n) => (
      <div key={n} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6, minWidth: 84 }}>
        <Icon name={n} size="lg" />
        <small style={{ fontSize: '0.75rem', color: '#6b6560' }}>{n}</small>
      </div>
    ))}
  </div>
);

/** The three sizes: `sm` (chips, buttons), default, `lg` (admin cards). */
export const Sizes = () => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
      <Icon name="alert-triangle" size="sm" />
      <small style={{ fontSize: '0.75rem' }}>sm · .875rem</small>
    </div>
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
      <Icon name="alert-triangle" />
      <small style={{ fontSize: '0.75rem' }}>md · 1.125rem</small>
    </div>
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
      <Icon name="alert-triangle" size="lg" />
      <small style={{ fontSize: '0.75rem' }}>lg · 1.25rem</small>
    </div>
  </div>
);

/** An icon takes `currentColor`, so it follows the text it sits in; a labelled icon is exposed to assistive tech. */
export const InheritsColour = () => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: '#b3261e' }}><Icon name="alert-circle" /> Blocked</span>
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: '#8a5a00' }}><Icon name="clock" /> Held</span>
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: '#1f3a5f' }}><Icon name="info" /> Review</span>
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Icon name="lock" label="Locked" /> Locked</span>
  </div>
);
