// The three v31 Triage Case proposals, their state presets and the synthetic
// fixture. Labels are the live strings from OperatorLabels.Triage,
// CaseWorkspaceLabels.Files and _TriageCase.cshtml (origin/dev 81b571c36).
import { scene } from './triage-scenes.mjs';

export const designs = [
  {
    id: 'a',
    name: 'Image stage',
    subtitle: 'One large photograph with a filmstrip, directly under the ribbon.',
    description: 'The page keeps today’s single column. A large lead image with a filmstrip takes the top of the record. Below it come a two-box Determinations read-out, then Files (Documents and Correspondence), then Notes.',
    benefit: 'It is the closest to today’s page and the easiest to move to. One image at a time, at full width, is the best size for judging damage.',
    tradeoff: 'Determinations and Notes are below the images, so you scroll down to see the record’s status beside a photograph.',
  },
  {
    id: 'b',
    name: 'Inspection split',
    subtitle: 'A persistent viewer on the left; the decision, notes and status on the right.',
    description: 'A two-column workspace. On the left is a large viewer with Tag and Crop in place and thumbnails underneath. On the right are stacked cards: Determinations, Correspondence status, Exact response evidence when present, and Notes. Files (Documents and Correspondence) run full width below.',
    benefit: 'The photographs and the decision stay side by side, so you can assess and record without scrolling or opening the viewer.',
    tradeoff: 'It is the densest layout. Below 1180px it folds into one column, and its image is smaller than A’s at the same width.',
    recommendation: true,
  },
  {
    id: 'c',
    name: 'Contact sheet',
    subtitle: 'A sticky status bar, then tabs for Images, Files and Notes.',
    description: 'A sticky summary bar holds the state, the two determinations and the assignee. Below it, tabs switch between Images (the default, a large-thumbnail contact sheet grouped by tag), Files (Documents and Correspondence) and Notes.',
    benefit: 'It gives the most room to images, puts every photograph on screen at once and needs the least scrolling.',
    tradeoff: 'Files and Notes are one click away rather than on the page. Tabs change the shape of the page compared with the regular Case record.',
  },
];

// The seven-view walk-round plus a close-up and the odometer.
const imageSet = [
  { file: 'IMG_4101_front_left.jpg', view: 'front-left', damage: false, tags: [['Overview', 'blue']] },
  { file: 'IMG_4102_offside.jpg', view: 'offside', damage: true, tags: [['Damage', 'red']] },
  { file: 'IMG_4103_offside_close.jpg', view: 'close-up', damage: true, tags: [['Damage', 'red']] },
  { file: 'IMG_4104_rear.jpg', view: 'rear', damage: false, tags: [['Overview', 'blue']] },
  { file: 'IMG_4105_front.jpg', view: 'front', damage: true, tags: [['Damage', 'red']], cropped: true },
  { file: 'IMG_4106_odometer.jpg', view: 'odometer', damage: false, tags: [['Odometer', 'green']] },
  { file: 'IMG_4107_nearside.jpg', view: 'nearside', damage: false, tags: [] },
];

export function buildImages(count) {
  const out = [];
  for (let i = 0; i < count; i += 1) {
    const base = imageSet[i % imageSet.length];
    const round = Math.floor(i / imageSet.length);
    const colour = ['silver', 'blue', 'red'][round % 3];
    const file = round === 0 ? base.file : base.file.replace('IMG_41', `IMG_4${1 + round}`);
    out.push({ id: `img${i + 1}`, file, size: 2_400_000 + (i * 137_000) % 900_000, tags: base.tags, cropped: !!base.cropped, src: scene({ view: base.view, colour, damage: base.damage, light: round === 2 ? 'dusk' : 'day' }) });
  }
  return out;
}

