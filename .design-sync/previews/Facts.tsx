import { Facts } from '@pegasus/design-system';

/** Two titled columns — vehicle and instruction — with one quiet value for a fact not yet set. */
export const TwoGroups = () => (
  <Facts
    groups={[
      {
        title: 'Vehicle',
        items: [
          { term: 'Registration', value: 'LM19 KXR' },
          { term: 'Make', value: 'Volkswagen' },
          { term: 'Model', value: 'Golf GTD' },
          { term: 'Mileage', value: '48,210' },
        ],
      },
      {
        title: 'Instruction',
        items: [
          { term: 'Principal', value: 'AXA' },
          { term: 'Claim', value: 'AX/44/210983' },
          { term: 'Received', value: '12 Aug 09:14' },
          { term: 'Engineer', value: 'Not assigned', quiet: true },
        ],
      },
    ]}
  />
);

/** Three columns; the grid wraps at 230px per column so the third drops under on a narrow body. */
export const ThreeGroups = () => (
  <Facts
    groups={[
      {
        title: 'Vehicle',
        items: [
          { term: 'Registration', value: 'YD68 TFA' },
          { term: 'Make', value: 'Ford' },
          { term: 'Model', value: 'Focus Titanium' },
        ],
      },
      {
        title: 'Claim',
        items: [
          { term: 'Principal', value: 'Direct Line' },
          { term: 'Claimant', value: 'P. Sandhu' },
          { term: 'Accident', value: '3 Aug 2026' },
        ],
      },
      {
        title: 'Progress',
        items: [
          { term: 'Stage', value: 'Review' },
          { term: 'Engineer', value: 'R. Achebe' },
          { term: 'Inspected', value: 'Not yet', quiet: true },
        ],
      },
    ]}
  />
);
