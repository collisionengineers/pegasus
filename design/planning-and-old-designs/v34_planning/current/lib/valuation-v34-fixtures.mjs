// The v34 proposal's states and the undecided choices the strip switches.
// Synthetic: one Case, Glass's connected, the other four guide sources not
// connected, as in production on 8 October 2026.

// [id, label]. "-read" is the same record outside an edit session.
export const presets = [
  ['fetched', 'Editing · Glass\'s fetched, others typed, nothing recorded'],
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

export const options = [
  {
    key: 'word',
    label: 'Chosen card\'s word (item B)',
    values: [['selected', 'Selected'], ['basis', 'Basis']],
  },
  {
    key: 'manual',
    label: 'Source with no provider (item C)',
    values: [['tag', 'Manual tag'], ['sentence', 'Standing sentence (23 Sep)']],
  },
];

export const defaults = { word: 'selected', manual: 'tag' };
