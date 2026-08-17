import { DetailList } from '@pegasus/design-system';

/** The details of a received message: term column, bold values, hairline rows. */
export const MessageDetails = () => (
  <div style={{ maxWidth: 640 }}>
    <DetailList
      items={[
        { term: 'From', value: 'claims.engineering@axa-insurance.co.uk' },
        { term: 'Mailbox', value: 'Inbox' },
        { term: 'Received', value: '12 Aug 09:14' },
        { term: 'Subject', value: 'New instruction — LM19 KXR — AX/44/210983' },
        { term: 'Attachments', value: '3 (2 PDF, 1 image)' },
        { term: 'Principal', value: 'AXA' },
      ]}
    />
  </div>
);

/** Case identity on the assessment screen; an em-dash stands for a value not recorded. */
export const CaseIdentity = () => (
  <div style={{ maxWidth: 640 }}>
    <DetailList
      items={[
        { term: 'Case/PO', value: 'CE-2026-01432' },
        { term: 'Principal', value: 'AXA' },
        { term: 'Registration', value: 'LM19 KXR' },
        {
          term: 'Engineer',
          value: (
            <>
              <span aria-hidden="true">—</span>
              <span className="vh">Not assigned</span>
            </>
          ),
        },
      ]}
    />
  </div>
);
