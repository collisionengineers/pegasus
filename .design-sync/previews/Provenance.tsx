import { DataRow, Provenance } from '@pegasus/design-system';

const WORDS = ['Staff', 'Extracted', 'AI', 'E-mail', 'Lookup', 'Principal', 'Automatic'] as const;

/** All seven words in a row; each is a small icon whose one-word tooltip shows on hover and keyboard focus (captions here are preview labels only). */
export const AllWords = () => (
  <div style={{ display: 'flex', alignItems: 'flex-start', gap: 18 }}>
    {WORDS.map((w) => (
      <div key={w} style={{ display: 'grid', justifyItems: 'center', gap: 2 }}>
        <Provenance word={w} />
        <span style={{ fontSize: 11, color: '#6b6b6b' }}>{w}</span>
      </div>
    ))}
  </div>
);

/** In place: the end slot of data rows, supplementary to the value. */
export const InDataRows = () => (
  <div style={{ maxWidth: 560 }}>
    <DataRow field="Registration" value="LM19 KXR" end={<Provenance word="Lookup" />} />
    <DataRow field="Accident date" value="6 Aug 2026" end={<Provenance word="Extracted" />} />
    <DataRow field="Claimant" value="J. Okafor" end={<Provenance word="E-mail" />} />
    <DataRow field="Pre-accident value" suggested="£14,250" end={<Provenance word="AI" />} />
    <DataRow field="Engineer" value="R. Patel" end={<Provenance word="Staff" />} />
  </div>
);