export const fixture = {
  reference: 't.QDOS26031',
  registration: 'MA59BDY',
  principal: 'SABS',
  opened: '02/10/2026 14:12',
  sourceEmail: 'E-mail',
  sourceUpload: 'Upload',
  roster: ['alex (you)', 'R. Khan', 'S. Patel'],
  mailbox: 'desk@collisionengineers.co.uk',
  requester: 'claims@sab-solicitors.example',
  subject: 'Fw: (EREF8) RTA on 01/10/2026 : Mr A Sample (Our Ref: SAB/49127/1, Vehicle: MA59BDY)',
  chaserBody: 'Dear Sir or Madam,\n\nThank you for your Triage request for MA59BDY. To complete our assessment, please send clear photographs of the offside rear quarter and the odometer.\n\nKind regards,\nCollision Engineers',
  outcomeBody: 'Dear Sir or Madam,\n\nWe have completed the Triage of MA59BDY.\n\nRoadworthiness: Roadworthy\nRepair outcome: Repairable\n\nKind regards,\nCollision Engineers',
  documents: [
    { name: 'Fw (EREF8) RTA on 01102026 Mr A Sample (Our Ref SAB491271, Vehicle MA59BDY).eml', origin: 'E-mail', when: '02/10/2026 14:12', size: '3.1 MB', role: 'Correspondence', mail: true },
    { name: 'Triage request SAB-49127.pdf', origin: 'E-mail attachment', when: '02/10/2026 14:12', size: '182 KB', role: 'Instruction', mail: false },
  ],
  history: [
    ['02/10/2026', '14:12', 'Pegasus', 'Triage created', 'Created from e-mail.'],
  ],
};

// Each preset is a starting state for the page; every action in the mockup
// moves it on from there.
export const presets = [
  ['open', 'Open · unassigned · e-mail'],
  ['assigned', 'Open · assigned'],
  ['awaiting', 'Awaiting information · chaser sent'],
  ['finding', 'Finding recorded'],
  ['completed', 'Completed · reply with finding'],
  ['replied', 'Completed · reply sent · Case linked'],
  ['cancelled', 'Cancelled'],
  ['upload', 'Open · uploaded (no e-mail)'],
  ['blocked', 'Send in flight (Submitted)'],
  ['unknown', 'Send status Unknown'],
  ['response', 'Response evidence to link'],
  ['noimages', 'No images'],
  ['many', 'Twenty images'],
];

export const presetState = {
  open: { state: 'Open', assignee: null, finding: null, email: true, images: 7 },
  assigned: { state: 'Open', assignee: 'alex', finding: null, email: true, images: 7 },
  awaiting: { state: 'Awaiting information', assignee: 'alex', finding: null, email: true, images: 7, send: 'Sent', chaserSent: true },
  finding: { state: 'Finding recorded', assignee: 'alex', finding: { road: 'Roadworthy', repair: 'Repairable' }, email: true, images: 7, chaserSent: true, send: 'Sent' },
  completed: { state: 'Completed', assignee: 'alex', finding: { road: 'Roadworthy', repair: 'Repairable' }, email: true, images: 7, chaserSent: true, send: 'Sent' },
  replied: { state: 'Completed', assignee: 'alex', finding: { road: 'Roadworthy', repair: 'Repairable' }, email: true, images: 7, chaserSent: true, replySent: true, send: 'Sent', linkedCase: 'QDOS26214' },
  cancelled: { state: 'Cancelled', assignee: 'R. Khan', finding: null, email: true, images: 7 },
  upload: { state: 'Open', assignee: null, finding: null, email: false, images: 5 },
  blocked: { state: 'Awaiting information', assignee: 'alex', finding: null, email: true, images: 7, send: 'Submitted', blocked: true },
  unknown: { state: 'Awaiting information', assignee: 'alex', finding: null, email: true, images: 7, send: 'Unknown', blocked: true },
  response: { state: 'Awaiting information', assignee: 'alex', finding: null, email: true, images: 7, send: 'Sent', chaserSent: true, candidate: true, linkedResponse: true },
  noimages: { state: 'Open', assignee: null, finding: null, email: true, images: 0 },
  many: { state: 'Open', assignee: 'alex', finding: null, email: true, images: 20 },
};
