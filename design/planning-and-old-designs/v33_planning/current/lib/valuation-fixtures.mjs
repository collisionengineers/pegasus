// The five proposals, their states and the undecided choices each page can
// switch between. Synthetic: one Case, Glass's connected, the other four
// guide sources not connected, as in production on 6 October 2026.

export const designs = [
  {
    id: 'a',
    name: 'Calculation under the boxes',
    subtitle: 'The smallest move: the calculation sits directly beneath the three values.',
    description: 'Retail value, Trade value and Engineer\'s Value stay on the first row. The calculation is the next row, in the same three columns, and each adjustment shows its amount in its own label line. The source cards and On the report follow, unchanged.',
    benefit: 'Nothing the Engineer already knows moves except the calculation, which now touches the box it fills. Least to build and least to relearn.',
    tradeoff: 'The five source cards keep their full height, four of them a notice and empty boxes, so the section is as long as today\'s while editing.',
    defaults: { record: 'tag', ai: 'tools' },
  },
  {
    id: 'b',
    name: 'Worksheet',
    subtitle: 'One sum, read top to bottom, ending in the Engineer\'s Value box.',
    description: 'The sources come first. Beneath them one worksheet starts from the guide retail, takes one line per adjustment with its control and its amount, and ends in the three values as the total line.',
    benefit: 'The page reads the way the figure is worked out, and the proposal and the box are one thing: the total line is the box.',
    tradeoff: 'The three values leave the top of the section, where they were placed on 26 September. It is the tallest of the five, about a tenth taller than today while editing.',
    defaults: { record: 'line', ai: 'own' },
  },
  {
    id: 'c',
    name: 'Three columns',
    subtitle: 'Each value has its origin directly beneath it.',
    description: 'The three values head three columns. The sources become rows under Retail value and Trade value, the figures they fill. The calculation stands under Engineer\'s Value, the figure it fills.',
    benefit: 'Keeps the three values first and puts the calculation closer to its box than any other design that keeps that row. The shortest of the five: about a fifth shorter than today while editing, a third shorter once a calculation is recorded.',
    tradeoff: 'The calculation column is a third of the width, so its controls stack. The total reads above its sum, not below it.',
    defaults: { record: 'line', ai: 'own' },
    recommendation: true,
  },
  {
    id: 'd',
    name: 'Chosen source opens',
    subtitle: 'The calculation hangs from the source it starts from.',
    description: 'The sources are full-width rows. The chosen one opens: its calculation and the three values sit inside it, so "from Glass\'s retail" is where the eye already is.',
    benefit: 'Which source the value came from cannot be missed, and a source with no provider costs two lines, not a card.',
    tradeoff: 'The three values move with the chosen row, so their place on the page is not fixed, and with no source chosen they stand alone at the foot.',
    defaults: { record: 'tag', ai: 'own' },
  },
  {
    id: 'e',
    name: 'Side by side',
    subtitle: 'Sources on the left, the value and its whole sum on the right.',
    description: 'The left pane holds the sources. The right pane holds everything about the value: Retail value and Trade value, the calculation, the lines of the sum, and the Engineer\'s Value as its last line.',
    benefit: 'Cause and effect sit side by side with no scrolling between them, and the sum reads downward into the Engineer\'s Value, which is the only copy of the figure.',
    tradeoff: 'The three values leave the top row. The source boxes are narrower than in C, and below 1180px the two panes stack.',
    defaults: { record: 'line', ai: 'own' },
  },
];

// [id, label]. "-read" is the same record outside an edit session.
export const presets = [
  ['fetched', 'Editing · Glass\'s fetched, nothing recorded (the screenshots)'],
  ['fetched-read', 'Reading · Glass\'s fetched, nothing recorded'],
  ['recorded', 'Editing · calculation recorded'],
  ['recorded-read', 'Reading · calculation recorded'],
  ['own', 'Editing · Engineer\'s own figure typed'],
  ['refused', 'Editing · calculation cannot be worked out'],
  ['claimant-vat', 'Editing · claimant is VAT registered'],
  ['pending', 'Editing · AI market research in progress'],
  ['inspection', 'Editing · Inspection view'],
  ['empty', 'Editing · nothing recorded'],
  ['empty-read', 'Reading · nothing recorded'],
];

// Today's page is the captured section, so it has only the captured states.
export const livePresets = presets.filter(([id]) => ['fetched', 'fetched-read', 'recorded', 'recorded-read', 'pending', 'empty-read'].includes(id));

export const options = [
  {
    key: 'record',
    label: 'Recorded calculation (item D)',
    values: [['tag', 'Source word on the Engineer\'s Value label'], ['line', 'Basis, Applied by and Adjustments line']],
  },
  {
    key: 'ai',
    label: 'AI market research (item F)',
    values: [['tools', 'Row above the sources (today)'], ['own', 'Its own place among the sources']],
  },
];
