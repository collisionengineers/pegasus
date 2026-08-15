import { ProposalDiff } from '@pegasus/design-system';

/** The recorded value beside the suggested one, equal weight — neither is the default outcome. */
export const RecordedVersusSuggested = () => (
  <div style={{ maxWidth: 720 }}>
    <ProposalDiff
      recorded={{ title: 'On the case now', children: '£13,400' }}
      proposed={{ title: 'Claude suggests', children: '£14,250' }}
    />
  </div>
);

/** Nothing recorded yet: the em-dash idiom on the left, the suggestion on the right. */
export const NothingRecordedYet = () => (
  <div style={{ maxWidth: 720 }}>
    <ProposalDiff
      recorded={{
        title: 'On the case now',
        children: (
          <p className="tabular">
            <span aria-hidden="true">—</span>
            <span className="vh">No recorded value</span>
          </p>
        ),
      }}
      proposed={{ title: 'Claude suggests', children: <p className="tabular">6 Aug 2026</p> }}
    />
  </div>
);

/** Longer text on both sides; the columns stay equal width and wrap independently. */
export const AddressChange = () => (
  <div style={{ maxWidth: 720 }}>
    <ProposalDiff
      recorded={{ title: 'On the case now', children: '14 Ridgeway Close, Reading, RG2 8QT' }}
      proposed={{ title: 'Claude suggests', children: 'Kingsway Accident Repair, Unit 4 Station Road, Theale, RG7 4AA' }}
    />
  </div>
);
